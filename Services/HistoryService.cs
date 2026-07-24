using System.IO;
using System.Text.Json;
using TaskAzure.Models;

namespace TaskAzure.Services;

/// <summary>一度でも自分に関わった WorkItem / PR を history.json に記録する。キーは (Kind, Id)</summary>
public class HistoryService
{
    private static readonly string HistoryDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskAzure");

    private static readonly string HistoryPath = Path.Combine(HistoryDir, "history.json");
    private static readonly string CollapsedPath = Path.Combine(HistoryDir, "history_collapsed.json");

    private readonly object _lock = new();

    /// <summary>折りたたみ中のノードキー ("WorkItem:123" 等) を読み込む</summary>
    public HashSet<string> LoadCollapsedKeys()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(CollapsedPath))
                    return [];
                var json = File.ReadAllText(CollapsedPath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list is null ? [] : [.. list];
            }
            catch
            {
                return [];
            }
        }
    }

    public void SaveCollapsedKeys(IEnumerable<string> keys)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(HistoryDir);
                var json = JsonSerializer.Serialize(keys.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CollapsedPath, json);
            }
            catch
            {
                // 保持失敗は無視
            }
        }
    }

    public List<HistoryEntry> Load()
    {
        lock (_lock)
        {
            return LoadUnlocked();
        }
    }

    /// <summary>現在自分に割り当てられている WorkItem を履歴にマージする</summary>
    public void RecordWorkItems(IReadOnlyList<WorkItem> items)
    {
        if (items.Count == 0) return;

        lock (_lock)
        {
            var entries = LoadUnlocked();
            var byKey = BuildIndex(entries);
            var now = DateTime.Now;

            foreach (var item in items)
            {
                if (byKey.TryGetValue((HistoryKind.WorkItem, item.Id), out var entry))
                {
                    entry.UpdateFromWorkItem(item);
                    entry.LastSeen = now;
                }
                else
                {
                    entries.Add(HistoryEntry.FromWorkItem(item, now));
                }
            }

            Save(entries);
        }
    }

    /// <summary>現在自分が作成している PR を履歴にマージする</summary>
    public void RecordPullRequests(IReadOnlyList<PullRequest> prs)
    {
        if (prs.Count == 0) return;

        lock (_lock)
        {
            var entries = LoadUnlocked();
            var byKey = BuildIndex(entries);
            var now = DateTime.Now;

            foreach (var pr in prs)
            {
                if (byKey.TryGetValue((HistoryKind.PullRequest, pr.Id), out var entry))
                {
                    entry.UpdateFromPullRequest(pr, pr.Project);
                    entry.State = "Active"; // アクティブ一覧に居るので Active
                    entry.LastSeen = now;
                }
                else
                {
                    entries.Add(HistoryEntry.FromPullRequest(pr, pr.Project, now));
                }
            }

            Save(entries);
        }
    }

    /// <summary>サーバーの最新情報で既存 WorkItem のフィールドのみ更新する (LastSeen は変更しない)</summary>
    public void UpdateWorkItemDetails(IReadOnlyList<WorkItem> items)
    {
        if (items.Count == 0) return;

        lock (_lock)
        {
            var entries = LoadUnlocked();
            var byKey = BuildIndex(entries);

            foreach (var item in items)
            {
                if (byKey.TryGetValue((HistoryKind.WorkItem, item.Id), out var entry))
                    entry.UpdateFromWorkItem(item);
            }

            Save(entries);
        }
    }

    /// <summary>PR の status を最新化する (id → status)。LastSeen は変更しない</summary>
    public void UpdatePullRequestStates(IReadOnlyDictionary<int, string> idToState)
    {
        if (idToState.Count == 0) return;

        lock (_lock)
        {
            var entries = LoadUnlocked();
            var byKey = BuildIndex(entries);
            var changed = false;

            foreach (var (id, state) in idToState)
            {
                if (byKey.TryGetValue((HistoryKind.PullRequest, id), out var entry) && entry.State != state)
                {
                    entry.State = state;
                    changed = true;
                }
            }

            if (changed) Save(entries);
        }
    }

    public void Remove(HistoryKind kind, int id)
    {
        lock (_lock)
        {
            var entries = LoadUnlocked();
            if (entries.RemoveAll(e => e.Kind == kind && e.Id == id) > 0)
                Save(entries);
        }
    }

    private static Dictionary<(HistoryKind, int), HistoryEntry> BuildIndex(List<HistoryEntry> entries)
    {
        var dict = new Dictionary<(HistoryKind, int), HistoryEntry>();
        foreach (var e in entries)
            dict[(e.Kind, e.Id)] = e; // 重複時は後勝ち
        return dict;
    }

    private static List<HistoryEntry> LoadUnlocked()
    {
        try
        {
            if (!File.Exists(HistoryPath))
                return [];
            var json = File.ReadAllText(HistoryPath);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void Save(List<HistoryEntry> entries)
    {
        Directory.CreateDirectory(HistoryDir);
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(HistoryPath, json);
    }
}
