using TaskAzure.Models;

namespace TaskAzure.ViewModels;

public class PullRequestViewModel(PullRequest pr)
{
    public int Id => pr.Id;
    public string Title => pr.Title;
    public string RepositoryName => pr.RepositoryName;
    public string WebUrl => pr.WebUrl;

    public string IdDisplay => $"PR#{pr.Id}";
    public string MarkdownLink => $"[PR#{pr.Id}: {pr.Title}]({pr.WebUrl})";
    public string HtmlLink     => $"<a href=\"{pr.WebUrl}\">PR#{pr.Id}</a>: {pr.Title}";
}
