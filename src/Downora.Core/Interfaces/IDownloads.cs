using Downora.Core.Models;

namespace Downora.Core.Interfaces;

/// <summary>Downloads one item to disk. The only place that knows how a download tool is driven.</summary>
public interface IDownloadEngine
{
    /// <summary>
    /// Downloads the item and reports progress as it goes. Expected failures (sign-in required, no space, network…)
    /// come back as a failed <see cref="DownloadResult"/>. Cancelling kills the whole tool process tree and throws
    /// <see cref="OperationCanceledException"/>; partial files stay in place so a later run can resume.
    /// </summary>
    Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes what a stopped download left behind (partial and intermediate files). Call it only when the user cancels.</summary>
    Task DiscardPartialFilesAsync(DownloadRequest request);
}

/// <summary>Creates download jobs, runs them and keeps their saved state (and the screens that show it) up to date.</summary>
public interface IDownloadJobService
{
    /// <summary>
    /// Saves a new job and starts it; follow it through <see cref="JobUpdated"/>. When the same item is already
    /// queued or running at the same quality, nothing new is started and that job is returned instead.
    /// </summary>
    Task<DownloadStartResult> StartAsync(NewDownload download, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a job (running, waiting or paused) and discards its partial files, so it ends as Cancelled.
    /// Returns false when there is no unfinished job with that id.
    /// </summary>
    Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Raised from a background thread whenever a job changes state or makes progress.</summary>
    event EventHandler<DownloadJobUpdate>? JobUpdated;
}
