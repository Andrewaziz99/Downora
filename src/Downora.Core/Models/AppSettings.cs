using Downora.Core.Enums;

namespace Downora.Core.Models;

/// <summary>User settings, stored as a single row (<see cref="SingletonId"/>).</summary>
public class AppSettings
{
    public const int SingletonId = 1;
    public const int MinConcurrentDownloads = 1;
    public const int MaxConcurrentDownloadsLimit = 10;

    /// <summary>Bitrates offered for lossy audio formats.</summary>
    public static IReadOnlyList<int> SupportedAudioBitratesKbps { get; } = [128, 192, 256, 320];

    public int Id { get; set; } = SingletonId;

    public required string DownloadDirectory { get; set; }
    public VideoContainer PreferredVideoContainer { get; set; } = VideoContainer.Mp4;
    public AudioFormat PreferredAudioFormat { get; set; } = AudioFormat.Mp3;
    public QualityPreset DefaultVideoQuality { get; set; } = QualityPreset.Best;
    public int DefaultAudioBitrateKbps { get; set; } = 320;
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>Write title, artist, date and the like into the file's own tags.</summary>
    public bool EmbedMetadata { get; set; } = true;

    /// <summary>Put the thumbnail inside the file as its cover picture (where the format supports it).</summary>
    public bool EmbedCoverArt { get; set; } = true;

    public bool AutoAddToQueue { get; set; }
    public bool AutoStartDownloads { get; set; } = true;

    /// <summary>Finished jobs older than this are pruned from history; null keeps them forever.</summary>
    public int? HistoryRetentionDays { get; set; }

    /// <summary>Optional path to a user-provided ffmpeg.exe; null uses the bundled copy.</summary>
    public string? FfmpegPathOverride { get; set; }
    public bool YtDlpAutoUpdate { get; set; } = true;

    /// <summary>Opt-in: pass the user's own browser cookies to yt-dlp for content their account can access.</summary>
    public bool UseBrowserSession { get; set; }
    public string? BrowserForCookies { get; set; }

    public bool LaunchAtStartup { get; set; }
    public bool StartMinimized { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.System;
    public AppLanguage Language { get; set; } = AppLanguage.English;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public static AppSettings CreateDefault(string downloadDirectory) => new() { DownloadDirectory = downloadDirectory };
}
