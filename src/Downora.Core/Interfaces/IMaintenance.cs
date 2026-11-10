namespace Downora.Core.Interfaces;

/// <summary>Housekeeping that runs after startup and when the settings behind it change.</summary>
public interface IAppMaintenance
{
    /// <summary>
    /// Removes history older than the retention setting, and updates yt-dlp when automatic updates are on, it hasn't
    /// been checked for a day and nothing is downloading. Never throws; failures are logged.
    /// </summary>
    Task RunStartupTasksAsync(CancellationToken cancellationToken = default);
}

/// <summary>Brings the "start with Windows" entry in line with the settings.</summary>
public interface IStartupEntrySynchronizer
{
    /// <summary>Call once after settings load. Never throws.</summary>
    void Synchronize();
}

/// <summary>The "run at Windows sign-in" entry. Windows-specific, so the app supplies the implementation.</summary>
public interface IStartupRegistry
{
    /// <summary>The command stored for Downora, or null when it isn't set to start with Windows.</summary>
    string? GetCommand();

    /// <summary>Stores the command, or removes the entry when it is null.</summary>
    void SetCommand(string? command);
}
