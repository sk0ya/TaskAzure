using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using Clipboard = System.Windows.Clipboard;

namespace TaskAzure;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm, App app)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        InputBindings.Add(new KeyBinding(
            new RelayCommand(async () => await _vm.RefreshAsync()),
            new KeyGesture(Key.F5)));
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
        var svc = new SettingsService();
        var s = svc.Load();
        s.WindowLeft = Left;
        s.WindowTop = Top;
        svc.Save(s);
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
        if (GetVm(sender) is { } vm) SetClipboard(vm.Id.ToString());
    }

    private void MenuCopyTitle_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) SetClipboard(vm.Title);
    }

    private void MenuOpenWeb_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) OpenUrl(vm.WebUrl);
    }

    private void MenuCreateLink_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is not { } vm) return;
        try
        {
            var data = new System.Windows.DataObject();
            data.SetData(System.Windows.DataFormats.Text, vm.MarkdownLink);
            data.SetData(System.Windows.DataFormats.Html, BuildHtmlClipboard(vm.HtmlLink));
            Clipboard.SetDataObject(data);
        }
        catch { }
    }

    // Windows HTML クリップボード形式に必要なヘッダーを付与する
    private static string BuildHtmlClipboard(string html)
    {
        const string header =
            "Version:0.9\r\n" +
            "StartHTML:00000000\r\n" +
            "EndHTML:00000000\r\n" +
            "StartFragment:00000000\r\n" +
            "EndFragment:00000000\r\n";
        const string pre  = "<html><body><!--StartFragment-->";
        const string post = "<!--EndFragment--></body></html>";

        var startHtml     = System.Text.Encoding.UTF8.GetByteCount(header);
        var startFragment = startHtml + System.Text.Encoding.UTF8.GetByteCount(pre);
        var endFragment   = startFragment + System.Text.Encoding.UTF8.GetByteCount(html);
        var endHtml       = endFragment + System.Text.Encoding.UTF8.GetByteCount(post);

        return header
            .Replace("StartHTML:00000000",     $"StartHTML:{startHtml:D8}")
            .Replace("EndHTML:00000000",       $"EndHTML:{endHtml:D8}")
            .Replace("StartFragment:00000000", $"StartFragment:{startFragment:D8}")
            .Replace("EndFragment:00000000",   $"EndFragment:{endFragment:D8}")
            + pre + html + post;
    }

    private void MenuOpenPR_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is { } vm) OpenUrl(vm.WebUrl);
    }

    private void MenuCopyPRId_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is { } vm) SetClipboard(vm.Id.ToString());
    }

    private void MenuCopyPRTitle_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is { } vm) SetClipboard(vm.Title);
    }

    private void MenuCreatePRLink_Click(object sender, RoutedEventArgs e)
    {
        if (GetPrVm(sender) is not { } vm) return;
        try
        {
            var data = new System.Windows.DataObject();
            data.SetData(System.Windows.DataFormats.Text, vm.MarkdownLink);
            data.SetData(System.Windows.DataFormats.Html, BuildHtmlClipboard(vm.HtmlLink));
            Clipboard.SetDataObject(data);
        }
        catch { }
    }

    private static void SetClipboard(string text)
    {
        try { Clipboard.SetText(text); } catch { }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
