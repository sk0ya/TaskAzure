using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TaskAzure.Models;
using TaskAzure.Services;

namespace TaskAzure.ViewModels;

public partial class CsvCreatorViewModel : INotifyPropertyChanged
{
    private readonly AzureDevOpsService _adoService;
    private readonly WorkItemViewModel _parentItem;
    private readonly AppSettings _settings;
    private List<AdoUser> _allUsers = [];
    private CancellationTokenSource? _templateChangeCts;

    public string ParentItemLabel => $"#{_parentItem.Id}: {_parentItem.Title}";

    public ObservableCollection<Template> Templates { get; } = [];

    private Template? _selectedTemplate;
    public Template? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            _selectedTemplate = value;
            OnPropertyChanged();
            _ = OnTemplateChangedAsync();
        }
    }

    public ObservableCollection<VariableInput> VariableInputs { get; } = [];

    private bool _isLoadingUsers;
    public bool IsLoadingUsers
    {
        get => _isLoadingUsers;
        private set { _isLoadingUsers = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPropertyChanged(); }
    }

    /// <summary>プレビューDataTableが更新された際に発火</summary>
    public event Action? PreviewRefreshRequested;

    public CsvCreatorViewModel(AzureDevOpsService adoService, WorkItemViewModel parentItem,
                                AppSettings settings, TemplateService templateService)
    {
        _adoService = adoService;
        _parentItem = parentItem;
        _settings = settings;

        foreach (var t in templateService.Load())
            Templates.Add(t);

        SelectedTemplate = Templates.FirstOrDefault();
    }

    private async Task OnTemplateChangedAsync()
    {
        // キャンセル: 前回の操作が進行中なら中断する
        _templateChangeCts?.Cancel();
        _templateChangeCts = new CancellationTokenSource();
        var ct = _templateChangeCts.Token;

        foreach (var v in VariableInputs)
            v.ValueChanged -= OnVariableValueChanged;
        VariableInputs.Clear();
        StatusMessage = "";

        if (_selectedTemplate == null)
        {
            PreviewRefreshRequested?.Invoke();
            return;
        }

        var vars = ParseVariables(_selectedTemplate.CsvContent);

        // ユーザー変数があれば ADO からユーザー一覧を取得 (初回のみ)
        if (vars.Any(v => v.Kind == VariableKind.User) && _allUsers.Count == 0)
        {
            IsLoadingUsers = true;
            StatusMessage = "ユーザー一覧を取得中...";
            try
            {
                _allUsers = await _adoService.GetUsersAsync(ct);
            }
            catch (OperationCanceledException)
            {
                IsLoadingUsers = false;
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"ユーザー取得失敗: {ex.Message}";
            }
            finally
            {
                IsLoadingUsers = false;
            }
        }

        if (ct.IsCancellationRequested) return;

        foreach (var v in vars)
        {
            if (v.Kind == VariableKind.User) v.Users = _allUsers;
            v.ValueChanged += OnVariableValueChanged;
            VariableInputs.Add(v);
        }

        StatusMessage = "";
        PreviewRefreshRequested?.Invoke();
    }

    private void OnVariableValueChanged() => PreviewRefreshRequested?.Invoke();

    /// <summary>テンプレートのCSVを変数解決してDataTableを返す</summary>
    public DataTable GeneratePreviewTable()
    {
        if (_selectedTemplate == null) return new DataTable();
        var resolved = ResolveVariables(_selectedTemplate.CsvContent);
        return CsvHelper.ParseToDataTable(resolved);
    }

    private string ResolveVariables(string csv)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["parent.Id"]    = _parentItem.Id.ToString(),
            ["parent.Title"] = _parentItem.Title,
            ["parent.Type"]  = _parentItem.WorkItemType,
            ["parent.State"] = _parentItem.State,
        };

        foreach (var v in VariableInputs)
            values[v.Key] = v.ResolvedValue;

        return VarRegex().Replace(csv, m =>
        {
            var key = m.Groups[1].Value;
            return values.TryGetValue(key, out var val) ? val : m.Value;
        });
    }

    /// <summary>テンプレートCSVから変数を抽出 (重複除去・インデックス順)</summary>
    private static List<VariableInput> ParseVariables(string csv)
    {
        var seen = new Dictionary<string, VariableInput>(StringComparer.Ordinal);
        foreach (Match m in VarRegex().Matches(csv))
        {
            var key = m.Groups[1].Value;
            if (seen.ContainsKey(key)) continue;
            // parent.* は自動解決なのでスキップ
            if (key.StartsWith("parent.", StringComparison.Ordinal)) continue;

            var parts = key.Split(':', 2);
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[0], out var idx)) continue;

            var kind = parts[1].Trim().ToLowerInvariant() switch
            {
                "user" => VariableKind.User,
                _ => VariableKind.Text,
            };

            var kindLabel = kind == VariableKind.User ? "ユーザー" : "テキスト";
            seen[key] = new VariableInput
            {
                Key   = key,
                Label = $"{kindLabel} #{idx}  ({key})",
                Kind  = kind,
            };
        }
        // 数値インデックスでソート (10以上でも正しい順序になる)
        return [.. seen.Values.OrderBy(v => int.TryParse(v.Key.Split(':')[0], out var n) ? n : 0)];
    }

    /// <summary>Azure DevOps の Queries ページ URL</summary>
    public string QueriesUrl =>
        $"{_settings.OrganizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(_settings.Project)}/_queries";

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex VarRegex();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
