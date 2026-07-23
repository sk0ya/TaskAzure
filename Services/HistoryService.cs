using System.IO;
using System.Text.Json;
using TaskAzure.Models;

namespace TaskAzure.Services;

/// <summary>一度でも自分に割り当てられた WorkItem を history.json に記録する</summary>
public class HistoryService
{
    private static readonly string HistoryDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskAzure");

    private static readonly string HistoryPath = Path.Combine(HistoryDir, "history.json");

    private readonly object _lock = new();

    public List<WorkItemHistoryEntry> Load()
    {
        lock (_lock)
        {
            return LoadUnlocked();
        }
    }

    /// <summary>現在自分に割り当てられている WorkItem を履歴にマージする</summary>
    public void Record(IReadOnlyList<WorkItem> items)
    {
        if (items.Count == 0) return;

        lock (_lock)
        {
            var entries = LoadUnlocked();
            var byId = entries.ToDictionary(e => e.Id);
            var now = DateTime.Now;

            foreach (var item in items)
            {
                if (byId.TryGetValue(item.Id, out var entry))
                {
                    entry.UpdateFrom(item);
                    entry.LastSeen = now;
                }
                else
                {
                    entries.Add(WorkItemHistoryEntry.FromWorkItem(item, now));
                }
            }

            Save(entries);
        }
    }

    /// <summary>サーバーの最新情報で既存エントリのフィールドのみ更新する (LastSeen は変更しない)</summary>
    public void UpdateDetails(IReadOnlyList<WorkItem> items)
    {
        if (items.Count == 0) return;

        lock (_lock)
        {
            var entries = LoadUnlocked();
            var byId = entries.ToDictionary(e => e.Id);

            foreach (var item in items)
            {
                if (byId.TryGetValue(item.Id, out var entry))
                    entry.UpdateFrom(item);
            }

            Save(entries);
        }
    }

    public void Remove(int id)
    {
        lock (_lock)
        {
            var entries = LoadUnlocked();
            if (entries.RemoveAll(e => e.Id == id) > 0)
                Save(entries);
        }
    }

    private static List<WorkItemHistoryEntry> LoadUnlocked()
    {
        try
        {
            if (!File.Exists(HistoryPath))
                return [];
            var json = File.ReadAllText(HistoryPath);
            return JsonSerializer.Deserialize<List<WorkItemHistoryEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void Save(List<WorkItemHistoryEntry> entries)
    {
        Directory.CreateDirectory(HistoryDir);
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(HistoryPath, json);
    }
}
