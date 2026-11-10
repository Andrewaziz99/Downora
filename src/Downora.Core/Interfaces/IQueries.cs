using Downora.Core.Enums;
using Downora.Core.Models;

namespace Downora.Core.Interfaces;

/// <summary>Read side of the download queue and history.</summary>
public interface IDownloadJobQueries
{
    /// <summary>Jobs that are not finished, in the order the queue will run them.</summary>
    Task<IReadOnlyList<DownloadJob>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>How many jobs are in each status; statuses with no jobs are absent.</summary>
    Task<IReadOnlyDictionary<DownloadStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Finished jobs (Completed, Failed, Cancelled), newest first.</summary>
    Task<IReadOnlyList<DownloadJob>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default);

    Task<DownloadStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes finished jobs. Library entries and files on disk are kept. Returns how many were removed.</summary>
    Task<int> ClearHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes finished jobs that ended before <paramref name="cutoffUtc"/>. Library entries and files are kept. Returns how many were removed.</summary>
    Task<int> PruneHistoryAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>Deletes one finished job from history. The file and its library entry are kept. Returns false when there is no such finished job.</summary>
    Task<bool> RemoveFromHistoryAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>The local media library.</summary>
public interface IMediaLibraryService
{
    Task<IReadOnlyList<MediaItem>> QueryAsync(LibraryQuery query, CancellationToken cancellationToken = default);

    /// <summary>Adds (or refreshes) the entry for a completed download. Does nothing when the job has no file on disk.</summary>
    Task AddCompletedAsync(DownloadJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds completed downloads from before the library existed (once) and marks entries whose file is gone (or back). Never throws:
    /// a failure is logged and the library is left as it was. Returns how many entries changed, so a screen can reload only when needed.
    /// </summary>
    Task<int> SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes an entry from the library. The file on disk is kept. Returns false when there is no such entry.</summary>
    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IDiskSpaceService
{
    /// <summary>Usage of the drive holding <paramref name="path"/>, or null when it can't be read (offline network share, missing drive).</summary>
    DiskUsage? GetUsage(string path);
}
