using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.Services;

/// <summary>A job update delivered on the UI thread, and whether the job's status is different from the last update.</summary>
public sealed record DownloadFeedEvent(DownloadJobUpdate Update, bool StatusChanged);

/// <summary>
/// Brings job updates (raised on background threads) to the UI thread for every screen that shows downloads,
/// and tells them which updates are a change of state (started, merging, finished) and which are only progress.
/// </summary>
public sealed partial class DownloadFeed : IDisposable
{
    private readonly IDownloadJobService _jobs;
    private readonly IUiThread _ui;
    private readonly ILogger<DownloadFeed> _logger;
    private readonly Dictionary<Guid, DownloadStatus> _lastStatus = [];

    public DownloadFeed(IDownloadJobService jobs, IUiThread ui, ILogger<DownloadFeed> logger)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        _jobs = jobs;
        _ui = ui;
        _logger = logger;
        _jobs.JobUpdated += OnJobUpdated;
    }

    /// <summary>Raised on the UI thread.</summary>
    public event EventHandler<DownloadFeedEvent>? Updated;

    public void Dispose() => _jobs.JobUpdated -= OnJobUpdated;

    private void OnJobUpdated(object? sender, DownloadJobUpdate update) => _ui.Post(() => Deliver(update));

    private void Deliver(DownloadJobUpdate update)
    {
        var job = update.Job;
        var changed = !_lastStatus.TryGetValue(job.Id, out var last) || last != job.Status;
        if (job.IsFinished)
        {
            _lastStatus.Remove(job.Id);
        }
        else
        {
            _lastStatus[job.Id] = job.Status;
        }

        var handler = Updated;
        if (handler is null)
        {
            return;
        }

        var feedEvent = new DownloadFeedEvent(update, changed);
        foreach (var listener in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<DownloadFeedEvent>)listener)(this, feedEvent);
            }
#pragma warning disable CA1031 // One screen failing to update must not stop the others.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogListenerFailed(job.Id, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A screen failed while showing an update of download {JobId}")]
    private partial void LogListenerFailed(Guid jobId, Exception exception);
}

/// <summary>What the buttons on a download card do.</summary>
public interface IDownloadItemActions
{
    Task CancelAsync(Guid jobId);

    Task PauseAsync(Guid jobId);

    Task ResumeAsync(Guid jobId);

    Task RetryAsync(Guid jobId);

    void OpenFolder(string folder);

    /// <summary>Shows the file in Explorer; if it was moved or deleted, opens its folder instead.</summary>
    void ShowFile(string filePath, string fallbackFolder);
}

internal sealed partial class DownloadItemActions(
    IDownloadJobService jobs,
    IShellService shell,
    ILogger<DownloadItemActions> logger) : IDownloadItemActions
{
    public Task CancelAsync(Guid jobId) => RunAsync(jobId, "cancel", () => jobs.CancelAsync(jobId));

    public Task PauseAsync(Guid jobId) => RunAsync(jobId, "pause", () => jobs.PauseAsync(jobId));

    public Task ResumeAsync(Guid jobId) => RunAsync(jobId, "resume", () => jobs.ResumeAsync(jobId));

    public Task RetryAsync(Guid jobId) => RunAsync(jobId, "retry", () => jobs.RetryAsync(jobId));

    private async Task RunAsync(Guid jobId, string action, Func<Task<bool>> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // A failed action is logged; the card keeps showing the real state of the job.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogActionFailed(action, jobId, ex);
        }
    }

    public void OpenFolder(string folder) => Try(() => shell.OpenFolder(folder), folder);

    public void ShowFile(string filePath, string fallbackFolder)
    {
        if (File.Exists(filePath))
        {
            Try(() => shell.ShowFileInFolder(filePath), filePath);
        }
        else
        {
            Try(() => shell.OpenFolder(fallbackFolder), fallbackFolder);
        }
    }

    private void Try(Action action, string path)
    {
        try
        {
            action();
        }
#pragma warning disable CA1031 // Explorer failing to open is not worth interrupting the user; it is logged.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogShellFailed(path, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not {Action} download {JobId}")]
    private partial void LogActionFailed(string action, Guid jobId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not open {Path} in Explorer")]
    private partial void LogShellFailed(string path, Exception exception);
}
