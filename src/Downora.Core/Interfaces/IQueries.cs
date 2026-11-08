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
}

/// <summary>The local media library.</summary>
public interface IMediaLibraryService
{
    Task<IReadOnlyList<MediaItem>> QueryAsync(LibraryQuery query, CancellationToken cancellationToken = default);
}

public interface IDiskSpaceService
{
    /// <summary>Usage of the drive holding <paramref name="path"/>, or null when it can't be read (offline network share, missing drive).</summary>
    DiskUsage? GetUsage(string path);
}
