namespace TaskAzure.Models;

public class PrTarget
{
    public string Project { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;

    public string DisplayName => $"{Project} / {Repository}";
}
