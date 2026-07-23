namespace TaskAzure.Models;

/// <summary>一度でも自分に割り当てられた WorkItem の履歴エントリ (history.json に永続化)</summary>
public class WorkItemHistoryEntry
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string WorkItemType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string AreaPath { get; set; } = string.Empty;
    public string IterationPath { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;

    /// <summary>最初に自分への割り当てを確認した日時</summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>最後に自分への割り当てを確認した日時</summary>
    public DateTime LastSeen { get; set; }

    public WorkItem ToWorkItem() => new()
    {
        Id = Id,
        Title = Title,
        WorkItemType = WorkItemType,
        State = State,
        AssignedTo = AssignedTo,
        AreaPath = AreaPath,
        IterationPath = IterationPath,
        WebUrl = WebUrl,
    };

    public static WorkItemHistoryEntry FromWorkItem(WorkItem item, DateTime now) => new()
    {
        Id = item.Id,
        Title = item.Title,
        WorkItemType = item.WorkItemType,
        State = item.State,
        AssignedTo = item.AssignedTo,
        AreaPath = item.AreaPath,
        IterationPath = item.IterationPath,
        WebUrl = item.WebUrl,
        FirstSeen = now,
        LastSeen = now,
    };

    /// <summary>サーバーの最新情報でフィールドを更新する (FirstSeen/LastSeen は保持)</summary>
    public void UpdateFrom(WorkItem item)
    {
        Title = item.Title;
        WorkItemType = item.WorkItemType;
        State = item.State;
        AssignedTo = item.AssignedTo;
        AreaPath = item.AreaPath;
        IterationPath = item.IterationPath;
        WebUrl = item.WebUrl;
    }
}
