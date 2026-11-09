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

/// <summary>
/// Creates download jobs and runs them through a queue: at most <c>MaxConcurrentDownloads</c> at once, in the order
/// they were added, each saved as it changes (and announced to the screens that show it).
/// </summary>
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

    /// <summary>
    /// Holds a job: a running download stops (partial files are kept) and a waiting one leaves the line.
    /// Returns false when the job can't be paused (finished, or already being finished by FFmpeg).
    /// </summary>
    Task<bool> PauseAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Puts a paused job back in line; it runs as soon as a slot is free. Returns false when it isn't paused.</summary>
    Task<bool> ResumeAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Puts a failed or cancelled job back in line. A failed one continues from its partial files. Returns false for any other job.</summary>
    Task<bool> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Pauses every job that is running or waiting.</summary>
    Task PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Resumes every paused job, in their original order.</summary>
    Task ResumeAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the jobs the last session left waiting. Call it once at startup, after the database is ready and
    /// settings are loaded; from then on the queue runs itself.
    /// </summary>
    Task StartQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised from a background thread whenever a job changes state or makes progress.</summary>
    event EventHandler<DownloadJobUpdate>? JobUpdated;
}
