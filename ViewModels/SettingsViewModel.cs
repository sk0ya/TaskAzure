using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskAzure.Services;

namespace TaskAzure.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly SettingsService _settings;

    private string _orgUrl = "";
    private string _project = "";
    private string _patEnvVarName = "ADO_PAT";
    private int _refreshMinutes = 5;

    public string OrganizationUrl
    {
        get => _orgUrl;
        set { _orgUrl = value; OnPropertyChanged(); }
    }

    public string Project
    {
        get => _project;
        set { _project = value; OnPropertyChanged(); }
    }

    /// <summary>PAT を読み取る環境変数名 (デフォルト: ADO_PAT)</summary>
    public string PatEnvVarName
    {
        get => _patEnvVarName;
        set { _patEnvVarName = value; OnPropertyChanged(); OnPropertyChanged(nameof(EnvPatValue)); }
    }

    /// <summary>現在の環境変数の値が存在するか (設定画面でのステータス表示用)</summary>
    public string EnvPatValue => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PatEnvVarName))
        ? "✓ 環境変数が設定されています"
        : "✗ 環境変数が見つかりません";

    public int RefreshIntervalMinutes
    {
        get => _refreshMinutes;
        set { _refreshMinutes = value > 0 ? value : 1; OnPropertyChanged(); }
    }

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        Load();
    }

    private void Load()
    {
        var s = _settings.Load();
        OrganizationUrl = s.OrganizationUrl;
        Project = s.Project;
        PatEnvVarName = string.IsNullOrWhiteSpace(s.PatEnvVarName) ? "ADO_PAT" : s.PatEnvVarName;
        RefreshIntervalMinutes = s.RefreshIntervalMinutes > 0 ? s.RefreshIntervalMinutes : 5;
    }

    public (bool ok, string error) Save(double winLeft, double winTop)
    {
        if (string.IsNullOrWhiteSpace(OrganizationUrl))
            return (false, "Organization URL を入力してください。");
        if (string.IsNullOrWhiteSpace(Project))
            return (false, "プロジェクト名を入力してください。");
        if (string.IsNullOrWhiteSpace(PatEnvVarName))
            return (false, "PAT 環境変数名を入力してください。");

        var s = _settings.Load();
        s.OrganizationUrl = OrganizationUrl.TrimEnd('/');
        s.Project = Project;
        s.PatEnvVarName = PatEnvVarName.Trim();
        s.RefreshIntervalMinutes = RefreshIntervalMinutes;
        s.WindowLeft = winLeft;
        s.WindowTop = winTop;

        _settings.Save(s);
        return (true, "");
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
