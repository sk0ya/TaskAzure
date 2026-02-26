namespace TaskAzure.Models;

public class PullRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
}
