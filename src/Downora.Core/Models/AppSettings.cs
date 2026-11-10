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

    /// <summary>Set once downloads from before the library existed have been added to it; later removals then stay removed.</summary>
    public bool LibraryBackfilled { get; set; }

    /// <summary>Start Downora when the user signs in to Windows.</summary>
    public bool LaunchAtStartup { get; set; }

    /// <summary>When Downora starts by itself at sign-in, stay in the tray instead of opening the window.</summary>
    public bool StartMinimized { get; set; }

    /// <summary>Closing the window hides it to the tray and downloads keep running; "Exit" in the tray menu really quits.</summary>
    public bool CloseToTray { get; set; }

    /// <summary>When yt-dlp was last checked for an update automatically. Limits the check to once a day.</summary>
    public DateTime? LastToolCheckUtc { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.System;
    public AppLanguage Language { get; set; } = AppLanguage.English;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public static AppSettings CreateDefault(string downloadDirectory) => new() { DownloadDirectory = downloadDirectory };
}
