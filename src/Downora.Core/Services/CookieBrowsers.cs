using Downora.Core.Models;

namespace Downora.Core.Services;

/// <summary>
/// The browsers whose own sign-in Downora can reuse (opt-in). Ids are the names yt-dlp's <c>--cookies-from-browser</c> takes.
/// Downora never reads, copies, stores or logs cookie contents: it only tells yt-dlp which browser to ask.
/// </summary>
public static class CookieBrowsers
{
    public const string Edge = "edge";
    public const string Chrome = "chrome";
    public const string Firefox = "firefox";
    public const string Brave = "brave";

    public static IReadOnlyList<string> Supported { get; } = [Edge, Chrome, Firefox, Brave];

    public static bool IsSupported(string? browser) =>
        browser is not null && Supported.Contains(browser, StringComparer.Ordinal);

    /// <summary>The browser to pass to yt-dlp, or null when the option is off or the saved browser isn't one we support.</summary>
    public static string? For(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.UseBrowserSession && IsSupported(settings.BrowserForCookies) ? settings.BrowserForCookies : null;
    }
}
