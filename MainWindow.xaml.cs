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

    private void MenuCopyId_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) SetClipboard(vm.IdDisplay);
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
        if (GetVm(sender) is { } vm) SetClipboard(vm.MarkdownLink);
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
