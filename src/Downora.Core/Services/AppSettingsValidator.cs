using Downora.Core.Models;

namespace Downora.Core.Services;

/// <summary>Checks settings before they are saved. Returns problems as plain messages; empty means valid.</summary>
public static class AppSettingsValidator
{
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.DownloadDirectory))
        {
            errors.Add("Choose a download folder.");
        }
        else if (!Path.IsPathFullyQualified(settings.DownloadDirectory))
        {
            errors.Add("The download folder must be a full path, such as C:\\Users\\you\\Downloads.");
        }

        if (settings.MaxConcurrentDownloads is < AppSettings.MinConcurrentDownloads or > AppSettings.MaxConcurrentDownloadsLimit)
        {
            errors.Add($"Simultaneous downloads must be between {AppSettings.MinConcurrentDownloads} and {AppSettings.MaxConcurrentDownloadsLimit}.");
        }

        if (!AppSettings.SupportedAudioBitratesKbps.Contains(settings.DefaultAudioBitrateKbps))
        {
            errors.Add($"Audio bitrate must be one of {string.Join(", ", AppSettings.SupportedAudioBitratesKbps)} kbps.");
        }

        if (settings.HistoryRetentionDays is <= 0)
        {
            errors.Add("History retention must be at least 1 day, or empty to keep history forever.");
        }

        if (!string.IsNullOrWhiteSpace(settings.FfmpegPathOverride) && !Path.IsPathFullyQualified(settings.FfmpegPathOverride))
        {
            errors.Add("The FFmpeg path must be a full path to ffmpeg.exe.");
        }

        if (settings.UseBrowserSession && string.IsNullOrWhiteSpace(settings.BrowserForCookies))
        {
            errors.Add("Pick which browser's session to use, or turn the option off.");
        }
        else if (!string.IsNullOrWhiteSpace(settings.BrowserForCookies) && !CookieBrowsers.IsSupported(settings.BrowserForCookies))
        {
            errors.Add($"The browser must be one of {string.Join(", ", CookieBrowsers.Supported)}.");
        }

        return errors;
    }
}

/// <summary>Thrown when settings fail <see cref="AppSettingsValidator"/>.</summary>
public sealed class SettingsValidationException : Exception
{
    public SettingsValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors ?? throw new ArgumentNullException(nameof(errors))))
    {
        Errors = errors;
    }

    public SettingsValidationException() : this([]) { }

    public SettingsValidationException(string message) : base(message) => Errors = [message];

    public SettingsValidationException(string message, Exception innerException) : base(message, innerException) => Errors = [message];

    public IReadOnlyList<string> Errors { get; }
}
