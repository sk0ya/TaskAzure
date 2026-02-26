using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskAzure.Models;
using TaskAzure.Services;
using Application = System.Windows.Application;

namespace TaskAzure.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly AzureDevOpsService _ado;
    private readonly SettingsService _settings;
    private readonly CredentialService _cred;

    private ObservableCollection<WorkItemViewModel> _workItems = [];
    private ObservableCollection<PullRequestViewModel> _unlinkedPullRequests = [];
    private List<PrTarget> _prTargets = [];
    private bool _isLoading;
    private string _statusMessage = "";
    private string _lastUpdated = "";
    private System.Threading.Timer? _timer;
    private CancellationTokenSource? _cts;

    public ObservableCollection<WorkItemViewModel> WorkItems
    {
        get => _workItems;
        private set { _workItems = value; OnPropertyChanged(); }
    }

    /// <summary>どの WorkItem にもリンクされていない PR (一覧末尾に表示)</summary>
    public ObservableCollection<PullRequestViewModel> UnlinkedPullRequests
    {
        get => _unlinkedPullRequests;
        private set { _unlinkedPullRequests = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        private set { _lastUpdated = value; OnPropertyChanged(); }
    }

    public MainViewModel(AzureDevOpsService ado, SettingsService settings, CredentialService cred)
    {
        _ado = ado;
        _settings = settings;
        _cred = cred;
    }

    public async Task InitializeAsync()
    {
        StopTimer();
        var s = _settings.Load();
        var pat = _cred.GetPat(s.PatEnvVarName)
            ?? throw new InvalidOperationException($"PAT が取得できませんでした。環境変数 {s.PatEnvVarName} または Windows 資格情報マネージャーに PAT を設定してください。");

        _ado.Configure(s.OrganizationUrl, s.Project, pat);
        _prTargets = s.PrTargets;

        await RefreshAsync();

        var interval = TimeSpan.FromMinutes(s.RefreshIntervalMinutes > 0 ? s.RefreshIntervalMinutes : 5);
        _timer = new System.Threading.Timer(_ =>
            Application.Current?.Dispatcher.InvokeAsync(async () => await RefreshAsync()),
            null, interval, interval);
    }

    public async Task RefreshAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsLoading = true;
        StatusMessage = "読み込み中...";
        try
        {
            var itemsTask = _ado.GetMyWorkItemsAsync(ct);
            var prsTask   = _ado.GetMyPullRequestsAsync(_prTargets, ct);
            await Task.WhenAll(itemsTask, prsTask);
            if (ct.IsCancellationRequested) return;

            var items = itemsTask.Result;
            var prs   = prsTask.Result;

            // WorkItem ViewModel を先に作成
            var workItemVms = items.Select(i => new WorkItemViewModel(i)).ToList();

            // PR を紐付けられた WorkItem の下にセット。リンクなし PR は末尾用に収集
            var vmById = workItemVms.ToDictionary(v => v.Id);
            var unlinked = new List<PullRequestViewModel>();
            foreach (var pr in prs)
            {
                var prVm = new PullRequestViewModel(pr);
                var linked = false;
                foreach (var wid in pr.LinkedWorkItemIds)
                {
                    if (vmById.TryGetValue(wid, out var wVm))
                    {
                        wVm.LinkedPullRequests.Add(prVm);
                        linked = true;
                    }
                }
                if (!linked) unlinked.Add(prVm);
            }

            WorkItems = new ObservableCollection<WorkItemViewModel>(workItemVms);
            UnlinkedPullRequests = new ObservableCollection<PullRequestViewModel>(unlinked);

            var totalPr = prs.Count;
            var prPart = totalPr > 0 ? $" / PR {totalPr} 件" : "";
            StatusMessage = $"{items.Count} 件{prPart}";
            LastUpdated = $"更新: {DateTime.Now:HH:mm}";
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            StatusMessage = "エラー";
            LastUpdated = ex.Message.Length > 40 ? ex.Message[..40] + "…" : ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
        _cts?.Cancel();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
