namespace Downora.Core.Services;

/// <summary>
/// Which files Downora hands to Windows to open. A downloaded file is data from the internet: opening it with
/// "whatever Windows associates" would run it if it were a program or a script, so only known media types are opened
/// and anything else is shown in its folder instead.
/// </summary>
public static class MediaFileTypes
{
    private static readonly HashSet<string> Openable = new(StringComparer.OrdinalIgnoreCase)
    {
        // Video
        ".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".flv", ".wmv", ".ts", ".mpg", ".mpeg", ".3gp", ".ogv",
        // Audio
        ".mp3", ".m4a", ".aac", ".opus", ".ogg", ".oga", ".flac", ".wav", ".wma", ".weba",
    };

    public static bool IsOpenable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Openable.Contains(Path.GetExtension(path.AsSpan().Trim().ToString()));
}
