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
        "Bug"        => new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),  // 明るい赤
        "User Story" => new SolidColorBrush(Color.FromRgb(0xBB, 0x99, 0xFF)),  // 明るい紫
        "Task"       => new SolidColorBrush(Color.FromRgb(0x4D, 0xAA, 0xFF)),  // 明るい青
        "Feature"    => new SolidColorBrush(Color.FromRgb(0x55, 0xCC, 0x88)),  // 明るい緑
        "Epic"       => new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x44)),  // オレンジ
        "Test Case"  => new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0x44)),  // 黄
        _            => new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA)),  // グレー
    };

    /// <summary>コンテキストメニュー「リンクを作成」で生成するマークダウン形式のリンク</summary>
    public string MarkdownLink => $"[#{item.Id} {item.Title}]({item.WebUrl})";
}
