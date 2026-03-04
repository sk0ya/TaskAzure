using System.IO;
using System.Text.Json;
using TaskAzure.Models;

namespace TaskAzure.Services;

public class TemplateService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskAzure", "templates.json");

    public List<Template> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return CreateDefaultTemplates();
            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<List<Template>>(json);
            return NormalizeTemplates(loaded);
        }
        catch { return CreateDefaultTemplates(); }
    }

    public void Save(List<Template> templates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var normalized = NormalizeTemplates(templates);
        var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    private static List<Template> CreateDefaultTemplates() => [new Template()];

    private static List<Template> NormalizeTemplates(List<Template>? templates)
    {
        var normalized = new List<Template>();

        if (templates != null)
        {
            foreach (var t in templates)
            {
                if (t == null) continue;

                normalized.Add(new Template
                {
                    Id = string.IsNullOrWhiteSpace(t.Id) ? Guid.NewGuid().ToString() : t.Id,
                    Name = string.IsNullOrWhiteSpace(t.Name) ? $"テンプレート {normalized.Count + 1}" : t.Name,
                    CsvContent = string.IsNullOrWhiteSpace(t.CsvContent) ? new Template().CsvContent : t.CsvContent,
                });
            }
        }

        if (normalized.Count == 0)
            normalized.Add(new Template());

        return normalized;
    }
}
