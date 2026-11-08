using Downora.Core.Enums;

namespace Downora.Core.Models;

/// <summary>Filter for finished downloads (the History screen).</summary>
public sealed record HistoryQuery(string? Search = null, DownloadStatus? Status = null, int Take = 200);

public enum LibrarySort
{
    DateAdded,
    Title,
    Duration,
    FileSize,
}

/// <summary>Filter and sort for the Media Library screen.</summary>
public sealed record LibraryQuery(
    string? Search = null,
    MediaKind? Kind = null,
    LibrarySort Sort = LibrarySort.DateAdded,
    bool Descending = true,
    int Take = 500);

/// <summary>Numbers shown on the Home dashboard.</summary>
public sealed record DownloadStatistics(
    int TotalCompleted,
    int CompletedLast7Days,
    long BytesDownloaded,
    int ActiveCount,
    double? SuccessRate);

/// <summary>Space on the drive that holds the download folder.</summary>
public sealed record DiskUsage(string DriveName, long TotalBytes, long FreeBytes)
{
    public long UsedBytes => TotalBytes - FreeBytes;

    public double UsedFraction => TotalBytes <= 0 ? 0 : (double)UsedBytes / TotalBytes;
}
