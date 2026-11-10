using Downora.Core.Enums;

namespace Downora.Core.Models;

/// <summary>One downloadable stream as reported by yt-dlp (a video-only, audio-only or combined format).</summary>
public sealed record MediaFormat(
    string FormatId,
    string? Extension,
    int? Width,
    int? Height,
    double? Fps,
    string? VideoCodec,
    string? AudioCodec,
    double? TotalBitrateKbps,
    double? AudioBitrateKbps,
    long? FileSize,
    bool IsSizeApproximate,
    string? Protocol)
{
    public bool HasVideo => !string.IsNullOrEmpty(VideoCodec) && VideoCodec != "none";

    public bool HasAudio => !string.IsNullOrEmpty(AudioCodec) && AudioCodec != "none";

    /// <summary>The shorter side, so a 1080×1920 vertical video counts as 1080p.</summary>
    public int? Resolution => (Width, Height) switch
    {
        ({ } w, { } h) => Math.Min(w, h),
        (null, { } h) => h,
        _ => null,
    };
}

/// <summary>A single video or track.</summary>
public sealed record MediaInfo(
    string Id,
    string Title,
    string? Uploader,
    TimeSpan? Duration,
    string? ThumbnailUrl,
    string WebpageUrl,
    SourcePlatform Platform,
    string Extractor,
    bool IsLive,
    IReadOnlyList<MediaFormat> Formats);

/// <summary>One item of a playlist, as listed without opening each item.</summary>
public sealed record PlaylistEntry(
    string Id,
    string Url,
    string Title,
    TimeSpan? Duration,
    string? ThumbnailUrl,
    string? Uploader,
    bool IsAvailable);

public sealed record PlaylistInfo(
    string Id,
    string Title,
    string? Uploader,
    string WebpageUrl,
    SourcePlatform Platform,
    int? TotalCount,
    IReadOnlyList<PlaylistEntry> Entries);

public enum AnalysisKind
{
    Media,
    Playlist,
    Failed,
}

/// <summary>What analyzing a link produced: a media item, a playlist, or a classified failure.</summary>
public sealed record AnalysisResult
{
    private AnalysisResult(AnalysisKind kind) => Kind = kind;

    public AnalysisKind Kind { get; }
    public MediaInfo? Media { get; private init; }
    public PlaylistInfo? Playlist { get; private init; }
    public DownloadErrorKind ErrorKind { get; private init; }

    /// <summary>The tool's own message (for details and logs), when there is one.</summary>
    public string? ErrorDetail { get; private init; }

    public static AnalysisResult ForMedia(MediaInfo media) => new(AnalysisKind.Media) { Media = media };

    public static AnalysisResult ForPlaylist(PlaylistInfo playlist) => new(AnalysisKind.Playlist) { Playlist = playlist };

    public static AnalysisResult Failed(DownloadErrorKind kind, string? detail = null) =>
        new(AnalysisKind.Failed) { ErrorKind = kind, ErrorDetail = detail };
}

/// <summary>Why a URL was rejected before anything was run.</summary>
public enum UrlProblem
{
    None,
    Empty,
    NotAUrl,
    UnsupportedScheme,
    TooLong,

    /// <summary>The link contains a user name or password (https://user:pass@host/...). It would be saved and logged in the clear.</summary>
    HasCredentials,
}

public sealed record UrlValidationResult(
    UrlProblem Problem,
    Uri? Url = null,
    string? NormalizedUrl = null,
    SourcePlatform Platform = SourcePlatform.Unknown,
    bool PreferSingleItem = false)
{
    public bool IsValid => Problem == UrlProblem.None && Url is not null;
}

/// <summary>One choice in the quality list, with what the source actually offers for it.</summary>
public sealed record QualityOption(
    QualityPreset Preset,
    bool IsAvailable,
    int? Height,
    double? Fps,
    string? VideoCodec,
    long? EstimatedBytes,
    bool IsEstimateApproximate);
