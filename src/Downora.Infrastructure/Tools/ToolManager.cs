using System.IO.Compression;
using System.Security.Cryptography;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Tools;

internal sealed partial class ToolManager : IToolManager, IDisposable
{
    private static readonly ToolKind[] AllTools = [ToolKind.YtDlp, ToolKind.Ffmpeg, ToolKind.Ffprobe, ToolKind.Deno];

    private readonly IAppPaths _paths;
    private readonly ISettingsService _settings;
    private readonly IProcessRunner _runner;
    private readonly HttpClient _http;
    private readonly ILogger<ToolManager> _logger;
    private readonly string _bundledDirectory;
    private readonly Func<ToolKind, ToolSource?> _sources;
    private readonly bool _canInstall;
    private readonly SemaphoreSlim _installGate = new(1, 1);

    public ToolManager(IAppPaths paths, ISettingsService settings, IProcessRunner runner, HttpClient http, ILogger<ToolManager> logger)
        : this(paths, settings, runner, http, logger, Path.Combine(AppContext.BaseDirectory, "tools"), ToolCatalog.SourceFor, OperatingSystem.IsWindows())
    {
    }

    /// <summary>Test seam: a different bundled folder, release sources and platform check.</summary>
    internal ToolManager(
        IAppPaths paths,
        ISettingsService settings,
        IProcessRunner runner,
        HttpClient http,
        ILogger<ToolManager> logger,
        string bundledDirectory,
        Func<ToolKind, ToolSource?> sources,
        bool canInstall)
    {
        _paths = paths;
        _settings = settings;
        _runner = runner;
        _http = http;
        _logger = logger;
        _bundledDirectory = bundledDirectory;
        _sources = sources;
        _canInstall = canInstall;
    }

    public string? GetPath(ToolKind tool)
    {
        if (tool == ToolKind.Ffmpeg && FfmpegOverride() is { } custom)
        {
            return custom;
        }

        var installed = Path.Combine(_paths.ToolsDirectory, ToolCatalog.ExecutableName(tool));
        return File.Exists(installed) ? installed : null;
    }

    public async Task<IReadOnlyList<ToolStatus>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ToolStatus>();
        foreach (var tool in AllTools)
        {
            var path = GetPath(tool);
            results.Add(path is null
                ? new ToolStatus(tool, false, null, null)
                : new ToolStatus(tool, true, await ReadVersionAsync(tool, path, cancellationToken).ConfigureAwait(false), path));
        }

