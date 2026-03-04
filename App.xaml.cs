using System.Drawing;
using System.Windows;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using TaskAzure.Windows;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace TaskAzure;

public partial class App : Application
{
    private WinForms.NotifyIcon? _tray;
    private MainWindow? _main;
    private readonly SettingsService _settingsService = new();
    private readonly CredentialService _credService = new();
    private readonly AzureDevOpsService _adoService = new();
    private readonly TemplateService _templateService = new();
    private MainViewModel? _mainVm;

    internal AzureDevOpsService AdoService => _adoService;
    internal SettingsService SettingsSvc => _settingsService;
    internal TemplateService TemplateSvc => _templateService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        InitTrayIcon();

        if (!_settingsService.IsConfigured())
        {
            OpenSettings(firstRun: true);
        }
        else
        {
            await ShowMainWindowAsync();
        }
    }

    private void InitTrayIcon()
    {
        _tray = new WinForms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "TaskAzure",
            Visible = true,
        };

        var menu = new WinForms.ContextMenuStrip();
        var showHide = new WinForms.ToolStripMenuItem("表示/非表示(&V)");
        showHide.Click += (_, _) => ToggleMainWindow();

        var refresh = new WinForms.ToolStripMenuItem("更新(&R)");
        refresh.Click += async (_, _) =>
        {
            if (_mainVm != null) await _mainVm.RefreshAsync();
        };

        var templateMgr = new WinForms.ToolStripMenuItem("テンプレート管理(&T)...");
        templateMgr.Click += (_, _) => OpenTemplateManager();

        var settings = new WinForms.ToolStripMenuItem("設定(&S)...");
        settings.Click += (_, _) => OpenSettings();

        var exit = new WinForms.ToolStripMenuItem("終了(&X)");
        exit.Click += (_, _) => Shutdown();

        menu.Items.Add(showHide);
        menu.Items.Add(refresh);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(templateMgr);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(settings);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exit);

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ToggleMainWindow();
    }

    private void ToggleMainWindow()
    {
        if (_main == null) return;
        if (_main.IsVisible)
            _main.Hide();
        else
            _main.Show();
    }

    internal void OpenTemplateManager()
    {
        var vm = new TemplateManagerViewModel();
        var win = new TemplateManagerWindow(vm) { Owner = _main };
        win.ShowDialog();
    }

    internal void OpenSettings(bool firstRun = false)
    {
        var vm = new SettingsViewModel(_settingsService);
        var win = new SettingsWindow(vm)
        {
            Owner = _main,
        };
        win.SettingsSaved += async () =>
        {
            _main?.Hide();
            _main?.Close();
            _main = null;
            _mainVm?.StopTimer();
            await ShowMainWindowAsync();
        };

        if (firstRun)
        {
            win.ShowDialog();
            // キャンセルされた場合のみ終了。保存時は SettingsSaved イベントが ShowMainWindowAsync を呼ぶ
            if (!_settingsService.IsConfigured())
                Shutdown();
        }
        else
        {
            win.Owner = _main;
            win.ShowDialog();
        }
    }

    private async Task ShowMainWindowAsync()
    {
        _mainVm = new MainViewModel(_adoService, _settingsService, _credService);
        _main = new MainWindow(_mainVm, this);

        var s = _settingsService.Load();
        _main.Left = s.WindowLeft;
        _main.Top = s.WindowTop;

        _main.Show();

        try
        {
            await _mainVm.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初期化に失敗しました:\n{ex.Message}\n\n設定を確認してください。",
                "TaskAzure", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainVm?.StopTimer();
        _tray?.Dispose();
        _adoService.Dispose();
        base.OnExit(e);
    }

    private static Icon CreateTrayIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.FillRoundedRectangle(new SolidBrush(Color.FromArgb(0, 120, 212)), 2, 2, 28, 28, 4);
            using var font = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("A", font, Brushes.White, new RectangleF(2, 2, 28, 28), sf);
        }
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush,
        float x, float y, float w, float h, float r)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
