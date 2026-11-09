using System.Globalization;

namespace Downora.Infrastructure.Downloads;

/// <summary>One line of yt-dlp's machine-readable output, as produced by the templates in <see cref="YtDlpDownloadArguments"/>.</summary>
internal abstract record YtDlpLine;

/// <summary>Progress of one stream (video, audio or a combined file). Absent values are null.</summary>
internal sealed record DownloadLine(
    string Status,
    long DownloadedBytes,
    long? TotalBytes,
    bool IsTotalEstimated,
    double? BytesPerSecond,
    int? EtaSeconds,
    int? FragmentIndex,
    int? FragmentCount,
    string StreamId) : YtDlpLine
{
    public bool IsFinished => Status == "finished";
}

/// <summary>A post-processing step (Merger, ExtractAudio, MoveFiles…) starting or finishing.</summary>
internal sealed record PostProcessLine(string Status, string Processor) : YtDlpLine
{
    public bool IsStarted => Status == "started";
}

/// <summary>The path of the finished file, printed once the download has been moved into place.</summary>
internal sealed record FileLine(string Path) : YtDlpLine;

/// <summary>Turns yt-dlp output lines into structured values. Pure: no IO, no state, so it is tested against captured output.</summary>
internal static class YtDlpProgressParser
{
    public const string DownloadPrefix = "DWN|";
    public const string PostProcessPrefix = "PP|";
    public const string FilePrefix = "FILE|";

    /// <summary>The line yt-dlp prints for each progress update; fields are separated by "|".</summary>
    public const string DownloadTemplate =
        "download:" + DownloadPrefix +
        "%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|" +
        "%(progress.speed)s|%(progress.eta)s|%(progress.fragment_index)s|%(progress.fragment_count)s|%(info.format_id)s";

    public const string PostProcessTemplate = "postprocess:" + PostProcessPrefix + "%(progress.status)s|%(progress.postprocessor)s";

    public const string FileTemplate = "after_move:" + FilePrefix + "%(filepath)s";

    private const string Absent = "NA";

    /// <summary>Returns the parsed line, or null for anything else (warnings, blank lines, unknown output).</summary>
    public static YtDlpLine? Parse(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        if (line.StartsWith(DownloadPrefix, StringComparison.Ordinal))
        {
            return ParseDownload(line[DownloadPrefix.Length..]);
        }

        if (line.StartsWith(PostProcessPrefix, StringComparison.Ordinal))
        {
            var parts = line[PostProcessPrefix.Length..].Split('|');
            return parts.Length >= 2 ? new PostProcessLine(parts[0], parts[1]) : null;
        }

        if (line.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            var path = line[FilePrefix.Length..].Trim();
            return path.Length > 0 ? new FileLine(path) : null;
        }

        return null;
    }

    private static DownloadLine? ParseDownload(string fields)
    {
        // status | downloaded | total | total estimate | speed | eta | fragment index | fragment count | stream id
        var parts = fields.Split('|');
        if (parts.Length < 9 || Long(parts[1]) is not { } downloaded)
        {
            return null;
        }

        var exactTotal = Long(parts[2]);
        var total = exactTotal ?? Long(parts[3]);
        return new DownloadLine(
            parts[0],
            downloaded,
            total is > 0 ? total : null,
            exactTotal is null && total is not null,
            Double(parts[4]),
            Double(parts[5]) is { } eta ? (int)Math.Min(eta, int.MaxValue) : null,
            Int(parts[6]),
            Int(parts[7]),
            parts[8]);
    }

    // yt-dlp prints speeds and sizes as floats ("52913.2351"), counts as integers, and "NA" when it doesn't know.
    private static double? Double(string text) =>
        text != Absent && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value >= 0
            ? value
            : null;

    private static long? Long(string text) => Double(text) is { } value ? (long)Math.Min(value, long.MaxValue) : null;

    private static int? Int(string text) => Double(text) is { } value ? (int)Math.Min(value, int.MaxValue) : null;
}
