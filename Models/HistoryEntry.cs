namespace TaskAzure.Models;

public enum HistoryKind
{
    WorkItem,
    PullRequest,
}

/// <summary>一度でも自分に関わった WorkItem / PR の履歴エントリ (history.json に永続化)</summary>
public class HistoryEntry
{
    /// <summary>WorkItem か PullRequest か。既存 JSON には無いフィールドなので既定は WorkItem</summary>
    public HistoryKind Kind { get; set; }

    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    // WorkItem 用フィールド
    public string WorkItemType { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string AreaPath { get; set; } = string.Empty;
    public string IterationPath { get; set; } = string.Empty;

    // PR 用フィールド (状態の再取得に使用)
    public string Project { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>WorkItem の State、または PR の status (Active/Completed/Abandoned)</summary>
    public string State { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;

    /// <summary>最初に確認した日時</summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>最後に確認した日時</summary>
    public DateTime LastSeen { get; set; }

    public static HistoryEntry FromWorkItem(WorkItem item, DateTime now) => new()
    {
        Kind = HistoryKind.WorkItem,
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

    public void UpdateFromWorkItem(WorkItem item)
    {
        Title = item.Title;
        WorkItemType = item.WorkItemType;
        State = item.State;
        AssignedTo = item.AssignedTo;
        AreaPath = item.AreaPath;
        IterationPath = item.IterationPath;
        WebUrl = item.WebUrl;
    }

    public static HistoryEntry FromPullRequest(PullRequest pr, string project, DateTime now) => new()
    {
        Kind = HistoryKind.PullRequest,
        Id = pr.Id,
        Title = pr.Title,
        State = "Active",
        Project = project,
        RepositoryName = pr.RepositoryName,
        WebUrl = pr.WebUrl,
        FirstSeen = now,
        LastSeen = now,
    };

    public void UpdateFromPullRequest(PullRequest pr, string project)
    {
        Title = pr.Title;
        RepositoryName = pr.RepositoryName;
        WebUrl = pr.WebUrl;
        if (!string.IsNullOrEmpty(project)) Project = project;
    }
}
