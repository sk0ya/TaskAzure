namespace TaskAzure.Models;

public class Template
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "新規テンプレート";
    /// <summary>
    /// CSV形式のテンプレート内容。
    /// 変数記法: ${parent.Id}, ${parent.Title}, ${parent.Type}, ${parent.State},
    ///           ${parent.AssignedTo}, ${parent.AreaPath}, ${parent.IterationPath},
    ///           ${parent.DevelopProcess}
    ///           ${0:user}, ${1:user}  ... ユーザー選択 (同インデックス=同変数)
    ///           ${0:text}, ${1:text} ... テキスト入力
    /// </summary>
    public string CsvContent { get; set; } =
        "Title,WorkItemType,AssignedTo\r\n${parent.Title} - 作業,Task,${0:user}";
}
