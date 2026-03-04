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
            if (!File.Exists(FilePath)) return [];
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<Template>>(json) ?? [];
        }
        catch { return []; }
    }

    public void Save(List<Template> templates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json = JsonSerializer.Serialize(templates, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
