namespace Downora.Core.Interfaces;

/// <summary>Where Downora keeps its own files (database, logs, tools, thumbnail cache).</summary>
public interface IAppPaths
{
    string DataDirectory { get; }
    string DatabasePath { get; }
    string LogsDirectory { get; }
    string ToolsDirectory { get; }
    string ThumbnailsDirectory { get; }

    /// <summary>The folder downloads go to until the user picks another one.</summary>
    string DefaultDownloadDirectory { get; }

    /// <summary>Creates every app-owned directory that does not exist yet.</summary>
    void EnsureCreated();
}
