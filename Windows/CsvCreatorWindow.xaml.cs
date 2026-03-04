using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TaskAzure.Models;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfBinding = System.Windows.Data.Binding;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace TaskAzure.Windows;

public partial class CsvCreatorWindow : Window
{
    private const string OutputEnabledColumnName = "__taskazure_output_enabled";
    private const string OutputEnabledHeader = "出力";
    private static readonly Brush UserComboEditorBackground = new SolidColorBrush(Color.FromRgb(0x25, 0x2B, 0x42));
    private static readonly Brush UserComboEditorForeground = new SolidColorBrush(Color.FromRgb(0xEA, 0xF6, 0xFF));
    private static readonly Brush UserComboSelectionBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x5A));

    private readonly CsvCreatorViewModel _vm;
    private DataTable _previewTable = new();

    public CsvCreatorWindow(CsvCreatorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.PreviewRefreshRequested += RefreshPreview;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        _previewTable = _vm.GeneratePreviewTable();
        EnsureOutputSelectionColumn(_previewTable);
        PreviewGrid.ItemsSource = null;
        PreviewGrid.Columns.Clear();
        PreviewGrid.ItemsSource = _previewTable.DefaultView;
    }

    private void PreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (string.Equals(e.PropertyName, OutputEnabledColumnName, StringComparison.Ordinal))
        {
            e.Column = new DataGridCheckBoxColumn
            {
                Header = OutputEnabledHeader,
                Binding = new WpfBinding($"[{OutputEnabledColumnName}]")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                },
                Width = new DataGridLength(56),
            };
            return;
        }

        DataGridHelper.ApplyDefaultColumnStyle(sender, e);
    }

    private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();
    private void ApplyLastValues_Click(object sender, RoutedEventArgs e)
    {
        _vm.ApplyLastValues();
        RefreshPreview();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        // DataGrid の編集をコミット
        PreviewGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        PreviewGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var exportTable = BuildExportTable(_previewTable);

        // DataTable → CSV 文字列
        var csv = CsvHelper.SerializeFromDataTable(exportTable);

        // 保存ダイアログ
        var dlg = new SaveFileDialog
        {
            Title = "子タスクCSVを保存",
            Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"child_tasks_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, csv, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ファイルの保存に失敗しました:\n{ex.Message}", "TaskAzure",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Azure DevOps の Queries ページを開く
        try
        {
            Process.Start(new ProcessStartInfo(_vm.QueriesUrl) { UseShellExecute = true });
        }
        catch { /* ブラウザが開けなくても続行 */ }

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _vm.SaveCurrentValuesAsLastUsed();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.PreviewRefreshRequested -= RefreshPreview;
        base.OnClosed(e);
    }

    private void UserComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfComboBox combo) return;
        ApplyUserComboEditorStyle(combo);
        combo.RemoveHandler(WpfTextBox.TextChangedEvent, new TextChangedEventHandler(UserComboBox_FilterTextChanged));
        combo.AddHandler(WpfTextBox.TextChangedEvent, new TextChangedEventHandler(UserComboBox_FilterTextChanged));
    }

    private void UserComboBox_DropDownClosed(object sender, EventArgs e)
    {
        if (sender is not WpfComboBox combo) return;
        ResetComboFilter(combo);
    }

    private void UserComboBox_FilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not WpfComboBox combo) return;
        if (!combo.IsEditable) return;
        if (!combo.IsKeyboardFocusWithin && !combo.IsDropDownOpen) return;

        var view = CollectionViewSource.GetDefaultView(combo.ItemsSource);
        if (view == null) return;
        if (!view.CanFilter) return;

        var filterText = combo.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(filterText))
        {
            try
            {
                view.Filter = null;
                view.Refresh();
            }
            catch
            {
                // フィルタ未対応Viewでも落とさない
            }
            return;
        }

        try
        {
            view.Filter = item =>
                item is AdoUser user &&
                (user.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                 || user.UniqueName.Contains(filterText, StringComparison.OrdinalIgnoreCase));
            view.Refresh();
        }
        catch
        {
            return;
        }

        if (!combo.IsDropDownOpen)
            combo.IsDropDownOpen = true;
    }

    private static void ResetComboFilter(WpfComboBox combo)
    {
        var view = CollectionViewSource.GetDefaultView(combo.ItemsSource);
        if (view != null && view.CanFilter)
        {
            try
            {
                view.Filter = null;
                view.Refresh();
            }
            catch
            {
                // フィルタ解除失敗でも続行
            }
        }

        combo.Text = combo.SelectedItem is AdoUser user ? user.DisplayName : "";
    }

    private static void ApplyUserComboEditorStyle(WpfComboBox combo)
    {
        combo.ApplyTemplate();
        if (combo.Template.FindName("PART_EditableTextBox", combo) is not WpfTextBox editor)
            return;

        editor.Background = UserComboEditorBackground;
        editor.Foreground = UserComboEditorForeground;
        editor.CaretBrush = Brushes.White;
        editor.BorderThickness = new Thickness(0);
        editor.Padding = new Thickness(3, 1, 20, 1);
        editor.VerticalContentAlignment = VerticalAlignment.Center;
        editor.SelectionBrush = UserComboSelectionBrush;
        editor.SelectionOpacity = 1;
    }

    private static void EnsureOutputSelectionColumn(DataTable table)
    {
        if (!table.Columns.Contains(OutputEnabledColumnName))
        {
            var col = new DataColumn(OutputEnabledColumnName, typeof(bool))
            {
                DefaultValue = true,
                AllowDBNull = false,
            };
            table.Columns.Add(col);
            col.SetOrdinal(0);
        }

        var outputColumn = table.Columns[OutputEnabledColumnName]!;
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            if (row.IsNull(outputColumn))
                row[outputColumn] = true;
        }
    }

    private static DataTable BuildExportTable(DataTable previewTable)
    {
        var exportTable = previewTable.Clone();
        if (exportTable.Columns.Contains(OutputEnabledColumnName))
            exportTable.Columns.Remove(OutputEnabledColumnName);

        foreach (DataRow sourceRow in previewTable.Rows)
        {
            if (sourceRow.RowState == DataRowState.Deleted) continue;
            if (!IsOutputEnabled(sourceRow)) continue;

            var exportRow = exportTable.NewRow();
            foreach (DataColumn col in exportTable.Columns)
                exportRow[col.ColumnName] = sourceRow[col.ColumnName];
            exportTable.Rows.Add(exportRow);
        }

        return exportTable;
    }

    private static bool IsOutputEnabled(DataRow row)
    {
        if (!row.Table.Columns.Contains(OutputEnabledColumnName)) return true;

        var raw = row[OutputEnabledColumnName];
        if (raw == DBNull.Value || raw == null) return true;

        try
        {
            return Convert.ToBoolean(raw);
        }
        catch
        {
            return true;
        }
    }
}
