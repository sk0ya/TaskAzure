using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskAzure.Models;
using TaskAzure.Services;

namespace TaskAzure.ViewModels;

public class HistoryViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public const string AllOption = "すべて";

    private readonly HistoryService _history;
    private readonly AzureDevOpsService _ado;

    private List<HistoryItemViewModel> _all = [];
    private Dictionary<int, HistoryItemViewModel> _byWorkItemId = [];
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
        var ids = _all.Where(v => v.Kind == HistoryKind.WorkItem)
            .Select(v => v.Id).ToList();
        if (ids.Count == 0) return;

        var latest = await _ado.GetWorkItemsByIdsAsync(ids);
        _history.UpdateWorkItemDetails(latest);
    }

    private async Task RefreshPullRequestsAsync()
    {
        // Active なままの PR のみ状態確認 (Completed/Abandoned は変化しない)
        var targets = _all
            .Where(v => v.Kind == HistoryKind.PullRequest && v.State == "Active"
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

    /// <summary>vm の親エントリを履歴内から返す。WorkItem は System.Parent、PR はリンク先 WorkItem</summary>
    private HistoryItemViewModel? GetParent(HistoryItemViewModel vm)
    {
        if (vm.Kind == HistoryKind.WorkItem)
        {
            if (vm.ParentId != 0 && _byWorkItemId.TryGetValue(vm.ParentId, out var p) && !ReferenceEquals(p, vm))
                return p;
            return null;
        }

        // PR: 履歴に存在する最初のリンク先 WorkItem を親とする
        foreach (var wid in vm.LinkedWorkItemIds)
            if (_byWorkItemId.TryGetValue(wid, out var p))
                return p;
        return null;
    }

    private void ApplyFilter()
    {
        _byWorkItemId = _all
            .Where(v => v.Kind == HistoryKind.WorkItem)
            .GroupBy(v => v.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var text = _filterText.Trim();
        bool Matches(HistoryItemViewModel v) =>
            (_selectedType == AllOption || v.TypeName == _selectedType)
            && (_selectedState == AllOption || v.State == _selectedState)
            && (text.Length == 0
                || v.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || v.Id.ToString().Contains(text, StringComparison.Ordinal));

        var matched = _all.Where(Matches).ToList();

        // 該当項目 + その祖先を表示対象に (親子構造を保つため祖先も含める)
        var visible = new HashSet<HistoryItemViewModel>();
        foreach (var m in matched)
        {
            var cur = m;
            var guard = 0;
            while (cur != null && visible.Add(cur) && guard++ < 100)
                cur = GetParent(cur);
        }

        // 表示対象内で親→子のマップを構築 (_all の順序を保持)
        var childrenOf = new Dictionary<HistoryItemViewModel, List<HistoryItemViewModel>>();
        var roots = new List<HistoryItemViewModel>();
        foreach (var v in _all)
        {
            if (!visible.Contains(v)) continue;
            var parent = GetParent(v);
            if (parent != null && visible.Contains(parent))
            {
                if (!childrenOf.TryGetValue(parent, out var list))
                    childrenOf[parent] = list = [];
                list.Add(v);
            }
            else
            {
                roots.Add(v);
            }
        }

        // 前順走査でフラット化し、深さを付与
        var flat = new List<HistoryItemViewModel>();
        var seen = new HashSet<HistoryItemViewModel>();
        void Walk(HistoryItemViewModel v, int depth)
        {
            if (!seen.Add(v)) return;
            v.Depth = depth;
            flat.Add(v);
            if (childrenOf.TryGetValue(v, out var kids))
                foreach (var k in kids) Walk(k, depth + 1);
        }
        foreach (var r in roots) Walk(r, 0);

        Items = new ObservableCollection<HistoryItemViewModel>(flat);
        StatusMessage = matched.Count == _all.Count
            ? $"{_all.Count} 件"
            : $"該当 {matched.Count} / 全 {_all.Count} 件";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
