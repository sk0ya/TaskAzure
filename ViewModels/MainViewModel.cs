using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private ObservableCollection<PullRequestViewModel> _pullRequests = [];
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

    public ObservableCollection<PullRequestViewModel> PullRequests
    {
        get => _pullRequests;
        private set { _pullRequests = value; OnPropertyChanged(); }
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
            var prsTask   = _ado.GetMyPullRequestsAsync(ct);
            await Task.WhenAll(itemsTask, prsTask);
            if (ct.IsCancellationRequested) return;

            var items = itemsTask.Result;
            var prs   = prsTask.Result;

            WorkItems = new ObservableCollection<WorkItemViewModel>(
                items.Select(i => new WorkItemViewModel(i)));
            PullRequests = new ObservableCollection<PullRequestViewModel>(
                prs.Select(p => new PullRequestViewModel(p)));

            var prPart = prs.Count > 0 ? $" / PR {prs.Count} 件" : "";
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
