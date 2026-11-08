using Downora.Core.Enums;
using Downora.Core.Models;

namespace Downora.Core.Interfaces;

public interface ISourceDetector
{
    SourcePlatform Detect(Uri url);

    /// <summary>A canonical form used to spot duplicates (e.g. youtu.be/x and youtube.com/watch?v=x are the same).</summary>
    string Normalize(Uri url);

    bool IsSingleItemInPlaylist(Uri url);
}

public interface IUrlValidator
{
    UrlValidationResult Validate(string? input);
}

/// <summary>Reads a link's metadata and formats without downloading anything.</summary>
public interface IMediaExtractor
{
    Task<AnalysisResult> AnalyzeAsync(UrlValidationResult url, CancellationToken cancellationToken = default);
}

/// <summary>A process to start. Arguments are passed one by one (never through a shell).</summary>
public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan? Timeout = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    string? WorkingDirectory = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

/// <summary>The seam between Downora and external tools. Tests replace it with recorded output.</summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs to completion and returns the captured output. Cancelling kills the whole process tree
    /// and throws <see cref="OperationCanceledException"/>; a timeout kills it and returns <c>TimedOut</c>.
    /// </summary>
    Task<ProcessResult> RunAsync(
        ProcessSpec spec,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default);
}

public sealed record ToolStatus(ToolKind Tool, bool IsInstalled, string? Version, string? Path);

public sealed record ToolInstallProgress(ToolKind Tool, long BytesReceived, long? TotalBytes)
{
    public double? Fraction => TotalBytes is > 0 ? (double)BytesReceived / TotalBytes.Value : null;
}

/// <summary>Finds, installs (download + SHA-256 check) and updates yt-dlp, FFmpeg/FFprobe and Deno.</summary>
public interface IToolManager
{
    /// <summary>The executable to run, or null when the tool isn't installed.</summary>
    string? GetPath(ToolKind tool);

    Task<IReadOnlyList<ToolStatus>> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Copies tools shipped next to the app into the data folder (first run after install). No network.</summary>
    Task PrepareAsync(CancellationToken cancellationToken = default);

    /// <summary>Downloads the official release, verifies its published SHA-256 and installs it. FFmpeg also installs FFprobe.</summary>
    Task InstallAsync(ToolKind tool, IProgress<ToolInstallProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Runs yt-dlp's own updater and returns the version afterwards.</summary>
    Task<string?> UpdateYtDlpAsync(CancellationToken cancellationToken = default);
}

/// <summary>Installing a tool failed (download error, checksum mismatch, unsupported platform).</summary>
public sealed class ToolInstallException : Exception
{
    public ToolInstallException()
    {
    }

    public ToolInstallException(string message) : base(message)
    {
    }

    public ToolInstallException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
