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

    public void Configure(string organizationUrl, string project, string pat)
    {
        _client?.Dispose();
        _orgUrl = organizationUrl.TrimEnd('/');
        _project = project;

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
        var fields = "System.Id,System.Title,System.WorkItemType,System.State";
        var url = $"{_orgUrl}/_apis/wit/workitems?ids={idList}&fields={fields}&api-version=7.1";

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
                Title = fields2.TryGetProperty("System.Title", out var t) ? t.GetString() ?? "" : "",
                WorkItemType = fields2.TryGetProperty("System.WorkItemType", out var wt) ? wt.GetString() ?? "" : "",
                State = fields2.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "",
                WebUrl = $"{_orgUrl}/{Uri.EscapeDataString(_project)}/_workitems/edit/{itemId}",
            });
        }
        return result;
    }

    public void Dispose() => _client?.Dispose();
}
