using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfBinding = System.Windows.Data.Binding;

namespace TaskAzure.Windows;

public partial class CsvCreatorWindow : Window
{
    private const string OutputEnabledColumnName = "__taskazure_output_enabled";
    private const string OutputEnabledHeader = "出力";

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

    private void ClearUserFilter_Click(object sender, RoutedEventArgs e)
        => _vm.UserFilterText = "";

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

        MessageBox.Show($"保存しました:\n{dlg.FileName}", "TaskAzure",
            MessageBoxButton.OK, MessageBoxImage.Information);

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
