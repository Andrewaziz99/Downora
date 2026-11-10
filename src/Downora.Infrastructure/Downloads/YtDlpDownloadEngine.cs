using System.Globalization;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Infrastructure.Extraction;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Downloads;

/// <summary>Downloads one item with yt-dlp: builds the command, follows its output and turns the outcome into a <see cref="DownloadResult"/>.</summary>
internal sealed partial class YtDlpDownloadEngine(
    IToolManager tools,
    IProcessRunner runner,
    IDiskSpaceService disk,
    IMediaFileVerifier verifier,
    ILogger<YtDlpDownloadEngine> logger) : IDownloadEngine
{
    /// <summary>Hidden folder inside the download folder that holds every running job's partial files.</summary>
    internal const string PartialFolderName = ".downora-partial";

    private const long MinimumFreeBytes = 256L * 1024 * 1024;
    private const long ExtraFreeBytes = 64L * 1024 * 1024;

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Converting or joining streams needs FFmpeg for almost every download, so it is required up front.
        if (tools.GetPath(ToolKind.YtDlp) is not { } ytDlp)
        {
            return DownloadResult.Failure(DownloadErrorKind.ToolMissing, "yt-dlp is not installed.");
        }

        if (tools.GetPath(ToolKind.Ffmpeg) is not { } ffmpeg)
        {
            return DownloadResult.Failure(DownloadErrorKind.ToolMissing, "FFmpeg is not installed.");
        }

        if (CheckFreeSpace(request) is { } notEnoughSpace)
        {
            return notEnoughSpace;
        }

        string baseName;
        try
        {
            Directory.CreateDirectory(request.OutputDirectory);
            baseName = ChooseBaseName(request);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFolderUnavailable(request.OutputDirectory, ex);
            return DownloadResult.Failure(DownloadErrorKind.Unknown, ex.Message);
        }

        var layout = new DownloadLayout(ffmpeg, tools.GetPath(ToolKind.Deno), request.OutputDirectory, TempDirectoryFor(request), baseName);
        var arguments = YtDlpDownloadArguments.Build(request, layout);
        var tracker = new ProgressTracker(request.EstimatedBytes);
        string? finishedPath = null;
        var gate = new Lock();

        // yt-dlp prints download progress on stdout but post-processing steps on stderr, and the two arrive on
        // different threads, so both go through the same handler under a lock.
        void OnLine(string text)
        {
            lock (gate)
            {
                switch (YtDlpProgressParser.Parse(text))
                {
                    case FileLine file:
                        finishedPath = file.Path;
                        break;
                    case { } line when tracker.Apply(line) is { } snapshot:
                        progress?.Report(snapshot);
                        break;
                    default:
                        break;
                }
            }
        }

        LogStarting(request.JobId, request.Kind, request.Quality, request.CookieBrowser);
        progress?.Report(tracker.Current);

        var spec = new ProcessSpec(ytDlp, arguments, Timeout: null, YtDlpMediaExtractor.Utf8Environment);
        var result = await runner.RunAsync(spec, OnLine, OnLine, cancellationToken).ConfigureAwait(false);

        // The path yt-dlp prints can be garbled when the title has non-Latin characters (Arabic, for one), because
        // its console encoding isn't ours to control. The name was chosen here, so the file can be found without it.
        var saved = finishedPath is not null && File.Exists(finishedPath) ? finishedPath : null;
        if (result.ExitCode == 0 && (saved ?? FindSavedFile(request.OutputDirectory, baseName)) is { } file)
        {
            // A file that doesn't open or was cut short is a failed download, not a finished one.
            progress?.Report(tracker.Current with { Stage = DownloadStage.Verifying, BytesPerSecond = null, Eta = null });
            var verdict = await verifier.VerifyAsync(file, request.Kind, request.ExpectedDuration, cancellationToken).ConfigureAwait(false);
            if (!verdict.IsValid)
            {
                DeleteBadFile(file);
                DeleteTempDirectory(request);
                return DownloadResult.Failure(DownloadErrorKind.CorruptFile, verdict.Problem);
            }

            var size = new FileInfo(file).Length;
            DeleteTempDirectory(request);
            LogCompleted(request.JobId, size);
            return DownloadResult.Success(file, size);
        }

        var (kind, detail) = ErrorClassifier.Classify(result.StandardError);
        if (result.ExitCode == 0)
        {
            kind = DownloadErrorKind.Unknown;
            detail ??= "yt-dlp finished without saving a file.";
        }

        detail ??= LastNonEmptyLine(result.StandardError);
        LogFailed(request.JobId, kind, result.ExitCode, result.StandardError);
        return DownloadResult.Failure(kind, detail);
    }

    public async Task DiscardPartialFilesAsync(DownloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var folder = TempDirectoryFor(request);

        // FFmpeg may still hold a file for a moment after its process was killed.
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }

                RemoveEmptyPartialRoot(request.OutputDirectory);
                LogDiscarded(request.JobId);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 5)
                {
                    LogDiscardFailed(request.JobId, ex);
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt)).ConfigureAwait(false);
            }
        }
    }

    internal static string TempDirectoryFor(DownloadRequest request) =>
        Path.Combine(request.OutputDirectory, PartialFolderName, request.JobId.ToString("N", CultureInfo.InvariantCulture));

    /// <summary>The space a download needs: both streams plus the joined file exist together for a moment.</summary>
    internal static long RequiredFreeBytes(long? estimatedBytes) =>
        estimatedBytes is > 0 ? (estimatedBytes.Value * 2) + ExtraFreeBytes : MinimumFreeBytes;

    private DownloadResult? CheckFreeSpace(DownloadRequest request)
    {
        // Unreadable usage (a disconnected share, say) is not a reason to refuse; the download itself will report real problems.
        if (disk.GetUsage(request.OutputDirectory) is not { } usage)
        {
            return null;
        }

        var required = RequiredFreeBytes(request.EstimatedBytes);
        if (usage.FreeBytes >= required)
        {
            return null;
        }

        var detail = string.Create(
            CultureInfo.InvariantCulture,
            $"{Formatting.Bytes(usage.FreeBytes, CultureInfo.InvariantCulture)} free on {usage.DriveName}, about {Formatting.Bytes(required, CultureInfo.InvariantCulture)} needed.");
        LogNotEnoughSpace(request.JobId, usage.FreeBytes, required);
        return DownloadResult.Failure(DownloadErrorKind.DiskFull, detail);
    }

    private static string ChooseBaseName(DownloadRequest request)
    {
        var wanted = FileNames.Expand(request.FileNameTemplate, request.Title, request.Uploader, Formatting.Quality(request.Quality));
        var folder = request.OutputDirectory;

        // A name is taken when any file already starts with it, whatever its extension.
        return FileNames.MakeUnique(wanted, name => Directory.EnumerateFiles(folder, name + ".*").Any());
    }

    /// <summary>The newest finished file named <c>baseName.something</c>, ignoring yt-dlp's unfinished leftovers.</summary>
    internal static string? FindSavedFile(string folder, string baseName)
    {
        try
        {
            return Directory.EnumerateFiles(folder, baseName + ".*")
                .Where(f => Path.GetExtension(f) is not (".part" or ".ytdl" or ".temp"))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void DeleteBadFile(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogBadFileKept(file, ex);
        }
    }

    private static void DeleteTempDirectory(DownloadRequest request)
    {
        try
        {
            var folder = TempDirectoryFor(request);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            RemoveEmptyPartialRoot(request.OutputDirectory);
        }
        catch (IOException)
        {
            // Leftover temporary files are harmless; the download itself succeeded.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: the file is in place, so a folder that can't be tidied is not an error.
        }
    }

    private static void RemoveEmptyPartialRoot(string outputDirectory)
    {
        var root = Path.Combine(outputDirectory, PartialFolderName);
        if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
        {
            Directory.Delete(root);
        }
    }

    private static string? LastNonEmptyLine(string text) =>
        text.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting download {JobId} ({Kind}, {Quality}); browser sign-in: {Browser}")]
    private partial void LogStarting(Guid jobId, MediaKind kind, QualityPreset quality, string? browser);

    [LoggerMessage(Level = LogLevel.Information, Message = "Download {JobId} completed ({Size} bytes)")]
    private partial void LogCompleted(Guid jobId, long size);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Download {JobId} failed ({Kind}, exit {ExitCode}): {StandardError}")]
    private partial void LogFailed(Guid jobId, DownloadErrorKind kind, int exitCode, string standardError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Download {JobId} not started: {Free} bytes free, {Required} needed")]
    private partial void LogNotEnoughSpace(Guid jobId, long free, long required);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The download folder {Folder} can't be used")]
    private partial void LogFolderUnavailable(string folder, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove the bad file {File}")]
    private partial void LogBadFileKept(string file, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed the partial files of download {JobId}")]
    private partial void LogDiscarded(Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove the partial files of download {JobId}")]
    private partial void LogDiscardFailed(Guid jobId, Exception exception);
}
