using Downora.Core.Enums;

namespace Downora.Core.Services;

/// <summary>Turns yt-dlp's error output into a <see cref="DownloadErrorKind"/> the UI can explain.</summary>
public static class ErrorClassifier
{
    // Checked in order: the first match wins, so more specific causes come first.
    private static readonly (DownloadErrorKind Kind, string[] Phrases)[] Rules =
    [
        // The user's own browser sign-in couldn't be read (browser open and locking its cookie file, no such profile…).
        (DownloadErrorKind.BrowserCookies,
        [
            "cookie database", "cookies database", "failed to decrypt", "could not find firefox cookies", "could not find chrome cookies",
            "could not find edge cookies", "could not find brave cookies", "unsupported browser specified for cookies", "failed to read cookies",
        ]),
        (DownloadErrorKind.Drm,["drm protected", "drm-protected", " drm "]),
        (DownloadErrorKind.RequiresSignIn,
        [
            "sign in to confirm", "login required", "log in to", "private video", "this video is private",
            "members-only", "join this channel", "registered users", "use --cookies", "requires authentication",
            "age-restricted", "confirm your age", "account credentials",
        ]),
        (DownloadErrorKind.GeoRestricted, ["in your country", "geo restrict", "geo-restrict", "not available from your location"]),
        (DownloadErrorKind.Unsupported, ["unsupported url", "no video formats found", "no suitable extractor"]),
        (DownloadErrorKind.Unavailable,
        [
            "video unavailable", "has been removed", "does not exist", "http error 404", "not found", "this video is no longer available",
            "account associated with this video has been terminated",
        ]),
        (DownloadErrorKind.Network,
        [
            "unable to download", "timed out", "timeout", "getaddrinfo failed", "name resolution", "connection reset",
            "connection refused", "connection aborted", "network is unreachable", "http error 5", "http error 429",
            "ssl", "remote end closed",
        ]),
        (DownloadErrorKind.DiskFull, ["no space left", "not enough space", "disk full"]),
    ];

    /// <summary>Classifies the most relevant line: the last "ERROR:" line, or the whole text when there is none.</summary>
    public static (DownloadErrorKind Kind, string? Detail) Classify(string? errorOutput)
    {
        if (string.IsNullOrWhiteSpace(errorOutput))
        {
            return (DownloadErrorKind.Unknown, null);
        }

        var detail = LastErrorLine(errorOutput);
        var haystack = " " + (detail ?? errorOutput).ToLowerInvariant() + " ";

        foreach (var (kind, phrases) in Rules)
        {
            if (phrases.Any(p => haystack.Contains(p, StringComparison.Ordinal)))
            {
                return (kind, detail);
            }
        }

        return (DownloadErrorKind.Unknown, detail);
    }

    /// <summary>"ERROR: [youtube] abc: Video unavailable" → "Video unavailable" (extractor and id prefixes removed).</summary>
    public static string? LastErrorLine(string text)
    {
        var line = text.Split('\n')
            .Select(l => l.Trim())
            .LastOrDefault(l => l.StartsWith("ERROR:", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        var message = line["ERROR:".Length..].Trim();
        if (message.StartsWith('['))
        {
            var close = message.IndexOf(']', StringComparison.Ordinal);
            message = close > 0 ? message[(close + 1)..].Trim() : message;
            var colon = message.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0 && !message[..colon].Contains(' ', StringComparison.Ordinal))
            {
                message = message[(colon + 2)..];
            }
        }

        return message;
    }
}
