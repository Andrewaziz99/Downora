using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Services;

/// <summary>Keeps Windows' "start at sign-in" entry in line with the setting, and pointing at where Downora really is.</summary>
internal sealed partial class StartupSynchronizer(
    IStartupRegistry registry,
    ISettingsService settings,
    ILogger<StartupSynchronizer> logger) : IHostedService, IStartupEntrySynchronizer
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        settings.Changed += OnSettingsChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        settings.Changed -= OnSettingsChanged;
        return Task.CompletedTask;
    }

    /// <summary>Called once after settings load, so an entry written by an older location of the app is corrected.</summary>
    public void Synchronize() => Apply(settings.Current);

    private void OnSettingsChanged(object? sender, AppSettings changed) => Apply(changed);

    internal void Apply(AppSettings current)
    {
        try
        {
            var wanted = current.LaunchAtStartup && Environment.ProcessPath is { Length: > 0 } exe ? StartupCommand.Build(exe) : null;
            if (!string.Equals(registry.GetCommand(), wanted, StringComparison.Ordinal))
            {
                registry.SetCommand(wanted);
                LogApplied(wanted is not null);
            }
        }
#pragma warning disable CA1031 // A locked-down registry must not break saving a setting or starting the app.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Start with Windows is now {Enabled}")]
    private partial void LogApplied(bool enabled);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not update the start-with-Windows entry")]
    private partial void LogFailed(Exception exception);
}
