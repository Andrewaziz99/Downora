using System.Globalization;
using Downora.Core.Enums;

namespace Downora.Core.Services;

/// <summary>Human-readable sizes, speeds and durations. Uses binary units (1 KB = 1024 B), like Windows Explorer.</summary>
public static class Formatting
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 0)
        {
            return "—";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit == 0 || value >= 100 ? "0" : value >= 10 ? "0.#" : "0.##";
        return $"{value.ToString(format, culture)} {Units[unit]}";
    }

    public static string Speed(double bytesPerSecond, CultureInfo? culture = null) =>
        bytesPerSecond < 0 ? "—" : $"{Bytes((long)bytesPerSecond, culture)}/s";

    /// <summary>"4:05", "1:02:09"; null when unknown.</summary>
    public static string? Duration(TimeSpan? duration, CultureInfo? culture = null)
    {
        if (duration is not { } d || d < TimeSpan.Zero)
        {
            return null;
        }

        culture ??= CultureInfo.CurrentCulture;
        return d.TotalHours >= 1
            ? string.Create(culture, $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}")
            : string.Create(culture, $"{d.Minutes}:{d.Seconds:00}");
    }

    /// <summary>Short quality label such as "1080p", "4K" or "Audio".</summary>
    public static string Quality(QualityPreset preset) => preset switch
    {
        QualityPreset.Best => "Best",
        QualityPreset.P2160 => "4K",
        QualityPreset.P1440 => "1440p",
        QualityPreset.P1080 => "1080p",
        QualityPreset.P720 => "720p",
        QualityPreset.P480 => "480p",
        QualityPreset.P360 => "360p",
        QualityPreset.AudioOnly => "Audio",
        _ => preset.ToString(),
    };

    /// <summary>The file-type badge for a job: "MP4", "MP3", "FLAC"…</summary>
    public static string FileType(MediaKind kind, VideoContainer? container, AudioFormat? audioFormat) => kind switch
    {
        MediaKind.Audio => (audioFormat ?? AudioFormat.Mp3).ToString().ToUpperInvariant(),
        _ => (container ?? VideoContainer.Mp4).ToString().ToUpperInvariant(),
    };
}
