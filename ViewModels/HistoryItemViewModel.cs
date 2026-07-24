using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;
using TaskAzure.Models;

namespace TaskAzure.ViewModels;

/// <summary>履歴ウィンドウの1行 (WorkItem / PR 両対応)</summary>
public class HistoryItemViewModel(HistoryEntry entry)
{
    public HistoryKind Kind => entry.Kind;
    public int Id => entry.Id;
    public string Title => entry.Title;
    public string State => entry.State;
    public string WebUrl => entry.WebUrl;

    /// <summary>親 WorkItem の ID (WorkItem のみ)。親なしは 0</summary>
    public int ParentId => entry.ParentId;

    /// <summary>PR がリンクする WorkItem の ID 一覧</summary>
    public IReadOnlyList<int> LinkedWorkItemIds => entry.LinkedWorkItemIds;

    /// <summary>親子ツリーの深さ (ApplyFilter で設定)。左インデントに使用</summary>
    public int Depth { get; set; }
    public System.Windows.Thickness IndentMargin => new(Depth * 16, 0, 0, 0);

    /// <summary>履歴外から親子表示のために取得した親 (自分の履歴ではない)</summary>
    public bool IsContext { get; init; }
    public double RowOpacity => IsContext ? 0.6 : 1.0;

    /// <summary>種別フィルターの選択肢に使う表示名 (PR は "Pull Request")</summary>
    public string TypeName => entry.Kind == HistoryKind.PullRequest ? "Pull Request" : entry.WorkItemType;

    public DateTime FirstSeen => entry.FirstSeen;
    public DateTime LastSeen => entry.LastSeen;
    public string FirstSeenDisplay => entry.FirstSeen.ToString("yyyy/MM/dd");
    public string LastSeenDisplay => IsContext ? "" : entry.LastSeen.ToString("yyyy/MM/dd");

    public string IdDisplay => entry.Kind == HistoryKind.PullRequest ? $"PR#{entry.Id}" : $"#{entry.Id}";

    public string TypeShort => entry.Kind == HistoryKind.PullRequest
        ? "PR"
        : entry.WorkItemType switch
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

    public Brush TypeColor => entry.Kind == HistoryKind.PullRequest
        ? new SolidColorBrush(Color.FromRgb(0x66, 0xAA, 0xFF))  // 青: PR
        : entry.WorkItemType switch
        {
            "Bug"        => new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),
            "User Story" => new SolidColorBrush(Color.FromRgb(0xBB, 0x99, 0xFF)),
            "Task"       => new SolidColorBrush(Color.FromRgb(0x4D, 0xAA, 0xFF)),
            "Feature"    => new SolidColorBrush(Color.FromRgb(0x55, 0xCC, 0x88)),
            "Epic"       => new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x44)),
            "Test Case"  => new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0x44)),
            _            => new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA)),
        };

    public Brush StateColor => entry.State switch
    {
        "Active" or "Doing" or "In Progress" or "Committed"
            => new SolidColorBrush(Color.FromRgb(0x4D, 0xAA, 0xFF)),  // 青: 進行中
        "Resolved" or "Completed"
            => new SolidColorBrush(Color.FromRgb(0x55, 0xCC, 0x88)),  // 緑: 解決/完了
        "Closed" or "Done" or "Removed" or "Abandoned"
            => new SolidColorBrush(Color.FromRgb(0x66, 0x77, 0x88)),  // グレー: 終了
        "New" or "To Do" or "Proposed"
            => new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0x99)),  // 黄: 未着手
        _   => new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA)),
    };

    public string MarkdownLink => entry.Kind == HistoryKind.PullRequest
        ? $"[PR#{Id}: {Title}]({WebUrl})"
        : $"[{TypeShort} {Id}: {Title}]({WebUrl})";

    public string HtmlLink => entry.Kind == HistoryKind.PullRequest
        ? $"<a href=\"{WebUrl}\">PR#{Id}</a>: {Title}"
        : $"<a href=\"{WebUrl}\">{TypeShort} {Id}</a>: {Title}";

    public string TooltipText => IsContext
        ? $"{IdDisplay} {Title}\n状態: {State}\n(親: 履歴外の項目)"
        : $"{IdDisplay} {Title}\n状態: {State}\n初回確認: {FirstSeenDisplay} / 最終確認: {LastSeenDisplay}";

    // PR status 再取得用
    public string Project => entry.Project;
    public string RepositoryName => entry.RepositoryName;
}
