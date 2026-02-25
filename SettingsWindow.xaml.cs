using System.Windows;
using TaskAzure.Services;
using TaskAzure.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace TaskAzure;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    /// <summary>設定が保存されたときに App が購読するイベント</summary>
    public event Action? SettingsSaved;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        OrgUrlBox.Text = vm.OrganizationUrl;
        ProjectBox.Text = vm.Project;
        PatEnvVarBox.Text = vm.PatEnvVarName;
        RefreshBox.Text = vm.RefreshIntervalMinutes.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.OrganizationUrl = OrgUrlBox.Text.Trim();
        _vm.Project = ProjectBox.Text.Trim();
        _vm.PatEnvVarName = PatEnvVarBox.Text.Trim();

        if (int.TryParse(RefreshBox.Text, out var interval))
            _vm.RefreshIntervalMinutes = interval;

        var (ok, error) = _vm.Save(
            Owner?.Left ?? 100,
            Owner?.Top ?? 100);

        if (!ok)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        SettingsSaved?.Invoke();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
