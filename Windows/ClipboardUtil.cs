using System.Diagnostics;
using Clipboard = System.Windows.Clipboard;

namespace TaskAzure.Windows;

/// <summary>クリップボード / URL オープンの共通ヘルパー</summary>
internal static class ClipboardUtil
{
    public static void SetText(string text)
    {
        try { Clipboard.SetText(text); } catch { }
    }

    /// <summary>Markdown リンク (テキスト) + HTML リンクの両形式でクリップボードにセットする</summary>
    public static void SetLink(string markdown, string html)
    {
        try
        {
            var data = new System.Windows.DataObject();
            data.SetData(System.Windows.DataFormats.Text, markdown);
            data.SetData(System.Windows.DataFormats.Html, BuildHtmlClipboard(html));
            Clipboard.SetDataObject(data);
        }
        catch { }
    }

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    // Windows HTML クリップボード形式に必要なヘッダーを付与する
    private static string BuildHtmlClipboard(string html)
    {
        const string header =
            "Version:0.9\r\n" +
            "StartHTML:00000000\r\n" +
            "EndHTML:00000000\r\n" +
            "StartFragment:00000000\r\n" +
            "EndFragment:00000000\r\n";
        const string pre  = "<html><body><!--StartFragment-->";
        const string post = "<!--EndFragment--></body></html>";

        var startHtml     = System.Text.Encoding.UTF8.GetByteCount(header);
        var startFragment = startHtml + System.Text.Encoding.UTF8.GetByteCount(pre);
        var endFragment   = startFragment + System.Text.Encoding.UTF8.GetByteCount(html);
        var endHtml       = endFragment + System.Text.Encoding.UTF8.GetByteCount(post);

        return header
            .Replace("StartHTML:00000000",     $"StartHTML:{startHtml:D8}")
            .Replace("EndHTML:00000000",       $"EndHTML:{endHtml:D8}")
            .Replace("StartFragment:00000000", $"StartFragment:{startFragment:D8}")
            .Replace("EndFragment:00000000",   $"EndFragment:{endFragment:D8}")
            + pre + html + post;
    }
}
