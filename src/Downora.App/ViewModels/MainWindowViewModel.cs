using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.App.Services;
using Downora.Core.Interfaces;

namespace Downora.App.ViewModels;

/// <summary>
/// Phase 1 window: shows that the host, database and settings are working.
/// Phase 2 replaces its content with the navigation shell.
/// </summary>
public sealed partial class MainWindowViewModel(
    IAppPaths paths,
    ISettingsService settings,
    IShellService shell) : ObservableObject
{
    public string Title { get; } = $"Downora {AppInfo.Version}";

    public string DataDirectory => paths.DataDirectory;

    public string DatabasePath => paths.DatabasePath;

    [ObservableProperty]
    public partial string DownloadDirectory { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RecoveryMessage { get; private set; } = string.Empty;

    public void Load(int recoveredJobs)
    {
        DownloadDirectory = settings.Current.DownloadDirectory;
        RecoveryMessage = recoveredJobs switch
        {
            0 => "No interrupted downloads.",
            1 => "1 interrupted download was paused and can be resumed.",
            _ => $"{recoveredJobs} interrupted downloads were paused and can be resumed.",
        };
    }

    [RelayCommand]
    private void OpenDataFolder() => shell.OpenFolder(paths.DataDirectory);

    [RelayCommand]
    private void OpenDownloadFolder() => shell.OpenFolder(DownloadDirectory);
}
