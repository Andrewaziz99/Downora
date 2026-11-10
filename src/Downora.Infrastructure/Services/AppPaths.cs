using Downora.Core.Interfaces;

namespace Downora.Infrastructure.Services;

/// <summary>
/// Default layout: everything app-owned lives under <c>%LocalAppData%\Downora</c>,
/// downloads go to <c>%UserProfile%\Downloads\Downora</c>.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public const string AppFolderName = "Downora";

    /// <summary>
    /// For testing and development: a full path here moves everything Downora keeps (database, logs, tools) to that folder,
    /// so a test copy never touches the real settings or history. Not meant for everyday use.
    /// </summary>
    public const string DataDirectoryVariable = "DOWNORA_DATA_DIR";

    public AppPaths(string dataDirectory, string defaultDownloadDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultDownloadDirectory);

        DataDirectory = Path.GetFullPath(dataDirectory);
        DefaultDownloadDirectory = Path.GetFullPath(defaultDownloadDirectory);
    }

    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "downora.db");
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");
    public string ToolsDirectory => Path.Combine(DataDirectory, "tools");
    public string ThumbnailsDirectory => Path.Combine(DataDirectory, "thumbnails");
    public string DefaultDownloadDirectory { get; }

    /// <summary>Paths for the signed-in Windows user.</summary>
    public static AppPaths ForCurrentUser()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads", AppFolderName);
        if (Environment.GetEnvironmentVariable(DataDirectoryVariable) is { Length: > 0 } custom && Path.IsPathFullyQualified(custom))
        {
            return new AppPaths(custom, downloads);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(Path.Combine(localAppData, AppFolderName), downloads);
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ToolsDirectory);
        Directory.CreateDirectory(ThumbnailsDirectory);
    }
}
