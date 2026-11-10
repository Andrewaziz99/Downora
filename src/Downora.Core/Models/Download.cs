using Downora.Core.Enums;

namespace Downora.Core.Models;

/// <summary>Where a running download is, as far as the user is concerned.</summary>
public enum DownloadStage
{
    /// <summary>The tool is starting and reading the page; no bytes yet.</summary>
    Starting,

    Downloading,

    /// <summary>Video and audio are being joined into one file.</summary>
    Merging,

    /// <summary>Audio extraction, moving the finished file and other clean-up.</summary>
    PostProcessing,

    /// <summary>The finished file is being checked with FFprobe.</summary>
    Verifying,
}

/// <summary>A snapshot of a running download. Percent is overall (across the video and audio streams), 0–100.</summary>
public sealed record DownloadProgress(
    DownloadStage Stage,
    double Percent,
    long DownloadedBytes,
    long? TotalBytes,
    double? BytesPerSecond,
    TimeSpan? Eta);

/// <summary>Everything the engine needs to download one item.</summary>
public sealed record DownloadRequest
{
    /// <summary>The job this download belongs to. Also names the job's private temporary folder.</summary>
    public required Guid JobId { get; init; }

    public required string Url { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Video;

    public QualityPreset Quality { get; init; } = QualityPreset.Best;

    public VideoContainer Container { get; init; } = VideoContainer.Mp4;

    public AudioFormat AudioFormat { get; init; } = AudioFormat.Mp3;

    /// <summary>Target bitrate for lossy audio formats.</summary>
    public int AudioBitrateKbps { get; init; } = 320;

    public required string OutputDirectory { get; init; }

    /// <summary>See <see cref="Services.FileNames.Expand"/>. The default is just the title.</summary>
    public string FileNameTemplate { get; init; } = Services.FileNames.DefaultTemplate;

    public string? Title { get; init; }

    public string? Uploader { get; init; }

    /// <summary>Size from the analysis, used to check free space and to scale progress. Null when unknown.</summary>
    public long? EstimatedBytes { get; init; }

    /// <summary>The browser (yt-dlp name, e.g. "edge") whose sign-in to use, or null for none.</summary>
    public string? CookieBrowser { get; init; }

    /// <summary>Write title, artist and the like into the file's tags.</summary>
    public bool EmbedMetadata { get; init; }

    /// <summary>Embed the thumbnail as cover art, for formats that can hold it.</summary>
    public bool EmbedCoverArt { get; init; }

    /// <summary>The length the analysis reported; the finished file is checked against it. Null when unknown.</summary>
    public TimeSpan? ExpectedDuration { get; init; }
}

/// <summary>How a download ended, when it wasn't cancelled.</summary>
public sealed record DownloadResult
{
    private DownloadResult()
    {
    }

    public bool Succeeded { get; private init; }

    public string? FilePath { get; private init; }

    public long? FileSize { get; private init; }

    public DownloadErrorKind ErrorKind { get; private init; }

    /// <summary>The tool's own message (for details and logs).</summary>
    public string? ErrorDetail { get; private init; }

    public static DownloadResult Success(string filePath, long fileSize) =>
        new() { Succeeded = true, FilePath = filePath, FileSize = fileSize };

    public static DownloadResult Failure(DownloadErrorKind kind, string? detail = null) =>
        new() { ErrorKind = kind, ErrorDetail = detail };
}

/// <summary>What the user chose to download: the item, the quality and the format.</summary>
public sealed record NewDownload
{
    public required string SourceUrl { get; init; }

    public required string NormalizedUrl { get; init; }

    public SourcePlatform Platform { get; init; }

    public string? Title { get; init; }

    public string? Uploader { get; init; }

    public string? ThumbnailUrl { get; init; }

    public TimeSpan? Duration { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Video;

    public QualityPreset Quality { get; init; } = QualityPreset.Best;

    public VideoContainer Container { get; init; } = VideoContainer.Mp4;

    public AudioFormat AudioFormat { get; init; } = AudioFormat.Mp3;

    public int AudioBitrateKbps { get; init; } = 320;

    /// <summary>The size the analysis predicted, or null.</summary>
    public long? EstimatedBytes { get; init; }
}

/// <summary>The job a start request produced, and whether it is an existing one that was already queued or running.</summary>
public sealed record DownloadStartResult(DownloadJob Job, bool WasAlreadyActive);

/// <summary>A change to a job, sent to the screens that show it. <see cref="Job"/> is a private copy, safe to read on any thread.</summary>
public sealed record DownloadJobUpdate(DownloadJob Job, DownloadProgress? Progress);
