using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TaskAzure.Models;
using TaskAzure.Services;

namespace TaskAzure.ViewModels;

public class TemplateManagerViewModel : INotifyPropertyChanged
{
    private readonly TemplateService _templateService = new();

    public ObservableCollection<Template> Templates { get; } = [];

    private Template? _selectedTemplate;
    public Template? SelectedTemplate
    {
        get => _selectedTemplate;
        set { _selectedTemplate = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => _selectedTemplate != null;

    public RelayCommand AddTemplateCommand { get; }
    public RelayCommand DeleteTemplateCommand { get; }

    public TemplateManagerViewModel()
    {
        AddTemplateCommand = new RelayCommand(AddTemplate);
        DeleteTemplateCommand = new RelayCommand(DeleteTemplate, () => HasSelection);
        Load();
    }

    private void Load()
    {
        Templates.Clear();
        foreach (var t in _templateService.Load())
            Templates.Add(t);
        SelectedTemplate = Templates.FirstOrDefault();
    }

    private void AddTemplate()
    {
        var t = new Template { Name = $"テンプレート {Templates.Count + 1}" };
        Templates.Add(t);
        SelectedTemplate = t;
    }

    private void DeleteTemplate()
    {
        if (_selectedTemplate == null) return;
        var idx = Templates.IndexOf(_selectedTemplate);
        Templates.Remove(_selectedTemplate);
        SelectedTemplate = Templates.Count > 0
            ? Templates[Math.Max(0, idx - 1)]
            : null;
    }

    public void Save() => _templateService.Save([.. Templates]);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
