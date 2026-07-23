using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaskAzure.ViewModels;
using TaskAzure.Windows;

namespace TaskAzure;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly App _app;
    private System.Windows.Threading.DispatcherTimer? _locationSaveTimer;

    public MainWindow(MainViewModel vm, App app)
    {
        InitializeComponent();
        _vm = vm;
        _app = app;
        DataContext = vm;

        InputBindings.Add(new KeyBinding(
            new RelayCommand(async () => await _vm.RefreshAsync()),
            new KeyGesture(Key.F5)));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => _app.OpenHistory()),
            new KeyGesture(Key.H, ModifierKeys.Control)));
    }

    // ─── ドラッグ移動 ─────────────────────────────────────────────
    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // スクロールバー上のクリックはドラッグしない
        var source = e.OriginalSource as DependencyObject;
        while (source != null)
        {
            if (source is System.Windows.Controls.Primitives.ScrollBar) return;
            source = VisualTreeHelper.GetParent(source);
        }
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        // ドラッグ中の連続発火をデバウンス — 500ms 後に1回だけ保存
        _locationSaveTimer?.Stop();
        _locationSaveTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(500) };
        _locationSaveTimer.Tick += (_, _) =>
        {
            _locationSaveTimer!.Stop();
            var s = _app.SettingsSvc.Load();
            s.WindowLeft = Left;
            s.WindowTop = Top;
            _app.SettingsSvc.Save(s);
        };
        _locationSaveTimer.Start();
    }

    // ─── コンテキストメニュー ─────────────────────────────────────
    private static WorkItemViewModel? GetVm(object sender)
    {
        if (sender is MenuItem { Tag: WorkItemViewModel vm }) return vm;
        if (sender is MenuItem mi
            && mi.Parent is ContextMenu cm
            && cm.PlacementTarget is FrameworkElement fe
            && fe.Tag is WorkItemViewModel vm2) return vm2;
        return null;
    }

    private static PullRequestViewModel? GetPrVm(object sender)
    {
        if (sender is MenuItem { Tag: PullRequestViewModel vm }) return vm;
        if (sender is MenuItem mi
            && mi.Parent is ContextMenu cm
            && cm.PlacementTarget is FrameworkElement fe
            && fe.Tag is PullRequestViewModel vm2) return vm2;
        return null;
    }

    private void MenuCopyId_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.SetText(vm.Id.ToString());
    }

    private void MenuCopyTitle_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.SetText(vm.Title);
    }

    private void MenuOpenWeb_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.OpenUrl(vm.WebUrl);
    }

    private void MenuCreateLink_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.SetLink(vm.MarkdownLink, vm.HtmlLink);
    }

    private void MenuShowHistory_Click(object sender, RoutedEventArgs e)
        => _app.OpenHistory();

    private void MenuOpenPR_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is { } vm) ClipboardUtil.OpenUrl(vm.WebUrl);
    }

    private void MenuCopyPRId_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is { } vm) ClipboardUtil.SetText(vm.Id.ToString());
    }

    private void MenuCopyPRTitle_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is { } vm) ClipboardUtil.SetText(vm.Title);
    }

    private void MenuCreatePRLink_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is { } vm) ClipboardUtil.SetLink(vm.MarkdownLink, vm.HtmlLink);
    }

    private void MenuCreateChildCsv_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is not { } vm) return;
        var settings = _app.SettingsSvc.Load();
        var creatorVm = new CsvCreatorViewModel(_app.AdoService, vm, settings, _app.SettingsSvc, _app.TemplateSvc);
        var win = new CsvCreatorWindow(creatorVm) { Owner = this };
        win.ShowDialog();
    }
}
