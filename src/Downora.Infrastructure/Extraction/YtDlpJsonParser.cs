using System.Text.Json;
using Downora.Core.Enums;
using Downora.Core.Models;

namespace Downora.Infrastructure.Extraction;

/// <summary>
/// Reads the JSON yt-dlp prints with <c>-J --flat-playlist</c>. Fields are optional and loosely typed
/// (numbers can be ints, floats or null), so every read is defensive.
/// </summary>
internal static class YtDlpJsonParser
{
    public static AnalysisResult Parse(string json, SourcePlatform detectedPlatform)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return AnalysisResult.Failed(DownloadErrorKind.Unknown, "yt-dlp returned no information for this link.");
        }

        var platform = PlatformFrom(String(root, "extractor_key"), detectedPlatform);
        var type = String(root, "_type");

        return type is "playlist" or "multi_video"
            ? AnalysisResult.ForPlaylist(ParsePlaylist(root, platform))
            : AnalysisResult.ForMedia(ParseMedia(root, platform));
    }

    private static MediaInfo ParseMedia(JsonElement root, SourcePlatform platform)
    {
        var formats = new List<MediaFormat>();
        if (root.TryGetProperty("formats", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in list.EnumerateArray())
            {
                if (ParseFormat(f) is { } format)
                {
                    formats.Add(format);
                }
            }
        }

        var id = String(root, "id") ?? string.Empty;
        return new MediaInfo(
            id,
            String(root, "title") ?? String(root, "fulltitle") ?? id,
            String(root, "uploader") ?? String(root, "channel") ?? String(root, "creator"),
            Seconds(root, "duration"),
            String(root, "thumbnail") ?? LastThumbnail(root),
            String(root, "webpage_url") ?? String(root, "original_url") ?? string.Empty,
            platform,
            String(root, "extractor_key") ?? String(root, "extractor") ?? "generic",
            Bool(root, "is_live") || String(root, "live_status") is "is_live" or "is_upcoming",
            formats);
    }

    private static MediaFormat? ParseFormat(JsonElement f)
    {
        var id = String(f, "format_id");
        if (id is null)
        {
            return null;
        }

        var exactSize = Long(f, "filesize");
        var approxSize = Long(f, "filesize_approx");
        var vcodec = String(f, "vcodec");
        var acodec = String(f, "acodec");

        // Storyboards ("mhtml" image strips) and the like have neither audio nor video.
        if (vcodec is null or "none" && acodec is null or "none" && String(f, "ext") is "mhtml")
        {
            return null;
        }

        return new MediaFormat(
            id,
            String(f, "ext"),
            Int(f, "width"),
            Int(f, "height"),
            Double(f, "fps"),
            vcodec,
            acodec,
            Double(f, "tbr"),
            Double(f, "abr"),
            exactSize ?? approxSize,
            exactSize is null && approxSize is not null,
            String(f, "protocol"));
    }

    private static PlaylistInfo ParsePlaylist(JsonElement root, SourcePlatform platform)
    {
        var entries = new List<PlaylistEntry>();
        if (root.TryGetProperty("entries", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in list.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = String(e, "id") ?? string.Empty;
                var url = String(e, "url") ?? String(e, "webpage_url") ?? string.Empty;
                var title = String(e, "title") ?? id;

                // YouTube lists removed items as "[Private video]" / "[Deleted video]" with no duration.
                var unavailable = url.Length == 0 || (title.StartsWith('[') && title.EndsWith(']') && Seconds(e, "duration") is null);
                entries.Add(new PlaylistEntry(
                    id,
                    url,
                    title,
                    Seconds(e, "duration"),
                    String(e, "thumbnail") ?? LastThumbnail(e),
                    String(e, "uploader") ?? String(e, "channel"),
                    !unavailable));
            }
        }

        return new PlaylistInfo(
            String(root, "id") ?? string.Empty,
            String(root, "title") ?? String(root, "id") ?? string.Empty,
            String(root, "uploader") ?? String(root, "channel"),
            String(root, "webpage_url") ?? String(root, "original_url") ?? string.Empty,
            platform,
            Int(root, "playlist_count") ?? entries.Count,
            entries);
    }

    private static SourcePlatform PlatformFrom(string? extractorKey, SourcePlatform detected) => extractorKey switch
    {
        "Youtube" or "YoutubeTab" or "YoutubePlaylist" => detected == SourcePlatform.YouTubeShorts ? detected : SourcePlatform.YouTube,
        "Soundcloud" or "SoundcloudSet" or "SoundcloudPlaylist" or "SoundcloudUser" => SourcePlatform.SoundCloud,
        "Vimeo" or "VimeoShowcase" or "VimeoAlbum" => SourcePlatform.Vimeo,
        "Dailymotion" or "DailymotionPlaylist" => SourcePlatform.Dailymotion,
        "Facebook" => SourcePlatform.Facebook,
        "Instagram" => SourcePlatform.Instagram,
        "Twitter" => SourcePlatform.X,
        _ => detected,
    };

    private static string? LastThumbnail(JsonElement element) =>
        element.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array && thumbs.GetArrayLength() > 0
            ? String(thumbs[thumbs.GetArrayLength() - 1], "url")
            : null;

    // Every read first checks that the element is an object: TryGetProperty throws on arrays, strings and null, and
    // yt-dlp's loosely typed output (or a damaged pipe) can put those anywhere.
    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A finite number, or null. Numbers too large for a double (1e999) are "unknown", not an error.</summary>
    private static double? Double(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && double.IsFinite(number)
            ? number
            : null;

    private static int? Int(JsonElement element, string name) =>
        Double(element, name) is { } d && Math.Abs(d) < int.MaxValue ? (int)Math.Round(d) : null;

    private static long? Long(JsonElement element, string name) =>
        Double(element, name) is { } d && Math.Abs(d) < long.MaxValue / 2.0 ? (long)Math.Round(d) : null;

    private static bool Bool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>A duration up to ten years; anything longer or negative is a data error and counts as unknown.</summary>
    private static TimeSpan? Seconds(JsonElement element, string name) =>
        Double(element, name) is { } s and >= 0 && s <= MaxSeconds ? TimeSpan.FromSeconds(s) : null;

    private static readonly double MaxSeconds = TimeSpan.FromDays(3650).TotalSeconds;
}
