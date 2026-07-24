namespace TaskAzure.Models;

public class WorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string WorkItemType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string AreaPath { get; set; } = string.Empty;
    public string IterationPath { get; set; } = string.Empty;
    public string DevelopProcess { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;

    /// <summary>親 WorkItem の ID (System.Parent)。親なしは 0</summary>
    public int ParentId { get; set; }
}
