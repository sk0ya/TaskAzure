using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace TaskAzure.Windows;

/// <summary>シンプルな入力ダイアログ</summary>
internal static class InputDialog
{
    public static string? Show(Window owner, string message, string title, string defaultValue = "")
    {
        string? result = null;

        var win = new Window
        {
            Title = title,
            Width = 340,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)),
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };

        var sp = new StackPanel { Margin = new Thickness(16) };

        var lbl = new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xDD, 0xFF)),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
        };

        var tb = new TextBox
        {
            Text = defaultValue,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xEE, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x44, 0x66)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 5, 6, 5),
            FontSize = 12,
            CaretBrush = Brushes.White,
            Margin = new Thickness(0, 0, 0, 12),
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var btnOk = new Button
        {
            Content = "OK",
            Width = 72,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
        };
        var btnCancel = new Button { Content = "キャンセル", Width = 80, IsCancel = true };

        btnOk.Click += (_, _) => { result = tb.Text; win.Close(); };
        btnCancel.Click += (_, _) => win.Close();

        btnPanel.Children.Add(btnOk);
        btnPanel.Children.Add(btnCancel);

        sp.Children.Add(lbl);
        sp.Children.Add(tb);
        sp.Children.Add(btnPanel);
        win.Content = sp;

        win.Loaded += (_, _) => { tb.SelectAll(); tb.Focus(); };
        win.ShowDialog();
        return result;
    }
}
