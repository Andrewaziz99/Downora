using System.Text.Json;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Extraction;

/// <summary>Runs <c>yt-dlp -J</c> to read a link's metadata, formats or playlist entries without downloading.</summary>
internal sealed partial class YtDlpMediaExtractor(
    IToolManager tools,
    IProcessRunner runner,
    ISettingsService settings,
    ILogger<YtDlpMediaExtractor> logger) : IMediaExtractor
{
    public static readonly TimeSpan AnalyzeTimeout = TimeSpan.FromSeconds(90);

    /// <summary>Makes Python print UTF-8 regardless of the Windows console code page (titles in Arabic, emoji…).</summary>
    internal static readonly IReadOnlyDictionary<string, string> Utf8Environment = new Dictionary<string, string>
    {
        ["PYTHONUTF8"] = "1",
        ["PYTHONIOENCODING"] = "utf-8",
    };

    public async Task<AnalysisResult> AnalyzeAsync(UrlValidationResult url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsValid)
        {
            throw new ArgumentException("Only validated URLs can be analyzed.", nameof(url));
        }

        if (tools.GetPath(ToolKind.YtDlp) is not { } ytDlp)
        {
            return AnalysisResult.Failed(DownloadErrorKind.ToolMissing, "yt-dlp is not installed.");
        }

        var spec = new ProcessSpec(ytDlp, BuildArguments(url), AnalyzeTimeout, Utf8Environment);
        var safeUrl = LogSafe.Url(url.NormalizedUrl); // never the raw link: it can carry sign-in tokens
        LogAnalyzing(url.Platform, safeUrl);

        var result = await runner.RunAsync(spec, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.TimedOut)
        {
            return AnalysisResult.Failed(DownloadErrorKind.Network, "The site took too long to respond.");
        }

        var json = result.StandardOutput.Trim();
        if (result.ExitCode != 0 || json.Length == 0 || json == "null")
        {
            var (kind, detail) = ErrorClassifier.Classify(result.StandardError);
            LogFailed(kind, result.ExitCode, result.StandardError);
            return AnalysisResult.Failed(kind, detail);
        }

        try
        {
            return YtDlpJsonParser.Parse(json, url.Platform);
        }
        catch (JsonException ex)
        {
            LogBadJson(ex);
            return AnalysisResult.Failed(DownloadErrorKind.Unknown, "yt-dlp returned information Downora couldn't read.");
        }
    }

    internal IReadOnlyList<string> BuildArguments(UrlValidationResult url)
    {
        var args = new List<string>
        {
            "--dump-single-json",
            "--flat-playlist",
            "--ignore-config", // a user's yt-dlp.conf must not change what Downora runs
            "--no-plugin-dirs", // nor may code from a plugin folder that anything could have written to
            "--no-warnings",
            "--socket-timeout", "20",
            "--retries", "3",
        };

        if (url.PreferSingleItem)
        {
            args.Add("--no-playlist");
        }

        if (tools.GetPath(ToolKind.Ffmpeg) is { } ffmpeg)
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpeg);
        }

        if (tools.GetPath(ToolKind.Deno) is { } deno)
        {
            // YouTube needs a JavaScript runtime for its challenges (yt-dlp ≥ 2025.11).
            args.Add("--js-runtimes");
            args.Add($"deno:{deno}");
        }

        // Opt-in: reuse the user's own browser sign-in for content their account can already see.
        if (CookieBrowsers.For(settings.Current) is { } browser)
        {
            args.Add("--cookies-from-browser");
            args.Add(browser);
        }

        // Everything after "--" is a URL, never an option, whatever it contains.
        args.Add("--");
        args.Add(url.Url!.AbsoluteUri);
        return args;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyzing {Platform} link {Url}")]
    private partial void LogAnalyzing(SourcePlatform platform, string? url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Analysis failed ({Kind}, exit {ExitCode}): {StandardError}")]
    private partial void LogFailed(DownloadErrorKind kind, int exitCode, string standardError);

    [LoggerMessage(Level = LogLevel.Error, Message = "yt-dlp output was not valid JSON")]
    private partial void LogBadJson(Exception exception);
}
