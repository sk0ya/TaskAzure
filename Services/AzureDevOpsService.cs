using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TaskAzure.Models;

namespace TaskAzure.Services;

public class AzureDevOpsService : IDisposable
{
    private HttpClient? _client;
    private string _orgUrl = string.Empty;
    private string _project = string.Empty;
    private string? _currentUserId;

    public void Configure(string organizationUrl, string project, string pat)
    {
        _client?.Dispose();
        _orgUrl = organizationUrl.TrimEnd('/');
        _project = project;
        _currentUserId = null;

        _client = new HttpClient();
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    public bool IsConfigured => _client != null && !string.IsNullOrEmpty(_orgUrl);

    public async Task<List<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default)
    {
        if (_client == null) throw new InvalidOperationException("サービスが設定されていません。");

        var wiqlBody = JsonSerializer.Serialize(new
        {
            query = "SELECT [System.Id] FROM workitems WHERE [System.AssignedTo] = @Me " +
                    "AND [System.State] NOT IN ('Closed','Done','Resolved','Removed') " +
                    "ORDER BY [System.ChangedDate] DESC"
        });

        var encodedProject = Uri.EscapeDataString(_project);
        var wiqlUrl = $"{_orgUrl}/{encodedProject}/_apis/wit/wiql?api-version=7.1";
        var content = new StringContent(wiqlBody, Encoding.UTF8, "application/json");

        var wiqlResponse = await _client.PostAsync(wiqlUrl, content, ct);
        wiqlResponse.EnsureSuccessStatusCode();

        var wiqlJson = await wiqlResponse.Content.ReadAsStringAsync(ct);
        using var wiqlDoc = JsonDocument.Parse(wiqlJson);

        var ids = new List<int>();
        foreach (var item in wiqlDoc.RootElement.GetProperty("workItems").EnumerateArray())
        {
            ids.Add(item.GetProperty("id").GetInt32());
            if (ids.Count >= 200) break;
        }

        if (ids.Count == 0) return [];
        return await GetWorkItemDetailsAsync(ids, ct);
    }

    private async Task<List<WorkItem>> GetWorkItemDetailsAsync(List<int> ids, CancellationToken ct)
    {
        var idList = string.Join(",", ids);
        var url = $"{_orgUrl}/_apis/wit/workitems?ids={idList}&api-version=7.1";

        var response = await _client!.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var result = new List<WorkItem>();
        foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var itemId = item.GetProperty("id").GetInt32();
            var fields2 = item.GetProperty("fields");

            result.Add(new WorkItem
            {
                Id = itemId,
                Title = GetFieldText(fields2, "System.Title"),
                WorkItemType = GetFieldText(fields2, "System.WorkItemType"),
                State = GetFieldText(fields2, "System.State"),
                AssignedTo = GetFieldText(fields2, "System.AssignedTo"),
                AreaPath = GetFieldText(fields2, "System.AreaPath"),
                IterationPath = GetFieldText(fields2, "System.IterationPath"),
                DevelopProcess = GetFieldText(fields2,
                    "Custom.DevelopProcess",
                    "Custom.DevelopProsess",
                    "Microsoft.VSTS.Common.DevelopProcess",
                    "Microsoft.VSTS.Common.DevelopProsess"),
                WebUrl = $"{_orgUrl}/{Uri.EscapeDataString(_project)}/_workitems/edit/{itemId}",
            });
        }
        return result;
    }

    private static string GetFieldText(JsonElement fields, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (!fields.TryGetProperty(fieldName, out var value)) continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? "";

            if (value.ValueKind == JsonValueKind.Object)
            {
                if (value.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String)
                    return dn.GetString() ?? "";
                if (value.TryGetProperty("uniqueName", out var un) && un.ValueKind == JsonValueKind.String)
                    return un.GetString() ?? "";
            }

            return value.ToString();
        }

        return "";
    }

    public async Task<List<Models.PullRequest>> GetMyPullRequestsAsync(
        IReadOnlyList<Models.PrTarget> targets, CancellationToken ct = default)
    {
        if (_client == null) throw new InvalidOperationException("サービスが設定されていません。");
        if (targets.Count == 0) return [];

        var userId = await GetCurrentUserIdAsync(ct);

        var tasks = targets.Select(t => FetchPrsForTargetAsync(t, userId, ct));
        var results = await Task.WhenAll(tasks);
        return [.. results.SelectMany(r => r)];
    }

    private async Task<List<Models.PullRequest>> FetchPrsForTargetAsync(
        Models.PrTarget target, string userId, CancellationToken ct)
    {
        var encodedProject = Uri.EscapeDataString(target.Project);
        var encodedRepo    = Uri.EscapeDataString(target.Repository);
        var url = $"{_orgUrl}/{encodedProject}/_apis/git/repositories/{encodedRepo}/pullrequests" +
                  $"?searchCriteria.status=active&searchCriteria.creatorId={userId}" +
                  $"&$expand=workItemRefs&api-version=7.1";

        var response = await _client!.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return [];   // リポジトリ名誤りなどは無視

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var result = new List<Models.PullRequest>();
        foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var prId  = item.GetProperty("pullRequestId").GetInt32();
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";

            var linkedIds = new List<int>();
            if (item.TryGetProperty("workItemRefs", out var refs))
            {
                foreach (var r in refs.EnumerateArray())
                {
                    if (r.TryGetProperty("id", out var idProp)
                        && int.TryParse(idProp.GetString(), out var wid))
                        linkedIds.Add(wid);
                }
            }

            result.Add(new Models.PullRequest
            {
                Id                 = prId,
                Title              = title,
                RepositoryName     = target.Repository,
                WebUrl             = $"{_orgUrl}/{encodedProject}/_git/{encodedRepo}/pullrequest/{prId}",
                LinkedWorkItemIds  = linkedIds,
            });
        }
        return result;
    }

    private async Task<string> GetCurrentUserIdAsync(CancellationToken ct)
    {
        if (_currentUserId != null) return _currentUserId;

        var url = $"{_orgUrl}/_apis/connectionData";
        var response = await _client!.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        _currentUserId = doc.RootElement
            .GetProperty("authenticatedUser")
            .GetProperty("id")
            .GetString() ?? "";
        return _currentUserId;
    }

    /// <summary>組織のユーザー一覧を取得する (vssps API)</summary>
    public async Task<List<Models.AdoUser>> GetUsersAsync(CancellationToken ct = default)
    {
        if (_client == null) throw new InvalidOperationException("サービスが設定されていません。");

        // orgUrl から組織名を取得
        var uri = new Uri(_orgUrl);
        string orgName;
        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            orgName = uri.PathAndQuery.TrimStart('/').Split('/')[0];
        else
            orgName = uri.Host.Split('.')[0]; // myorg.visualstudio.com 形式

        var url = $"https://vssps.dev.azure.com/{orgName}/_apis/graph/users?api-version=7.1-preview.1";
        var response = await _client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var result = new List<Models.AdoUser>();
        foreach (var user in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            var displayName = user.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
            var mailAddress = user.TryGetProperty("mailAddress", out var ma) ? ma.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(mailAddress)) continue;
            // サービスアカウント等を除外
            if (mailAddress.Contains("@ado", StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new Models.AdoUser { DisplayName = displayName, UniqueName = mailAddress });
        }
        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture));
        return result;
    }

    public void Dispose() => _client?.Dispose();
}
