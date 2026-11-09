using Downora.Core.Enums;
using Downora.Core.Models;

namespace Downora.Core.Services;

/// <summary>
/// The rules for how a <see cref="DownloadJob"/> moves through its states: Waiting → Extracting → Downloading →
/// Processing → Completed, with Failed, Cancelled and Paused reachable from any unfinished state. Pure, so it is
/// tested without a database; the job service applies these and saves the result.
/// </summary>
public static class DownloadJobTransitions
{
    public static bool CanMove(DownloadStatus from, DownloadStatus to)
    {
        if (from is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
        {
            return false; // finished jobs only change when the user retries them (a new job)
        }

        return to switch
        {
            DownloadStatus.Waiting => from == DownloadStatus.Paused,
            DownloadStatus.Extracting => from is DownloadStatus.Waiting or DownloadStatus.Paused,
            DownloadStatus.Downloading => from is DownloadStatus.Extracting or DownloadStatus.Processing or DownloadStatus.Paused,
            DownloadStatus.Processing => from is DownloadStatus.Extracting or DownloadStatus.Downloading,
            DownloadStatus.Completed => from is DownloadStatus.Extracting or DownloadStatus.Downloading or DownloadStatus.Processing,
            DownloadStatus.Failed or DownloadStatus.Cancelled or DownloadStatus.Paused => true,
            _ => false,
        };
    }

    /// <summary>The job state that matches an engine stage.</summary>
    public static DownloadStatus StatusFor(DownloadStage stage) => stage switch
    {
        DownloadStage.Starting => DownloadStatus.Extracting,
        DownloadStage.Downloading => DownloadStatus.Downloading,
        _ => DownloadStatus.Processing,
    };

    /// <summary>The job begins work. Valid from Waiting, or from Paused when it is resumed.</summary>
    public static void Start(DownloadJob job, DateTime nowUtc)
    {
        Move(job, DownloadStatus.Extracting);
        job.StartedAtUtc ??= nowUtc;
        job.ErrorKind = DownloadErrorKind.None;
        job.ErrorMessage = null;
    }

    /// <summary>
    /// Applies an engine progress report. Status only moves forward (Extracting → Downloading → Processing) and the
    /// percentage never goes back, so a late or out-of-order report can't make the bar jump. Returns true when the
    /// status changed, which callers save immediately.
    /// </summary>
    public static bool ApplyProgress(DownloadJob job, DownloadProgress progress)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(progress);
        if (job.IsFinished || job.Status == DownloadStatus.Paused)
        {
            return false;
        }

        var target = StatusFor(progress.Stage);
        var changed = target != job.Status && Rank(target) > Rank(job.Status);
        if (changed)
        {
            job.Status = target;
        }

        job.ProgressPercent = Math.Clamp(Math.Max(job.ProgressPercent, progress.Percent), 0, 100);
        job.DownloadedBytes = Math.Max(job.DownloadedBytes, progress.DownloadedBytes);
        if (progress.TotalBytes is { } total && total > 0)
        {
            job.TotalBytes = total;
        }

        return changed;
    }

    public static void Complete(DownloadJob job, string filePath, long fileSize, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        Move(job, DownloadStatus.Completed);
        job.OutputPath = filePath;
        job.ProgressPercent = 100;
        job.DownloadedBytes = fileSize;
        job.TotalBytes = fileSize;
        job.ErrorKind = DownloadErrorKind.None;
        job.ErrorMessage = null;
        job.CompletedAtUtc = nowUtc;
    }

    public static void Fail(DownloadJob job, DownloadErrorKind kind, string? detail, DateTime nowUtc)
    {
        Move(job, DownloadStatus.Failed);
        job.ErrorKind = kind == DownloadErrorKind.None ? DownloadErrorKind.Unknown : kind;
        job.ErrorMessage = detail is { Length: > MaxMessageLength } ? detail[..MaxMessageLength] : detail;
        job.CompletedAtUtc = nowUtc;
    }

    public static void Cancel(DownloadJob job, DateTime nowUtc)
    {
        Move(job, DownloadStatus.Cancelled);
        job.CompletedAtUtc = nowUtc;
    }

    /// <summary>Stops a job without ending it, keeping its partial files (app closing, or a later "pause").</summary>
    public static void Pause(DownloadJob job)
    {
        Move(job, DownloadStatus.Paused);
    }

    /// <summary>Matches the column length so a long tool message can't make a save fail.</summary>
    private const int MaxMessageLength = 2000;

    private static void Move(DownloadJob job, DownloadStatus to)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!CanMove(job.Status, to))
        {
            throw new InvalidOperationException($"A download can't go from {job.Status} to {to}.");
        }

        job.Status = to;
    }

    private static int Rank(DownloadStatus status) => status switch
    {
        DownloadStatus.Waiting => 0,
        DownloadStatus.Extracting => 1,
        DownloadStatus.Downloading => 2,
        DownloadStatus.Processing => 3,
        _ => 4,
    };
}
