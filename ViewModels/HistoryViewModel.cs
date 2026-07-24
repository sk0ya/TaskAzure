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

    private List<HistoryItemViewModel> _persisted = [];   // 履歴に保存された自分の項目
    private List<HistoryItemViewModel> _context = [];     // 親子表示のため取得した履歴外の親
    private List<HistoryItemViewModel> _all = [];         // _persisted + _context
    private Dictionary<int, HistoryItemViewModel> _byWorkItemId = [];
    private ObservableCollection<HistoryItemViewModel> _items = [];

    // 折りたたみ中ノードのキー ("WorkItem:123") — ディスクに永続化
    private HashSet<string> _collapsed = [];
    private HashSet<string> _collapsibleKeys = [];

    private static string KeyOf(HistoryItemViewModel v) => $"{v.Kind}:{v.Id}";
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
        _collapsed = _history.LoadCollapsedKeys();
        LoadLocal();

        // サーバーから最新の状態を取得して履歴を更新する (失敗しても履歴表示は継続)
        if (!_ado.IsConfigured || _persisted.Count == 0) return;
        try
        {
            StatusMessage = "最新状態を取得中...";
            await RefreshWorkItemsAsync();
            await RefreshPullRequestsAsync();
            LoadLocal();                 // 更新した親ID/状態で再読込
            await FetchAncestorsAsync(); // 履歴外の親を取得して親子表示を補完
        }
        catch
        {
            ApplyFilter(); // 件数表示に戻す
        }
    }

    private async Task RefreshWorkItemsAsync()
    {
        var ids = _persisted.Where(v => v.Kind == HistoryKind.WorkItem)
            .Select(v => v.Id).ToList();
        if (ids.Count == 0) return;

        var latest = await _ado.GetWorkItemsByIdsAsync(ids);
        _history.UpdateWorkItemDetails(latest);
    }

    private async Task RefreshPullRequestsAsync()
    {
        // Active なままの PR のみ状態確認 (Completed/Abandoned は変化しない)
        var targets = _persisted
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

    /// <summary>履歴に無い親 WorkItem を遡って取得し、コンテキスト行として表示する</summary>
    private async Task FetchAncestorsAsync()
    {
        var have = new HashSet<int>(
            _persisted.Where(v => v.Kind == HistoryKind.WorkItem).Select(v => v.Id));

        // 起点: 履歴項目が参照する親 (WorkItem の親、PR のリンク先) のうち履歴に無いもの
        var needed = new HashSet<int>();
        foreach (var v in _persisted)
        {
            if (v.Kind == HistoryKind.WorkItem && v.ParentId != 0 && !have.Contains(v.ParentId))
                needed.Add(v.ParentId);
            else if (v.Kind == HistoryKind.PullRequest)
                foreach (var wid in v.LinkedWorkItemIds)
                    if (!have.Contains(wid)) needed.Add(wid);
        }

        var context = new List<HistoryItemViewModel>();
        var fetched = new HashSet<int>();
        var guard = 0;
        while (needed.Count > 0 && guard++ < 20)
        {
            var batch = needed.Where(id => !have.Contains(id) && !fetched.Contains(id)).ToList();
            if (batch.Count == 0) break;

            var items = await _ado.GetWorkItemsByIdsAsync(batch);
            needed = [];
            foreach (var it in items)
            {
                fetched.Add(it.Id);
                var entry = HistoryEntry.FromWorkItem(it, DateTime.MinValue);
                context.Add(new HistoryItemViewModel(entry) { IsContext = true });
                if (it.ParentId != 0 && !have.Contains(it.ParentId) && !fetched.Contains(it.ParentId))
                    needed.Add(it.ParentId); // さらに上の親を辿る
            }
        }

        _context = context;
        Recompose();
    }

    public void Remove(HistoryItemViewModel vm)
    {
        if (vm.IsContext) return; // 履歴外の親は削除対象外
        _history.Remove(vm.Kind, vm.Id);
        _persisted.Remove(vm);
        Recompose();
    }

    /// <summary>ノードの展開/折りたたみを切り替えて永続化する</summary>
    public void ToggleExpand(HistoryItemViewModel vm)
    {
        if (!vm.HasChildren) return;
        var key = KeyOf(vm);
        if (!_collapsed.Remove(key)) _collapsed.Add(key);
        _history.SaveCollapsedKeys(_collapsed);
        ApplyFilter();
    }

    public void ExpandAll()
    {
        if (_collapsed.Count == 0) return;
        _collapsed.Clear();
        _history.SaveCollapsedKeys(_collapsed);
        ApplyFilter();
    }

    public void CollapseAll()
    {
        _collapsed = [.. _collapsibleKeys];
        _history.SaveCollapsedKeys(_collapsed);
        ApplyFilter();
    }

    private void LoadLocal()
    {
        _persisted = _history.Load()
            .OrderByDescending(e => e.LastSeen)
            .ThenByDescending(e => e.Id)
            .Select(e => new HistoryItemViewModel(e))
            .ToList();
        Recompose();
    }

    private void Recompose()
    {
        _all = [.. _persisted, .. _context];
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

        // 折りたたみ可能なノード(子を持つ)のキーを記録
        _collapsibleKeys = childrenOf.Keys.Select(KeyOf).ToHashSet();

        // フィルター中は折りたたみを無視して全展開 (該当が隠れないように)
        var filtering = _selectedType != AllOption || _selectedState != AllOption || text.Length > 0;

        // 前順走査でフラット化し、深さ・展開状態を付与。折りたたみ中は子孫をスキップ
        var flat = new List<HistoryItemViewModel>();
        var seen = new HashSet<HistoryItemViewModel>();
        void Walk(HistoryItemViewModel v, int depth)
        {
            if (!seen.Add(v)) return;
            v.Depth = depth;
            var hasKids = childrenOf.TryGetValue(v, out var kids) && kids.Count > 0;
            v.HasChildren = hasKids;
            var expanded = filtering || !_collapsed.Contains(KeyOf(v));
            v.IsExpanded = expanded;
            flat.Add(v);
            if (hasKids && expanded)
                foreach (var k in kids!) Walk(k, depth + 1);
        }
        foreach (var r in roots) Walk(r, 0);

        Items = new ObservableCollection<HistoryItemViewModel>(flat);
        var myMatched = matched.Count(v => !v.IsContext);
        var ctxNote = _context.Count > 0 ? $" (+親 {_context.Count})" : "";
        StatusMessage = myMatched == _persisted.Count
            ? $"{_persisted.Count} 件{ctxNote}"
            : $"該当 {myMatched} / 全 {_persisted.Count} 件{ctxNote}";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
