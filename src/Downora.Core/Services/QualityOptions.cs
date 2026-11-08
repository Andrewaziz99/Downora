using Downora.Core.Enums;
using Downora.Core.Models;

namespace Downora.Core.Services;

/// <summary>Works out which quality presets a media item really offers and roughly how big each download is.</summary>
public static class QualityOptions
{
    /// <summary>Preset → the resolution band it covers (exclusive lower, inclusive upper bound).</summary>
    private static readonly (QualityPreset Preset, int Lower, int Upper)[] Bands =
    [
        (QualityPreset.P2160, 1440, int.MaxValue),
        (QualityPreset.P1440, 1080, 1440),
        (QualityPreset.P1080, 720, 1080),
        (QualityPreset.P720, 480, 720),
        (QualityPreset.P480, 360, 480),
        (QualityPreset.P360, 0, 360),
    ];

    public static IReadOnlyList<QualityOption> Build(MediaInfo media)
    {
        ArgumentNullException.ThrowIfNull(media);
        var video = media.Formats.Where(f => f.HasVideo && f.Resolution is > 0).ToList();
        var audio = BestAudio(media);
        var options = new List<QualityOption>();

        var best = BestVideo(video);
        options.Add(best is null
            ? new QualityOption(QualityPreset.Best, audio is not null, null, null, null, Estimate(null, audio, media.Duration), IsApprox(null, audio))
            : Option(QualityPreset.Best, best, audio, media.Duration));

        foreach (var (preset, lower, upper) in Bands)
        {
            var inBand = BestVideo(video.Where(f => f.Resolution > lower && f.Resolution <= upper));
            options.Add(inBand is null
                ? new QualityOption(preset, false, null, null, null, null, false)
                : Option(preset, inBand, audio, media.Duration));
        }

        options.Add(new QualityOption(
            QualityPreset.AudioOnly,
            audio is not null,
            null,
            null,
            null,
            Estimate(null, audio, media.Duration),
            IsApprox(null, audio)));

        return options;
    }

    /// <summary>The best audio stream: audio-only formats first (what a merge would use), by bitrate.</summary>
    public static MediaFormat? BestAudio(MediaInfo media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return media.Formats.Where(f => f.HasAudio && !f.HasVideo)
                   .OrderByDescending(f => f.AudioBitrateKbps ?? f.TotalBitrateKbps ?? 0)
                   .ThenByDescending(f => f.FileSize ?? 0)
                   .FirstOrDefault()
               ?? media.Formats.Where(f => f.HasAudio)
                   .OrderByDescending(f => f.AudioBitrateKbps ?? 0)
                   .FirstOrDefault();
    }

    private static MediaFormat? BestVideo(IEnumerable<MediaFormat> formats) =>
        formats.OrderByDescending(f => f.Resolution)
            .ThenByDescending(f => f.Fps ?? 0)
            .ThenByDescending(f => f.FileSize is not null) // prefer streams with a known size for a better estimate
            .ThenByDescending(f => f.TotalBitrateKbps ?? 0)
            .FirstOrDefault();

    private static QualityOption Option(QualityPreset preset, MediaFormat video, MediaFormat? audio, TimeSpan? duration)
    {
        // A combined format already contains its audio.
        var extraAudio = video.HasAudio ? null : audio;
        return new QualityOption(
            preset,
            true,
            video.Resolution,
            video.Fps,
            ShortCodec(video.VideoCodec),
            Estimate(video, extraAudio, duration),
            IsApprox(video, extraAudio));
    }

    private static long? Estimate(MediaFormat? video, MediaFormat? audio, TimeSpan? duration)
    {
        long? Size(MediaFormat? f) => f switch
        {
            null => 0,
            { FileSize: { } size } => size,
            { TotalBitrateKbps: { } kbps } when duration is { } d => (long)(kbps * 1000 / 8 * d.TotalSeconds),
            _ => null,
        };

        var v = Size(video);
        var a = Size(audio);
        if (video is null && audio is null)
        {
            return null;
        }

        return v is null || a is null ? null : v + a;
    }

    private static bool IsApprox(MediaFormat? video, MediaFormat? audio) =>
        (video is not null && (video.FileSize is null || video.IsSizeApproximate)) ||
        (audio is not null && (audio.FileSize is null || audio.IsSizeApproximate));

    /// <summary>"avc1.4D400B" → "H.264", "vp09.00…" → "VP9", "av01…" → "AV1".</summary>
    public static string? ShortCodec(string? codec) => codec?.ToLowerInvariant() switch
    {
        null or "none" => null,
        var c when c.StartsWith("avc", StringComparison.Ordinal) || c.StartsWith("h264", StringComparison.Ordinal) => "H.264",
        var c when c.StartsWith("hev", StringComparison.Ordinal) || c.StartsWith("hvc", StringComparison.Ordinal) || c.StartsWith("h265", StringComparison.Ordinal) => "H.265",
        var c when c.StartsWith("vp09", StringComparison.Ordinal) || c.StartsWith("vp9", StringComparison.Ordinal) => "VP9",
        var c when c.StartsWith("av01", StringComparison.Ordinal) => "AV1",
        var c when c.StartsWith("mp4a", StringComparison.Ordinal) => "AAC",
        "opus" => "Opus",
        var c => c.Split('.')[0].ToUpperInvariant(),
    };
}
