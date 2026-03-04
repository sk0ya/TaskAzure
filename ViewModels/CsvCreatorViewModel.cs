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
    private readonly SettingsService _settingsService;
    private List<AdoUser> _allUsers = [];
    private CancellationTokenSource? _templateChangeCts;
    private bool _suppressPreviewRefresh;

    public string ParentItemLabel => $"#{_parentItem.Id}: {_parentItem.Title}";

    public ObservableCollection<Template> Templates { get; } = [];

    private Template? _selectedTemplate;
    public Template? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (ReferenceEquals(_selectedTemplate, value)) return;
            PersistCurrentTemplateValues();
            _selectedTemplate = value;
            OnPropertyChanged();
            _ = OnTemplateChangedAsync();
        }
    }

    public ObservableCollection<VariableInput> VariableInputs { get; } = [];
    public bool HasUserVariables => VariableInputs.Any(v => v.Kind == VariableKind.User);

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
                                AppSettings settings, SettingsService settingsService,
                                TemplateService templateService)
    {
        _adoService = adoService;
        _parentItem = parentItem;
        _settings = settings;
        _settingsService = settingsService;

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
        OnPropertyChanged(nameof(HasUserVariables));
        StatusMessage = "";

        if (_selectedTemplate == null)
        {
            PreviewRefreshRequested?.Invoke();
            return;
        }

        var vars = ParseVariables(_selectedTemplate.CsvContent);
        var hasUserVariables = vars.Any(v => v.Kind == VariableKind.User);
        var userFetchMessage = "";

        // ユーザー変数があれば ADO からユーザー一覧を取得 (初回のみ)
        if (hasUserVariables && _allUsers.Count == 0)
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
                userFetchMessage = $"ユーザー取得失敗: {ex.Message}";
            }
            finally
            {
                IsLoadingUsers = false;
            }
        }

        if (ct.IsCancellationRequested) return;

        if (hasUserVariables && _allUsers.Count == 0)
        {
            // 取得失敗時でも最低限の選択肢を出す
            var usedFallbackUser = false;
            if (!string.IsNullOrWhiteSpace(_parentItem.AssignedTo))
            {
                _allUsers =
                [
                    new AdoUser
                    {
                        DisplayName = _parentItem.AssignedTo,
                        UniqueName = _parentItem.AssignedTo,
                    }
                ];
                usedFallbackUser = true;
            }

            if (usedFallbackUser && string.IsNullOrWhiteSpace(userFetchMessage))
                userFetchMessage = "ユーザー一覧を取得できなかったため、親担当者のみ候補に表示しています。";

            if (_allUsers.Count == 0 && string.IsNullOrWhiteSpace(userFetchMessage))
                userFetchMessage = "ユーザー一覧を取得できませんでした。PATと権限設定を確認してください。";
        }

        foreach (var v in vars)
        {
            if (v.Kind == VariableKind.User)
                v.Users = [.. _allUsers];
            v.ValueChanged += OnVariableValueChanged;
            VariableInputs.Add(v);
        }

        if (hasUserVariables)
            ApplyLastValuesCore(userOnly: true, showStatus: false, raisePreview: false);

        OnPropertyChanged(nameof(HasUserVariables));
        StatusMessage = userFetchMessage;
        PreviewRefreshRequested?.Invoke();
    }

    public bool ApplyLastValues()
        => ApplyLastValuesCore(userOnly: false, showStatus: true, raisePreview: true);

    private bool ApplyLastValuesCore(bool userOnly, bool showStatus, bool raisePreview)
    {
        if (_selectedTemplate == null)
            return false;

        var saved = GetSavedValuesForTemplate(_selectedTemplate);
        if (saved == null || saved.Count == 0)
        {
            if (showStatus)
                StatusMessage = "前回値が見つかりません。";
            return false;
        }

        var applied = false;
        _suppressPreviewRefresh = true;
        try
        {
            foreach (var input in VariableInputs)
            {
                if (!saved.TryGetValue(input.Key, out var savedValue))
                    continue;

                if (input.Kind == VariableKind.User)
                {
                    var user = _allUsers.FirstOrDefault(u =>
                                   string.Equals(u.UniqueName, savedValue, StringComparison.OrdinalIgnoreCase))
                               ?? _allUsers.FirstOrDefault(u =>
                                   string.Equals(u.DisplayName, savedValue, StringComparison.OrdinalIgnoreCase));

                    if (user == null && !string.IsNullOrWhiteSpace(savedValue))
                    {
                        user = new AdoUser
                        {
                            DisplayName = savedValue,
                            UniqueName = savedValue,
                        };
                    }

                    if (user != null && !input.Users.Any(u =>
                            string.Equals(u.UniqueName, user.UniqueName, StringComparison.OrdinalIgnoreCase)))
                    {
                        input.Users = [user, .. input.Users];
                    }

                    input.SelectedUser = user;
                    if (user != null) applied = true;
                    continue;
                }

                if (userOnly) continue;
                input.TextValue = savedValue;
                applied = true;
            }
        }
        finally
        {
            _suppressPreviewRefresh = false;
        }

        if (!applied)
        {
            if (showStatus)
                StatusMessage = "前回値は見つかりましたが、適用対象がありませんでした。";
            return false;
        }

        if (showStatus)
            StatusMessage = userOnly ? "前回ユーザー値を設定しました。" : "前回値を設定しました。";
        if (raisePreview)
            PreviewRefreshRequested?.Invoke();
        return true;
    }

    public void SaveCurrentValuesAsLastUsed()
    {
        if (_selectedTemplate == null) return;
        SaveValuesForTemplate(_selectedTemplate, VariableInputs);
    }

    private void PersistCurrentTemplateValues()
    {
        if (_selectedTemplate == null || VariableInputs.Count == 0) return;
        SaveValuesForTemplate(_selectedTemplate, VariableInputs);
    }

    private void SaveValuesForTemplate(Template template, IReadOnlyCollection<VariableInput> inputs)
    {
        try
        {
            var latest = _settingsService.Load();
            latest.CsvCreatorLastValues ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                var value = input.Kind == VariableKind.User
                    ? input.SelectedUser?.UniqueName ?? ""
                    : input.TextValue;

                if (!string.IsNullOrWhiteSpace(value))
                    values[input.Key] = value;
            }

            var storageKey = GetTemplateStorageKey(template);
            if (values.Count == 0)
                latest.CsvCreatorLastValues.Remove(storageKey);
            else
                latest.CsvCreatorLastValues[storageKey] = values;

            _settings.CsvCreatorLastValues = latest.CsvCreatorLastValues;
            _settingsService.Save(latest);
        }
        catch
        {
            // 前回値保存失敗はCSV作成自体を妨げない
        }
    }

    private Dictionary<string, string>? GetSavedValuesForTemplate(Template template)
    {
        if (_settings.CsvCreatorLastValues == null)
            return null;

        return _settings.CsvCreatorLastValues.TryGetValue(GetTemplateStorageKey(template), out var values)
            ? values
            : null;
    }

    private static string GetTemplateStorageKey(Template template)
        => string.IsNullOrWhiteSpace(template.Id) ? template.Name : template.Id;

    private void OnVariableValueChanged()
    {
        if (_suppressPreviewRefresh) return;
        PreviewRefreshRequested?.Invoke();
    }

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
            ["parent.Id"]            = _parentItem.Id.ToString(),
            ["parent.Title"]         = _parentItem.Title,
            ["parent.Type"]          = _parentItem.WorkItemType,
            ["parent.State"]         = _parentItem.State,
            ["parent.AssignedTo"]    = _parentItem.AssignedTo,
            ["parent.AreaPath"]      = _parentItem.AreaPath,
            ["parent.IterationPath"] = _parentItem.IterationPath,
            ["parent.DevelopProcess"] = _parentItem.DevelopProcess,
            // タイポ互換: Prosess でも同じ値に解決
            ["parent.DevelopProsess"] = _parentItem.DevelopProcess,
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
