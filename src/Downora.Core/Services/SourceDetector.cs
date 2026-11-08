using System.Globalization;
using Downora.Core.Enums;
using Downora.Core.Interfaces;

namespace Downora.Core.Services;

/// <summary>Recognizes the platform behind a URL and builds a canonical form for duplicate detection.</summary>
public sealed class SourceDetector : ISourceDetector
{
    private static readonly string[] MediaExtensions =
        [".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".opus"];

    // Query parameters that only track where a link was shared from.
    private static readonly string[] TrackingParameters = ["si", "feature", "fbclid", "gclid", "igshid", "ref_src", "pp"];

    public SourcePlatform Detect(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var host = Host(url);

        if (IsHost(host, "youtube.com") || IsHost(host, "youtube-nocookie.com") || host == "youtu.be")
        {
            return url.AbsolutePath.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase)
                ? SourcePlatform.YouTubeShorts
                : SourcePlatform.YouTube;
        }

        if (IsHost(host, "soundcloud.com"))
        {
            return SourcePlatform.SoundCloud;
        }

        if (IsHost(host, "vimeo.com"))
        {
            return SourcePlatform.Vimeo;
        }

        if (IsHost(host, "dailymotion.com") || host == "dai.ly")
        {
            return SourcePlatform.Dailymotion;
        }

        if (IsHost(host, "facebook.com") || host == "fb.watch")
        {
            return SourcePlatform.Facebook;
        }

        if (IsHost(host, "instagram.com"))
        {
            return SourcePlatform.Instagram;
        }

        if (IsHost(host, "x.com") || IsHost(host, "twitter.com"))
        {
            return SourcePlatform.X;
        }

        var extension = Path.GetExtension(url.AbsolutePath);
        if (MediaExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return SourcePlatform.DirectFile;
        }

        return SourcePlatform.Other;
    }

    public string Normalize(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var platform = Detect(url);

        if (platform is SourcePlatform.YouTube or SourcePlatform.YouTubeShorts && YouTubeVideoId(url) is { } videoId)
        {
            return $"https://www.youtube.com/watch?v={videoId}";
        }

        if (platform == SourcePlatform.YouTube && QueryValue(url, "list") is { Length: > 0 } listId)
        {
            return $"https://www.youtube.com/playlist?list={listId}";
        }

        var query = string.Join('&', ParseQuery(url)
            .Where(p => !TrackingParameters.Contains(p.Key, StringComparer.OrdinalIgnoreCase)
                        && !p.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Value is null ? p.Key : $"{p.Key}={p.Value}"));

        var path = url.AbsolutePath.Length > 1 ? url.AbsolutePath.TrimEnd('/') : string.Empty;
        var port = url.IsDefaultPort ? string.Empty : $":{url.Port.ToString(CultureInfo.InvariantCulture)}";
        return $"{url.Scheme.ToLowerInvariant()}://{Host(url)}{port}{path}{(query.Length > 0 ? "?" + query : string.Empty)}";
    }

    /// <summary>True for a YouTube watch link that also carries a playlist: the user pasted one video, so analyze just that.</summary>
    public bool IsSingleItemInPlaylist(Uri url) =>
        Detect(url) is SourcePlatform.YouTube or SourcePlatform.YouTubeShorts
        && YouTubeVideoId(url) is not null
        && QueryValue(url, "list") is not null;

    internal static string? YouTubeVideoId(Uri url)
    {
        var host = Host(url);
        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var candidate = host == "youtu.be"
            ? segments.FirstOrDefault()
            : segments.Length >= 2 && segments[0] is "shorts" or "embed" or "live" or "v"
                ? segments[1]
                : QueryValue(url, "v");

        return candidate is { Length: 11 } && candidate.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? candidate
            : null;
    }

    private static string Host(Uri url) => url.Host.ToLowerInvariant();

    private static bool IsHost(string host, string domain) =>
        host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

    private static string? QueryValue(Uri url, string key) =>
        ParseQuery(url).FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal)).Value;

    private static IEnumerable<KeyValuePair<string, string?>> ParseQuery(Uri url)
    {
        var query = url.Query.TrimStart('?');
        if (query.Length == 0)
        {
            yield break;
        }

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            yield return eq < 0
                ? new KeyValuePair<string, string?>(part, null)
                : new KeyValuePair<string, string?>(part[..eq], part[(eq + 1)..]);
        }
    }
}
