namespace TaskAzure.Models;

public class AppSettings
{
    public string OrganizationUrl { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    /// <summary>PAT を読み取る環境変数名。デフォルトは ADO_PAT。</summary>
    public string PatEnvVarName { get; set; } = "ADO_PAT";
    public int RefreshIntervalMinutes { get; set; } = 5;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public List<PrTarget> PrTargets { get; set; } = [];
    /// <summary>
    /// 子タスクCSV作成画面の前回値。
    /// Key: Template.Id, Value: 変数キー(例 "0:user") -> 入力値
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> CsvCreatorLastValues { get; set; } = new();
}
