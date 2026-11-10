namespace Downora.Core.Enums;

/// <summary>Lifecycle of a download job. Values are persisted by name, so never rename a member.</summary>
public enum DownloadStatus
{
    Waiting,
    Extracting,
    Downloading,
    Processing,
    Completed,
    Failed,
    Cancelled,
    Paused,
}

public enum MediaKind
{
    Video,
    Audio,
}

public enum SourcePlatform
{
    Unknown,
    YouTube,
    YouTubeShorts,
    SoundCloud,
    Vimeo,
    Dailymotion,
    Facebook,
    Instagram,
    X,
    DirectFile,
    Other,
}

public enum QualityPreset
{
    Best,
    P2160,
    P1440,
    P1080,
    P720,
    P480,
    P360,
    AudioOnly,
}

public enum VideoContainer
{
    Mp4,
    Mkv,
    WebM,
}

public enum AudioFormat
{
    Mp3,
    M4a,
    Wav,
    Flac,
    Opus,
}

public enum AppTheme
{
    System,
    Dark,
    Light,
}

public enum AppLanguage
{
    English,
    Arabic,
}

/// <summary>Why a job failed, used to pick the user-facing message and whether an automatic retry makes sense.</summary>
public enum DownloadErrorKind
{
    None,
    Unsupported,
    RequiresSignIn,
    Drm,
    GeoRestricted,
    Unavailable,
    Network,
    ToolMissing,
    DiskFull,
    BrowserCookies,

    /// <summary>The download finished but the file failed the check (unreadable, wrong streams or cut short).</summary>
    CorruptFile,
    Unknown,
}

public enum ToolKind
{
    YtDlp,
    Ffmpeg,
    Ffprobe,
    Deno,
}
