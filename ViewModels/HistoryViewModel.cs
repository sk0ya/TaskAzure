using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskAzure.Services;

namespace TaskAzure.ViewModels;

public class HistoryViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public const string AllOption = "すべて";

    private readonly HistoryService _history;
    private readonly AzureDevOpsService _ado;

    private List<HistoryItemViewModel> _all = [];
    private ObservableCollection<HistoryItemViewModel> _items = [];
    private ObservableCollection<string> _typeOptions = [AllOption];
    private ObservableCollection<string> _stateOptions = [AllOption];
    private string _filterText = "";
    private string _selectedType = AllOption;
    private string _selectedState = AllOption;
    private string _statusMessage = "";

    public ObservableCollection<HistoryItemViewModel> Items
    {
        get => _items;
        private set { _items = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> TypeOptions
    {
        get => _typeOptions;
        private set { _typeOptions = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> StateOptions
    {
        get => _stateOptions;
        private set { _stateOptions = value; OnPropertyChanged(); }
    }

    public string FilterText
    {
        get => _filterText;
        set { _filterText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string SelectedType
    {
        get => _selectedType;
        set { _selectedType = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string SelectedState
    {
        get => _selectedState;
        set { _selectedState = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPropertyChanged(); }
    }

    public HistoryViewModel(HistoryService history, AzureDevOpsService ado)
    {
        _history = history;
        _ado = ado;
    }

    public async Task InitializeAsync()
    {
        LoadLocal();

        // サーバーから最新の状態を取得して履歴を更新する (失敗しても履歴表示は継続)
        if (!_ado.IsConfigured || _all.Count == 0) return;
        try
        {
            StatusMessage = "最新状態を取得中...";
            await RefreshWorkItemsAsync();
            await RefreshPullRequestsAsync();
            LoadLocal();
        }
        catch
        {
            ApplyFilter(); // 件数表示に戻す
        }
    }

    private async Task RefreshWorkItemsAsync()
    {
        var ids = _all.Where(v => v.Kind == Models.HistoryKind.WorkItem)
            .Select(v => v.Id).ToList();
        if (ids.Count == 0) return;

        var latest = await _ado.GetWorkItemsByIdsAsync(ids);
        _history.UpdateWorkItemDetails(latest);
    }

    private async Task RefreshPullRequestsAsync()
    {
        // Active なままの PR のみ状態確認 (Completed/Abandoned は変化しない)
        var targets = _all
            .Where(v => v.Kind == Models.HistoryKind.PullRequest && v.State == "Active"
                        && !string.IsNullOrWhiteSpace(v.Project) && !string.IsNullOrWhiteSpace(v.RepositoryName))
            .ToList();
        if (targets.Count == 0) return;

        var idToState = new Dictionary<int, string>();
        foreach (var pr in targets)
        {
            var state = await _ado.GetPullRequestStateAsync(pr.Project, pr.RepositoryName, pr.Id);
            if (!string.IsNullOrEmpty(state)) idToState[pr.Id] = state;
        }
        _history.UpdatePullRequestStates(idToState);
    }

    public void Remove(HistoryItemViewModel vm)
    {
        _history.Remove(vm.Kind, vm.Id);
        _all.Remove(vm);
        RebuildOptions();
        ApplyFilter();
    }

    private void LoadLocal()
    {
        _all = _history.Load()
            .OrderByDescending(e => e.LastSeen)
            .ThenByDescending(e => e.Id)
            .Select(e => new HistoryItemViewModel(e))
            .ToList();
        RebuildOptions();
        ApplyFilter();
    }

    private void RebuildOptions()
    {
        var types = _all.Select(v => v.TypeName)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct().Order().ToList();
        var states = _all.Select(v => v.State)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct().Order().ToList();

        TypeOptions = new ObservableCollection<string>([AllOption, .. types]);
        StateOptions = new ObservableCollection<string>([AllOption, .. states]);

        // 選択中の値が消えた場合は「すべて」に戻す
        if (!TypeOptions.Contains(_selectedType))
        {
            _selectedType = AllOption;
            OnPropertyChanged(nameof(SelectedType));
        }
        if (!StateOptions.Contains(_selectedState))
        {
            _selectedState = AllOption;
            OnPropertyChanged(nameof(SelectedState));
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<HistoryItemViewModel> filtered = _all;

        if (_selectedType != AllOption)
            filtered = filtered.Where(v => v.TypeName == _selectedType);

        if (_selectedState != AllOption)
            filtered = filtered.Where(v => v.State == _selectedState);

        var text = _filterText.Trim();
        if (text.Length > 0)
        {
            filtered = filtered.Where(v =>
                v.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || v.Id.ToString().Contains(text, StringComparison.Ordinal));
        }

        var list = filtered.ToList();
        Items = new ObservableCollection<HistoryItemViewModel>(list);
        StatusMessage = list.Count == _all.Count
            ? $"{_all.Count} 件"
            : $"{list.Count} / {_all.Count} 件";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
