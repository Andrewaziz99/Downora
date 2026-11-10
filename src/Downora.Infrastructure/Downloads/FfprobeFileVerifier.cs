using System.Globalization;
using System.Text.Json;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Services;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Downloads;

/// <summary>Opens a finished file with FFprobe and applies <see cref="FileVerificationRules"/>.</summary>
internal sealed partial class FfprobeFileVerifier(IToolManager tools, IProcessRunner runner, ILogger<FfprobeFileVerifier> logger) : IMediaFileVerifier, IMediaProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(60);

    public async Task<FileVerification> VerifyAsync(string path, MediaKind kind, TimeSpan? expectedDuration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // A missing FFprobe is not the file's fault: the download stays as it is.
        if (tools.GetPath(ToolKind.Ffprobe) is not { } ffprobe)
        {
            LogSkipped(path);
            return FileVerification.Skipped;
        }

        var spec = new ProcessSpec(ffprobe, Arguments(path), ProbeTimeout);
        var result = await runner.RunAsync(spec, null, null, cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            // Slow disks or antivirus can stall a probe; that says nothing about the file.
            LogTimedOut(path);
            return FileVerification.Skipped;
        }

        if (result.ExitCode != 0)
        {
            LogUnreadable(path, result.ExitCode, result.StandardError);
            return FileVerification.Bad("The file can't be opened.");
        }

        if (Parse(result.StandardOutput) is not { } probe)
        {
            LogUnreadable(path, result.ExitCode, result.StandardOutput);
            return FileVerification.Bad("The file can't be opened.");
        }

        var verdict = FileVerificationRules.Evaluate(probe, kind, expectedDuration);
        if (!verdict.IsValid)
        {
            LogRejected(path, verdict.Problem);
        }

        return verdict;
    }

    public async Task<MediaProbe?> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (tools.GetPath(ToolKind.Ffprobe) is not { } ffprobe)
        {
            return null;
        }

        var result = await runner.RunAsync(new ProcessSpec(ffprobe, Arguments(path), ProbeTimeout), null, null, cancellationToken).ConfigureAwait(false);
        return result is { ExitCode: 0, TimedOut: false } ? Parse(result.StandardOutput) : null;
    }

    internal static IReadOnlyList<string> Arguments(string path) =>
    [
        "-v", "error",
        "-print_format", "json",
        "-show_entries", "format=duration,bit_rate:stream=codec_type,codec_name,width,height:stream_disposition=attached_pic",
        "-i", path,
    ];

    /// <summary>Reads FFprobe's JSON. Returns null when it isn't the expected document.</summary>
    internal static MediaProbe? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var hasVideo = false;
            var hasAudio = false;
            int? width = null;
            int? height = null;
            string? videoCodec = null;
            string? audioCodec = null;
            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    var type = stream.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
                    var codec = stream.TryGetProperty("codec_name", out var c) ? c.GetString() : null;
                    hasVideo |= type == "video";
                    hasAudio |= type == "audio";

                    // An embedded cover is a "video" stream too; it must not stand in for the video's own size and codec.
                    var isCover = stream.TryGetProperty("disposition", out var disposition)
                                  && disposition.TryGetProperty("attached_pic", out var pic) && pic.ValueKind == JsonValueKind.Number && pic.GetInt32() == 1;
                    if (type == "video" && !isCover && videoCodec is null)
                    {
                        videoCodec = codec;
                        width = stream.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : null;
                        height = stream.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : null;
                    }
                    else if (type == "audio" && audioCodec is null)
                    {
                        audioCodec = codec;
                    }
                }
            }

            int? bitrate = null;
            if (root.TryGetProperty("format", out var fmt)
                && fmt.TryGetProperty("bit_rate", out var br)
                && long.TryParse(br.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bps)
                && bps > 0)
            {
                bitrate = (int)Math.Min(int.MaxValue, bps / 1000);
            }

            TimeSpan? duration = null;
            if (root.TryGetProperty("format", out var format)
                && format.TryGetProperty("duration", out var d)
                && double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                && double.IsFinite(seconds)
                && seconds < TimeSpan.MaxValue.TotalSeconds)
            {
                duration = TimeSpan.FromSeconds(seconds);
            }

            return new MediaProbe(duration, hasVideo, hasAudio, width, height, videoCodec, audioCodec, bitrate);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "FFprobe isn't installed; not checking {Path}")]
    private partial void LogSkipped(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FFprobe timed out on {Path}; not checking it")]
    private partial void LogTimedOut(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "FFprobe could not read {Path} (exit {ExitCode}): {Output}")]
    private partial void LogUnreadable(string path, int exitCode, string output);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Path} failed the check: {Problem}")]
    private partial void LogRejected(string path, string? problem);
}
