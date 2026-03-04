using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskAzure.Models;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace TaskAzure.Windows;

public partial class TemplateManagerWindow : Window
{
    private const int BaseGridRows = 15;
    private const int BaseGridColumns = 15;
    private const int AutoSaveDelayMs = 400;

    private readonly TemplateManagerViewModel _vm;
    private readonly DispatcherTimer _autoSaveTimer;
    private Template? _editingTemplate;
    private DataTable _dataTable = new();
    private bool _updatingGrid;
    private int _nextColumnId = 1;

    public TemplateManagerWindow(TemplateManagerViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoSaveDelayMs),
        };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SelectedTemplate))
            {
                FlushPendingAutoSave();
                LoadSelectedTemplate();
            }
        };

        TemplateNameBox.TextChanged += TemplateNameBox_TextChanged;
        LoadSelectedTemplate();
    }

    private void LoadSelectedTemplate()
    {
        _updatingGrid = true;
        try
        {
            DetachTableEvents();
            CsvGrid.ItemsSource = null;
            CsvGrid.Columns.Clear();
            _editingTemplate = _vm.SelectedTemplate;
            _nextColumnId = 1;

            if (_editingTemplate == null)
            {
                _dataTable = CreateEditorTable(BaseGridColumns, BaseGridRows);
                ConfigureGridColumns();
                CsvGrid.ItemsSource = _dataTable.DefaultView;
                AttachTableEvents();
                return;
            }

            var parsed = CsvHelper.ParseToDataTable(_editingTemplate.CsvContent);
            _dataTable = BuildEditorTable(parsed);
            ConfigureGridColumns();
            CsvGrid.ItemsSource = _dataTable.DefaultView;
            AttachTableEvents();
        }
        finally
        {
            _updatingGrid = false;
        }
    }

    private DataTable BuildEditorTable(DataTable parsed)
    {
        var usedColumnCount = parsed.Columns.Count;
        var usedRowCount = usedColumnCount == 0 && parsed.Rows.Count == 0 ? 0 : parsed.Rows.Count + 1;

        var columnCount = Math.Max(BaseGridColumns, usedColumnCount);
        var rowCount = Math.Max(BaseGridRows, usedRowCount);

        var editorTable = CreateEditorTable(columnCount, rowCount);

        if (usedColumnCount > 0)
        {
            for (int c = 0; c < usedColumnCount; c++)
                editorTable.Rows[0][c] = parsed.Columns[c].ColumnName;

            for (int r = 0; r < parsed.Rows.Count; r++)
            {
                for (int c = 0; c < usedColumnCount; c++)
                    editorTable.Rows[r + 1][c] = parsed.Rows[r][c]?.ToString() ?? "";
            }
        }

        _nextColumnId = editorTable.Columns.Count + 1;
        return editorTable;
    }

    private DataTable CreateEditorTable(int columnCount, int rowCount)
    {
        var table = new DataTable();
        for (int c = 0; c < columnCount; c++)
            table.Columns.Add(CreateInternalColumnName(), typeof(string));

        for (int r = 0; r < rowCount; r++)
        {
            var row = table.NewRow();
            for (int c = 0; c < columnCount; c++) row[c] = "";
            table.Rows.Add(row);
        }

        return table;
    }

    private string CreateInternalColumnName() => $"C{_nextColumnId++}";

    private void ConfigureGridColumns()
    {
        CsvGrid.Columns.Clear();

        for (int i = 0; i < _dataTable.Columns.Count; i++)
        {
            var columnName = _dataTable.Columns[i].ColumnName;
            var column = new DataGridTextColumn
            {
                Header = ToExcelColumnName(i),
                Binding = new System.Windows.Data.Binding($"[{columnName}]")
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 90,
            };
            CsvGrid.Columns.Add(column);
        }
    }

    private static string ToExcelColumnName(int zeroBasedIndex)
    {
        var n = zeroBasedIndex + 1;
        var name = "";
        while (n > 0)
        {
            n--;
            name = (char)('A' + (n % 26)) + name;
            n /= 26;
        }
        return name;
    }

    private void AttachTableEvents() => _dataTable.ColumnChanged += DataTable_ColumnChanged;

    private void DetachTableEvents() => _dataTable.ColumnChanged -= DataTable_ColumnChanged;

    private void DataTable_ColumnChanged(object? sender, DataColumnChangeEventArgs e)
    {
        if (_updatingGrid) return;
        ScheduleAutoSave();
    }

    private void TemplateNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingGrid || _editingTemplate == null) return;
        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        if (_editingTemplate == null || _updatingGrid) return;
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        _autoSaveTimer.Stop();
        SaveCurrentTemplate();
    }

    private void FlushPendingAutoSave()
    {
        _autoSaveTimer.Stop();
        SaveCurrentTemplate();
    }

    private void SaveCurrentTemplate()
    {
        if (_editingTemplate == null || _updatingGrid) return;

        CommitGridEdits();
        _editingTemplate.CsvContent = SerializeEditorTableToCsv();
        _vm.Save();
    }

    private void CommitGridEdits()
    {
        CsvGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        CsvGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private string SerializeEditorTableToCsv()
    {
        if (_dataTable.Columns.Count == 0 || _dataTable.Rows.Count == 0) return "";

        var (lastUsedRow, lastUsedCol) = FindLastUsedRange();
        if (lastUsedRow < 0 || lastUsedCol < 0) return "";

        var sb = new StringBuilder();
        for (int r = 0; r <= lastUsedRow; r++)
        {
            var values = new string[lastUsedCol + 1];
            for (int c = 0; c <= lastUsedCol; c++)
                values[c] = CsvHelper.QuoteCsv(GetCellText(r, c));

            sb.Append(string.Join(",", values));
            if (r < lastUsedRow) sb.AppendLine();
        }

        return sb.ToString();
    }

    private (int lastUsedRow, int lastUsedCol) FindLastUsedRange()
    {
        var lastRow = -1;
        var lastCol = -1;

        for (int r = 0; r < _dataTable.Rows.Count; r++)
        {
            for (int c = 0; c < _dataTable.Columns.Count; c++)
            {
                if (string.IsNullOrWhiteSpace(GetCellText(r, c))) continue;
                if (r > lastRow) lastRow = r;
                if (c > lastCol) lastCol = c;
            }
        }

        return (lastRow, lastCol);
    }

    private string GetCellText(int rowIndex, int colIndex)
    {
        var value = _dataTable.Rows[rowIndex][colIndex];
        if (value == DBNull.Value) return "";
        return value?.ToString() ?? "";
    }

    private void CsvGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        => e.Row.Header = (e.Row.GetIndex() + 1).ToString();

    private void CsvGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_updatingGrid) return;
        Dispatcher.BeginInvoke(ScheduleAutoSave, DispatcherPriority.Background);
    }

    private void CsvGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editingTemplate == null) return;

        var source = e.OriginalSource as DependencyObject;

        var rowHeader = FindAncestor<DataGridRowHeader>(source);
        if (rowHeader?.DataContext is DataRowView rowView)
        {
            var rowIndex = _dataTable.Rows.IndexOf(rowView.Row);
            if (rowIndex >= 0)
            {
                OpenContextMenu(BuildRowHeaderContextMenu(rowIndex), e);
                return;
            }
        }

        var colHeader = FindAncestor<DataGridColumnHeader>(source);
        if (colHeader?.Column != null)
        {
            OpenContextMenu(BuildColumnHeaderContextMenu(colHeader.Column.DisplayIndex), e);
            return;
        }

        CsvGrid.ContextMenu = null;
    }

    private void OpenContextMenu(ContextMenu menu, MouseButtonEventArgs e)
    {
        CsvGrid.ContextMenu = menu;
        menu.PlacementTarget = CsvGrid;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu BuildRowHeaderContextMenu(int rowIndex)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("上に行を挿入", () => InsertRowAt(rowIndex)));
        menu.Items.Add(CreateMenuItem("下に行を挿入", () => InsertRowAt(rowIndex + 1)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("この行を削除", () => DeleteRowAt(rowIndex)));
        return menu;
    }

    private ContextMenu BuildColumnHeaderContextMenu(int colIndex)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("左に列を挿入", () => InsertColumnAt(colIndex)));
        menu.Items.Add(CreateMenuItem("右に列を挿入", () => InsertColumnAt(colIndex + 1)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("この列を削除", () => DeleteColumnAt(colIndex)));
        return menu;
    }

    private static MenuItem CreateMenuItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target) return target;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void CsvGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        PasteClipboard();
        e.Handled = true;
    }

    private void PasteClipboard()
    {
        if (_editingTemplate == null) return;

        var text = System.Windows.Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;

        var clipboardRows = ParseClipboardRows(text);
        if (clipboardRows.Count == 0) return;

        CommitGridEdits();
        var startRow = GetCurrentRowIndex();
        var startCol = CsvGrid.CurrentCell.Column?.DisplayIndex ?? 0;
        if (startRow < 0) startRow = 0;
        if (startCol < 0) startCol = 0;

        var requiredRows = startRow + clipboardRows.Count;
        var requiredCols = startCol + clipboardRows.Max(r => r.Count);
        EnsureGridCapacity(requiredRows, requiredCols);

        _updatingGrid = true;
        try
        {
            for (int r = 0; r < clipboardRows.Count; r++)
            {
                var row = clipboardRows[r];
                for (int c = 0; c < row.Count; c++)
                    _dataTable.Rows[startRow + r][startCol + c] = row[c];
            }
        }
        finally
        {
            _updatingGrid = false;
        }

        ScheduleAutoSave();
    }

    private static List<List<string>> ParseClipboardRows(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        var rows = new List<List<string>>(lines.Count);
        foreach (var line in lines)
            rows.Add(line.Split('\t').ToList());

        return rows;
    }

    private int GetCurrentRowIndex()
    {
        if (CsvGrid.CurrentItem is DataRowView currentView)
            return _dataTable.Rows.IndexOf(currentView.Row);

        if (CsvGrid.SelectedCells.Count > 0 &&
            CsvGrid.SelectedCells[0].Item is DataRowView selectedView)
            return _dataTable.Rows.IndexOf(selectedView.Row);

        return 0;
    }

    private void EnsureGridCapacity(int requiredRows, int requiredCols)
    {
        if (requiredCols > _dataTable.Columns.Count)
        {
            for (int c = _dataTable.Columns.Count; c < requiredCols; c++)
            {
                var column = new DataColumn(CreateInternalColumnName(), typeof(string)) { DefaultValue = "" };
                _dataTable.Columns.Add(column);
            }
            ConfigureGridColumns();
        }

        while (_dataTable.Rows.Count < requiredRows)
        {
            var row = _dataTable.NewRow();
            for (int c = 0; c < _dataTable.Columns.Count; c++) row[c] = "";
            _dataTable.Rows.Add(row);
        }
    }

    private void InsertRowAt(int rowIndex)
    {
        if (_editingTemplate == null) return;

        CommitGridEdits();
        var row = _dataTable.NewRow();
        for (int c = 0; c < _dataTable.Columns.Count; c++) row[c] = "";

        rowIndex = Math.Clamp(rowIndex, 0, _dataTable.Rows.Count);
        _dataTable.Rows.InsertAt(row, rowIndex);
        CsvGrid.Items.Refresh();
        ScheduleAutoSave();
    }

    private void DeleteRowAt(int rowIndex)
    {
        if (_editingTemplate == null) return;

        if (_dataTable.Rows.Count <= BaseGridRows)
        {
            MessageBox.Show($"行数は最低 {BaseGridRows} 行です。", "TaskAzure",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (rowIndex < 0 || rowIndex >= _dataTable.Rows.Count) return;

        CommitGridEdits();
        _dataTable.Rows.RemoveAt(rowIndex);
        CsvGrid.Items.Refresh();
        ScheduleAutoSave();
    }

    private void InsertColumnAt(int colIndex)
    {
        if (_editingTemplate == null) return;

        CommitGridEdits();
        colIndex = Math.Clamp(colIndex, 0, _dataTable.Columns.Count);

        var column = new DataColumn(CreateInternalColumnName(), typeof(string)) { DefaultValue = "" };
        _dataTable.Columns.Add(column);
        column.SetOrdinal(colIndex);

        foreach (DataRow row in _dataTable.Rows)
        {
            if (row.IsNull(column))
                row[column] = "";
        }

        ConfigureGridColumns();
        CsvGrid.Items.Refresh();
        ScheduleAutoSave();
    }

    private void DeleteColumnAt(int colIndex)
    {
        if (_editingTemplate == null) return;

        if (_dataTable.Columns.Count <= BaseGridColumns)
        {
            MessageBox.Show($"列数は最低 {BaseGridColumns} 列です。", "TaskAzure",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (colIndex < 0 || colIndex >= _dataTable.Columns.Count) return;

        CommitGridEdits();
        _dataTable.Columns.RemoveAt(colIndex);
        ConfigureGridColumns();
        CsvGrid.Items.Refresh();
        ScheduleAutoSave();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        FlushPendingAutoSave();
        base.OnClosing(e);
    }
}
