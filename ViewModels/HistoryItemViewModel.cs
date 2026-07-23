using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;
using TaskAzure.Models;

namespace TaskAzure.ViewModels;

/// <summary>履歴ウィンドウの1行分。WorkItemViewModel の表示ロジックを流用する</summary>
public class HistoryItemViewModel(WorkItemHistoryEntry entry) : WorkItemViewModel(entry.ToWorkItem())
{
    public DateTime FirstSeen => entry.FirstSeen;
    public DateTime LastSeen => entry.LastSeen;

    public string FirstSeenDisplay => entry.FirstSeen.ToString("yyyy/MM/dd");
    public string LastSeenDisplay => entry.LastSeen.ToString("yyyy/MM/dd");

    public string TooltipText =>
        $"#{Id} {Title}\n状態: {State}\n初回確認: {FirstSeenDisplay} / 最終確認: {LastSeenDisplay}";

    public Brush StateColor => State switch
    {
        "Active" or "Doing" or "In Progress" or "Committed"
            => new SolidColorBrush(Color.FromRgb(0x4D, 0xAA, 0xFF)),  // 青: 進行中
        "Resolved"
            => new SolidColorBrush(Color.FromRgb(0x55, 0xCC, 0x88)),  // 緑: 解決済み
        "Closed" or "Done" or "Removed"
            => new SolidColorBrush(Color.FromRgb(0x66, 0x77, 0x88)),  // グレー: 完了
        "New" or "To Do" or "Proposed"
            => new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0x99)),  // 黄: 未着手
        _   => new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA)),
    };
}
