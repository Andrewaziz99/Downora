using System.Text.RegularExpressions;
using Downora.Core.Enums;

namespace Downora.Infrastructure.Tools;

/// <summary>Where each tool comes from: the official GitHub release and its published SHA-256 checksum file.</summary>
internal sealed record ToolSource(
    Uri DownloadUrl,
    Uri ChecksumUrl,
    string ChecksumFileName,
    /// <summary>Null when the download is the executable itself; otherwise which file in the zip becomes which tool.</summary>
    IReadOnlyList<ZipEntryMapping>? ZipContents);

internal sealed record ZipEntryMapping(ToolKind Tool, string EntryFileName);

internal static partial class ToolCatalog
{
    public static string ExecutableName(ToolKind tool)
    {
        var name = tool switch
        {
            ToolKind.YtDlp => "yt-dlp",
            ToolKind.Ffmpeg => "ffmpeg",
            ToolKind.Ffprobe => "ffprobe",
            ToolKind.Deno => "deno",
            _ => throw new ArgumentOutOfRangeException(nameof(tool)),
        };
        return OperatingSystem.IsWindows() ? name + ".exe" : name;
    }

    /// <summary>Windows x64 releases. Downora's installer bundles the same files.</summary>
    public static ToolSource? SourceFor(ToolKind tool) => tool switch
    {
        ToolKind.YtDlp => new ToolSource(
            new Uri("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"),
            new Uri("https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS"),
            "yt-dlp.exe",
            null),
        ToolKind.Ffmpeg or ToolKind.Ffprobe => new ToolSource(
            new Uri("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip"),
            new Uri("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256"),
            "ffmpeg-master-latest-win64-lgpl.zip",
            [new(ToolKind.Ffmpeg, "ffmpeg.exe"), new(ToolKind.Ffprobe, "ffprobe.exe")]),
        ToolKind.Deno => new ToolSource(
            new Uri("https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip"),
            new Uri("https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip.sha256sum"),
            "deno-x86_64-pc-windows-msvc.zip",
            [new(ToolKind.Deno, "deno.exe")]),
        _ => null,
    };

    /// <summary>
    /// Finds the SHA-256 for <paramref name="fileName"/> in a checksum file. Handles "hash  name" lists (yt-dlp, FFmpeg),
    /// PowerShell Get-FileHash output (Deno) and a file holding a single hash. Returns lowercase hex, or null.
    /// </summary>
    public static string? ParseChecksum(string text, string fileName)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var match = HashThenName().Match(line);
            if (match.Success && string.Equals(match.Groups["name"].Value, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["hash"].Value.ToLowerInvariant();
            }
        }

        var listed = lines.Count(l => HashThenName().IsMatch(l));
        if (listed > 0)
        {
            return null; // A list that doesn't mention our file: don't guess.
        }

        var hashes = lines.SelectMany(l => AnyHash().Matches(l)).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return hashes.Count == 1 ? hashes[0].ToLowerInvariant() : null;
    }

    [GeneratedRegex(@"^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<name>\S+)$")]
    private static partial Regex HashThenName();

    [GeneratedRegex(@"\b[0-9a-fA-F]{64}\b")]
    private static partial Regex AnyHash();
}
