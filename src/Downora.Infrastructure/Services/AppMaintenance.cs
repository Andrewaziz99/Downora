using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Services;

internal sealed partial class AppMaintenance(
    IDownloadJobQueries jobs,
    IToolManager tools,
    ISettingsService settings,
    TimeProvider time,
    ILogger<AppMaintenance> logger) : IAppMaintenance, IHostedService
{
    /// <summary>yt-dlp is checked for an update at most this often.</summary>
    internal static readonly TimeSpan ToolCheckInterval = TimeSpan.FromHours(24);

    private int? _appliedRetention;
    private bool _retentionApplied;

    /// <summary>Only subscribes: settings and the database are not ready when the host starts.</summary>
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

    public async Task RunStartupTasksAsync(CancellationToken cancellationToken = default)
    {
        await RunQuietlyAsync(() => PruneAsync(settings.Current.HistoryRetentionDays, cancellationToken)).ConfigureAwait(false);
        await RunQuietlyAsync(() => UpdateToolsAsync(cancellationToken)).ConfigureAwait(false);
    }

    private void OnSettingsChanged(object? sender, AppSettings changed)
    {
        // A shorter retention takes effect at once; unrelated saves (and the timestamp saved after an update check) don't re-run it.
        if (!_retentionApplied || changed.HistoryRetentionDays != _appliedRetention)
        {
            _ = RunQuietlyAsync(() => PruneAsync(changed.HistoryRetentionDays, CancellationToken.None));
        }
    }

    private async Task PruneAsync(int? days, CancellationToken cancellationToken)
    {
        _appliedRetention = days;
        _retentionApplied = true;
        if (days is not > 0)
        {
            return;
        }

        var cutoff = time.GetUtcNow().UtcDateTime.AddDays(-days.Value);
        var removed = await jobs.PruneHistoryAsync(cutoff, cancellationToken).ConfigureAwait(false);
        if (removed > 0)
        {
            LogPruned(removed, days.Value);
        }
    }

    private async Task UpdateToolsAsync(CancellationToken cancellationToken)
    {
        var current = settings.Current;
        var now = time.GetUtcNow().UtcDateTime;
        if (!current.YtDlpAutoUpdate
            || (current.LastToolCheckUtc is { } last && now - last < ToolCheckInterval)
            || tools.GetPath(ToolKind.YtDlp) is null)
        {
            return;
        }

        // Replacing yt-dlp under a running download would break it; try again at the next start instead.
        var counts = await jobs.CountByStatusAsync(cancellationToken).ConfigureAwait(false);
        if (counts.Any(c => c.Key is DownloadStatus.Extracting or DownloadStatus.Downloading or DownloadStatus.Processing && c.Value > 0))
        {
            LogUpdateSkippedBusy();
            return;
        }

        try
        {
            var version = await tools.UpdateYtDlpAsync(cancellationToken).ConfigureAwait(false);
            LogUpdated(version);
        }
        catch (ToolInstallException ex)
        {
            LogUpdateFailed(ex);
        }

        // Failures count as a check too, so a broken network doesn't retry on every start.
        var latest = settings.Current;
        latest.LastToolCheckUtc = now;
        await settings.SaveAsync(latest, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunQuietlyAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Housekeeping must never stop the app or surface as an error; it is retried at the next start.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {Count} history entries older than {Days} days")]
    private partial void LogPruned(int count, int days);

    [LoggerMessage(Level = LogLevel.Information, Message = "yt-dlp checked for updates; version is now {Version}")]
    private partial void LogUpdated(string? version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Automatic yt-dlp update failed")]
    private partial void LogUpdateFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping the yt-dlp update check: downloads are running")]
    private partial void LogUpdateSkippedBusy();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A maintenance task failed")]
    private partial void LogFailed(Exception exception);
}
