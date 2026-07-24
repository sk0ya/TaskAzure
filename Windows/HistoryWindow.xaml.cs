using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskAzure.ViewModels;

namespace TaskAzure.Windows;

public partial class HistoryWindow : Window
{
    private readonly HistoryViewModel _vm;

    public HistoryWindow(HistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        InputBindings.Add(new KeyBinding(
            new RelayCommand(Close),
            new KeyGesture(Key.Escape)));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        await _vm.InitializeAsync();
    }

    private static HistoryItemViewModel? GetVm(object sender)
    {
        if (sender is MenuItem { Tag: HistoryItemViewModel vm }) return vm;
        if (sender is MenuItem mi
            && mi.Parent is ContextMenu cm
            && cm.PlacementTarget is FrameworkElement fe
            && fe.Tag is HistoryItemViewModel vm2) return vm2;
        return null;
    }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItemViewModel vm)
            ClipboardUtil.OpenUrl(vm.WebUrl);
    }

    private void MenuOpenWeb_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.OpenUrl(vm.WebUrl);
    }

    private void MenuCopyId_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.SetText(vm.Id.ToString());
    }

    private void MenuCopyTitle_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.SetText(vm.Title);
    }

    private void MenuCreateLink_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) ClipboardUtil.SetLink(vm.MarkdownLink, vm.HtmlLink);
    }

    private void MenuRemove_Click(object sender, RoutedEventArgs e)
    {
        if (GetVm(sender) is { } vm) _vm.Remove(vm);
    }

    private void Expander_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: HistoryItemViewModel vm })
            _vm.ToggleExpand(vm);
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e) => _vm.ExpandAll();

    private void CollapseAll_Click(object sender, RoutedEventArgs e) => _vm.CollapseAll();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
