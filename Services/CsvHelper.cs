using System.Data;
using System.Text;

namespace TaskAzure.Services;

public static class CsvHelper
{
    /// <summary>CSV文字列をDataTableに変換 (1行目がヘッダー)</summary>
    public static DataTable ParseToDataTable(string csv)
    {
        var dt = new DataTable();
        if (string.IsNullOrWhiteSpace(csv)) return dt;

        var lines = SplitCsvLines(csv);
        if (lines.Count == 0) return dt;

        var headers = ParseCsvLine(lines[0]);
        foreach (var h in headers)
            dt.Columns.Add(h.Trim(), typeof(string));

        for (int i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var values = ParseCsvLine(lines[i]);
            var row = dt.NewRow();
            for (int j = 0; j < dt.Columns.Count; j++)
                row[j] = j < values.Length ? values[j] : "";
            dt.Rows.Add(row);
        }
        return dt;
    }

    /// <summary>DataTableをCSV文字列に変換 (1行目がヘッダー)</summary>
    public static string SerializeFromDataTable(DataTable dt)
    {
        var sb = new StringBuilder();
        var colNames = dt.Columns.Cast<DataColumn>().Select(c => QuoteCsv(c.ColumnName));
        sb.AppendLine(string.Join(",", colNames));

        foreach (DataRow row in dt.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            var values = dt.Columns.Cast<DataColumn>()
                           .Select(c => QuoteCsv(row[c]?.ToString() ?? ""));
            sb.AppendLine(string.Join(",", values));
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>1行のCSVをフィールド配列に分割 (クォート対応)</summary>
    public static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }

    /// <summary>必要に応じてダブルクォートでエスケープ</summary>
    public static string QuoteCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static List<string> SplitCsvLines(string csv)
    {
        var lines = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                lines.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) lines.Add(sb.ToString());
        return lines;
    }
}