        return results;
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_bundledDirectory))
        {
            return;
        }

        Directory.CreateDirectory(_paths.ToolsDirectory);
        foreach (var tool in AllTools)
        {
            var name = ToolCatalog.ExecutableName(tool);
            var bundled = Path.Combine(_bundledDirectory, name);
            var target = Path.Combine(_paths.ToolsDirectory, name);

            // Never overwrite: yt-dlp updates itself in the data folder and may be newer than the bundled copy.
            if (File.Exists(bundled) && !File.Exists(target))
            {
                await using var source = File.OpenRead(bundled);
                await using var destination = File.Create(target);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                LogCopiedBundled(name);
            }
        }
    }

    public async Task InstallAsync(ToolKind tool, IProgress<ToolInstallProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_canInstall || _sources(tool) is not { } source)
        {
            throw new ToolInstallException("Automatic install is only available on Windows (64-bit).");
        }

        await _installGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(_paths.ToolsDirectory);
        var download = Path.Combine(_paths.ToolsDirectory, $".download-{Guid.NewGuid():N}.partial");
        try
        {
            var expected = await ExpectedChecksumAsync(source, cancellationToken).ConfigureAwait(false);
            var actual = await DownloadAsync(source.DownloadUrl, download, tool, progress, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                LogChecksumMismatch(tool, expected, actual);
                throw new ToolInstallException($"The downloaded file didn't match its published checksum, so it was not installed. ({tool})");
            }

            if (source.ZipContents is null)
            {
                File.Move(download, Path.Combine(_paths.ToolsDirectory, ToolCatalog.ExecutableName(tool)), overwrite: true);
            }
            else
            {
                await ExtractAsync(download, source.ZipContents, cancellationToken).ConfigureAwait(false);
            }

            LogInstalled(tool, actual);
        }
        catch (HttpRequestException ex)
        {
            throw new ToolInstallException($"Couldn't download {tool}: {ex.Message}", ex);
        }
        finally
        {
            TryDelete(download);
            _installGate.Release();
        }
    }

    public async Task<string?> UpdateYtDlpAsync(CancellationToken cancellationToken = default)
    {
        var path = GetPath(ToolKind.YtDlp) ?? throw new ToolInstallException("yt-dlp isn't installed yet.");
        var result = await _runner.RunAsync(new ProcessSpec(path, ["-U"], TimeSpan.FromMinutes(3)), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var reason = Core.Services.ErrorClassifier.LastErrorLine(result.StandardError) ?? result.StandardError.Trim();
            throw new ToolInstallException($"yt-dlp couldn't update itself: {reason}");
        }

        return await ReadVersionAsync(ToolKind.YtDlp, path, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _installGate.Dispose();

    /// <summary>"2026.10.26" / "ffmpeg version N-121-gabc Copyright…" → "N-121-gabc" / "deno 2.9.8 (stable…)" → "2.9.8".</summary>
    internal static string? ParseVersion(ToolKind tool, string output)
    {
        var line = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (line is null)
        {
            return null;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tool switch
        {
            ToolKind.Ffmpeg or ToolKind.Ffprobe when words.Length >= 3 && words[1] == "version" => words[2],
            ToolKind.Deno when words.Length >= 2 && words[0] == "deno" => words[1],
            _ => line,
        };
    }

    private async Task<string?> ReadVersionAsync(ToolKind tool, string path, CancellationToken cancellationToken)
    {
        string[] args = tool is ToolKind.Ffmpeg or ToolKind.Ffprobe ? ["-version"] : ["--version"];
        try
        {
            var result = await _runner.RunAsync(new ProcessSpec(path, args, TimeSpan.FromSeconds(20)), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return result.ExitCode == 0 ? ParseVersion(tool, result.StandardOutput) : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            LogVersionFailed(tool, ex);
            return null;
        }
    }

    private async Task<string> ExpectedChecksumAsync(ToolSource source, CancellationToken cancellationToken)
    {
        var text = await _http.GetStringAsync(source.ChecksumUrl, cancellationToken).ConfigureAwait(false);
        return ToolCatalog.ParseChecksum(text, source.ChecksumFileName)
            ?? throw new ToolInstallException($"The release doesn't list a checksum for {source.ChecksumFileName}, so it wasn't installed.");
    }

    private async Task<string> DownloadAsync(Uri url, string target, ToolKind tool, IProgress<ToolInstallProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (var output = File.Create(target))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                received += read;
                progress?.Report(new ToolInstallProgress(tool, received, total));
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private async Task ExtractAsync(string zipPath, IReadOnlyList<ZipEntryMapping> contents, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var mapping in contents)
        {
            // Matched by file name only and written to a fixed path, so entry paths can't escape the tools folder.
            var entry = archive.Entries.FirstOrDefault(e => string.Equals(e.Name, mapping.EntryFileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new ToolInstallException($"{mapping.EntryFileName} wasn't found in the downloaded archive.");

            var target = Path.Combine(_paths.ToolsDirectory, ToolCatalog.ExecutableName(mapping.Tool));
            var temp = target + ".partial";
            await using (var source = entry.Open())
            await using (var destination = File.Create(temp))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, target, overwrite: true);
        }
    }

    private string? FfmpegOverride()
    {
        try
        {
            var custom = _settings.Current.FfmpegPathOverride;
            return !string.IsNullOrWhiteSpace(custom) && File.Exists(custom) ? custom : null;
        }
        catch (InvalidOperationException)
        {
            return null; // Settings not loaded yet.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Copied bundled {Tool} into the tools folder")]
    private partial void LogCopiedBundled(string tool);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installed {Tool} (sha256 {Sha256})")]
    private partial void LogInstalled(ToolKind tool, string sha256);

    [LoggerMessage(Level = LogLevel.Error, Message = "Checksum mismatch for {Tool}: expected {Expected}, got {Actual}")]
    private partial void LogChecksumMismatch(ToolKind tool, string expected, string actual);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the {Tool} version")]
    private partial void LogVersionFailed(ToolKind tool, Exception exception);
}
