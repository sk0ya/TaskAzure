using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace TaskAzure.Windows;

public partial class CsvCreatorWindow : Window
{
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
        PreviewGrid.ItemsSource = null;
        PreviewGrid.Columns.Clear();
        PreviewGrid.ItemsSource = _previewTable.DefaultView;
    }

    private void PreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        => DataGridHelper.ApplyDefaultColumnStyle(sender, e);

    private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        // DataGrid の編集をコミット
        PreviewGrid.CommitEdit(DataGridEditingUnit.Row, true);

        // DataTable → CSV 文字列
        var csv = CsvHelper.SerializeFromDataTable(_previewTable);

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
}
