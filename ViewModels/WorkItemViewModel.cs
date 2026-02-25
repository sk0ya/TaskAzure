using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;
using TaskAzure.Models;

namespace TaskAzure.ViewModels;

public class WorkItemViewModel(WorkItem item)
{
    public int Id => item.Id;
    public string Title => item.Title;
    public string WorkItemType => item.WorkItemType;
    public string State => item.State;
    public string WebUrl => item.WebUrl;

    public string IdDisplay => $"#{item.Id}";

    public string TypeShort => item.WorkItemType switch
    {
        "Bug" => "Bug",
        "User Story" => "Story",
        "Task" => "Task",
        "Feature" => "Feature",
        "Epic" => "Epic",
        "Test Case" => "Test",
        "Issue" => "Issue",
        var s when s.Length > 5 => s[..5],
        var s => s,
    };

    public Brush TypeColor => item.WorkItemType switch
    {
        "Bug" => new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33)),
        "User Story" => new SolidColorBrush(Color.FromRgb(0x5E, 0x35, 0xB1)),
        "Task" => new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
        "Feature" => new SolidColorBrush(Color.FromRgb(0x22, 0x88, 0x44)),
        "Epic" => new SolidColorBrush(Color.FromRgb(0xE6, 0x57, 0x22)),
        "Test Case" => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0x55, 0x66, 0x77)),
    };

    /// <summary>コンテキストメニュー「リンクを作成」で生成するマークダウン形式のリンク</summary>
    public string MarkdownLink => $"[#{item.Id} {item.Title}]({item.WebUrl})";
}
