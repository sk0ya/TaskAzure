using System.Data;
using System.Windows;
using System.Windows.Controls;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace TaskAzure.Windows;

public partial class TemplateManagerWindow : Window
{
    private readonly TemplateManagerViewModel _vm;
    private DataTable _dataTable = new();
    private bool _updatingGrid;

    public TemplateManagerWindow(TemplateManagerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SelectedTemplate))
                LoadSelectedTemplate();
        };

        LoadSelectedTemplate();
    }

    // ─── DataTable <-> Template.CsvContent ───────────────────────────

    private void LoadSelectedTemplate()
    {
        _updatingGrid = true;
        try
        {
            CsvGrid.ItemsSource = null;
            CsvGrid.Columns.Clear();

            if (_vm.SelectedTemplate == null)
            {
                _dataTable = new DataTable();
                return;
            }

            _dataTable = CsvHelper.ParseToDataTable(_vm.SelectedTemplate.CsvContent);
            if (_dataTable.Columns.Count == 0)
            {
                _dataTable.Columns.Add("Title", typeof(string));
                _dataTable.Columns.Add("WorkItemType", typeof(string));
                _dataTable.Columns.Add("AssignedTo", typeof(string));
            }

            CsvGrid.ItemsSource = _dataTable.DefaultView;
        }
        finally
        {
            _updatingGrid = false;
        }
    }

    private void CommitGridToTemplate()
    {
        if (_vm.SelectedTemplate == null || _updatingGrid) return;
        // Accept any pending edits
        CsvGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _vm.SelectedTemplate.CsvContent = CsvHelper.SerializeFromDataTable(_dataTable);
    }

    private void CsvGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        => DataGridHelper.ApplyDefaultColumnStyle(sender, e);

    // ─── 行操作 ─────────────────────────────────────────────────────

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTemplate == null) return;
        CsvGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var row = _dataTable.NewRow();
        foreach (DataColumn col in _dataTable.Columns) row[col] = "";
        _dataTable.Rows.Add(row);
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTemplate == null) return;
        CsvGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var selected = CsvGrid.SelectedItems.Cast<DataRowView>().ToList();
        if (selected.Count == 0 && _dataTable.Rows.Count > 0)
        {
            // 選択なし → 最終行を削除
            _dataTable.Rows[_dataTable.Rows.Count - 1].Delete();
        }
        else
        {
            foreach (var rv in selected)
                rv.Row.Delete();
        }
        _dataTable.AcceptChanges();
    }

    // ─── 列操作 ─────────────────────────────────────────────────────

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTemplate == null) return;
        var name = InputDialog.Show(this, "追加する列名を入力してください:", "列を追加", "NewColumn");
        if (string.IsNullOrWhiteSpace(name)) return;

        // 重複チェック
        if (_dataTable.Columns.Contains(name))
        {
            MessageBox.Show($"列 '{name}' は既に存在します。", "TaskAzure",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CsvGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _dataTable.Columns.Add(new DataColumn(name, typeof(string)) { DefaultValue = "" });

        RefreshGrid();
    }

    private void DeleteColumn_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedTemplate == null || _dataTable.Columns.Count == 0) return;

        var colNames = _dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
        var selected = CsvGrid.CurrentColumn?.Header?.ToString();

        // 選択中の列がない場合、最後の列を選択
        if (string.IsNullOrEmpty(selected))
        {
            MessageBox.Show("削除する列のセルを選択してください。", "TaskAzure",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"列 '{selected}' を削除しますか？", "列の削除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        CsvGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _dataTable.Columns.Remove(selected);
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        _updatingGrid = true;
        try
        {
            CsvGrid.ItemsSource = null;
            CsvGrid.Columns.Clear();
            CsvGrid.ItemsSource = _dataTable.DefaultView;
        }
        finally
        {
            _updatingGrid = false;
        }
    }

    // ─── ボタン ─────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        CommitGridToTemplate();
        _vm.Save();
        MessageBox.Show("保存しました。", "TaskAzure", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        CommitGridToTemplate();
        base.OnClosing(e);
    }
}
