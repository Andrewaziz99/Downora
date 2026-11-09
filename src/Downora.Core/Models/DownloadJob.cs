using Downora.Core.Enums;

namespace Downora.Core.Models;

/// <summary>
/// One requested download. The queue and the history are the same table:
/// history is simply the jobs whose <see cref="Status"/> is final.
/// </summary>
public class DownloadJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string SourceUrl { get; set; }
    public required string NormalizedUrl { get; set; }
    public SourcePlatform Platform { get; set; }

    public string? Title { get; set; }
    public string? Uploader { get; set; }
    public string? ThumbnailUrl { get; set; }
    public TimeSpan? Duration { get; set; }

    public MediaKind MediaKind { get; set; }
    public QualityPreset QualityPreset { get; set; }
    public string? FormatSelector { get; set; }
    public VideoContainer? Container { get; set; }
    public AudioFormat? AudioFormat { get; set; }
    public int? AudioBitrateKbps { get; set; }

    public required string OutputDirectory { get; set; }
    public string? OutputPath { get; set; }

    public DownloadStatus Status { get; set; } = DownloadStatus.Waiting;
    public double ProgressPercent { get; set; }
    public long DownloadedBytes { get; set; }
    public long? TotalBytes { get; set; }

    public DownloadErrorKind ErrorKind { get; set; } = DownloadErrorKind.None;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }

    /// <summary>Position in the queue; lower runs first.</summary>
    public long QueueOrder { get; set; }

    public Guid? PlaylistBatchId { get; set; }
    public PlaylistBatch? PlaylistBatch { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>A copy that can be handed to other threads. The playlist link is not copied.</summary>
    public DownloadJob Clone()
    {
        var copy = (DownloadJob)MemberwiseClone();
        copy.PlaylistBatch = null;
        return copy;
    }

    /// <summary>True when the job will not change again unless the user retries it.</summary>
    public bool IsFinished => Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled;
}
