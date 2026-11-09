using System.Diagnostics;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Infrastructure.Extraction;
using Downora.Infrastructure.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

/// <summary>Runs real processes. "dotnet" exists wherever these tests run.</summary>
public class ProcessRunnerTests
{
    private static readonly ProcessRunner Runner = new(NullLogger<ProcessRunner>.Instance);

    private static string Dotnet => Environment.ProcessPath is { } p && Path.GetFileNameWithoutExtension(p) == "dotnet" ? p : "dotnet";

    /// <summary>A command that keeps running for a while on Windows and Linux.</summary>
    private static ProcessSpec LongRunning(TimeSpan? timeout = null) => OperatingSystem.IsWindows()
        ? new ProcessSpec("ping", ["-n", "30", "127.0.0.1"], timeout)
        : new ProcessSpec("sleep", ["30"], timeout);

    [Fact]
    public async Task Output_and_exit_code_are_captured_line_by_line()
    {
        var lines = new List<string>();

        var result = await Runner.RunAsync(new ProcessSpec(Dotnet, ["--version"]), lines.Add, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.StandardOutput.Trim());
        Assert.Equal(result.StandardOutput.Trim(), Assert.Single(lines, l => l.Length > 0));
    }

    [Fact]
    public async Task Errors_and_non_zero_exit_codes_are_reported()
    {
        var result = await Runner.RunAsync(new ProcessSpec(Dotnet, ["no-such-command-downora"]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardError + result.StandardOutput));
    }

    [Fact]
    public async Task Timeout_kills_the_process_and_says_so()
    {
        var watch = Stopwatch.StartNew();

        var result = await Runner.RunAsync(LongRunning(TimeSpan.FromMilliseconds(500)), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Cancelling_kills_the_process_and_throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Runner.RunAsync(LongRunning(), cancellationToken: cts.Token));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Missing_executable_fails_loudly() =>
        await Assert.ThrowsAnyAsync<Exception>(() =>
            Runner.RunAsync(new ProcessSpec("downora-no-such-tool-xyz", []), cancellationToken: TestContext.Current.CancellationToken));
}

/// <summary>
/// Opt-in: runs the real yt-dlp against YouTube and SoundCloud. Set DOWNORA_LIVE_TESTS=1 and have yt-dlp on PATH
/// (or DOWNORA_YTDLP=full path). Skipped otherwise, including in CI.
/// </summary>
public class LiveExtractionTests
{
    private static string? YtDlp()
    {
        if (Environment.GetEnvironmentVariable("DOWNORA_LIVE_TESTS") != "1")
        {
            return null;
        }

        var configured = Environment.GetEnvironmentVariable("DOWNORA_YTDLP");
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        var name = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, name))
            .FirstOrDefault(File.Exists);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw", AnalysisKind.Media, "Me at the zoo")]
    [InlineData("https://soundcloud.com/forss/flickermood", AnalysisKind.Media, "Flickermood")]
    [InlineData("https://www.youtube.com/playlist?list=PLlaN88a7y2_plecYoJxvRFTLHVbIVAOoc", AnalysisKind.Playlist, "Rick Astley")]
    public async Task Real_links_are_analyzed(string link, AnalysisKind kind, string titlePart)
    {
        var ytDlp = YtDlp();
        Assert.SkipWhen(ytDlp is null, "Set DOWNORA_LIVE_TESTS=1 with yt-dlp available to run live tests.");

        var tools = Substitute.For<IToolManager>();
        tools.GetPath(ToolKind.YtDlp).Returns(ytDlp);
        var extractor = new YtDlpMediaExtractor(tools, new ProcessRunner(NullLogger<ProcessRunner>.Instance), ExtractionTests.Settings(), NullLogger<YtDlpMediaExtractor>.Instance);

        var result = await extractor.AnalyzeAsync(new UrlValidator(new SourceDetector()).Validate(link), TestContext.Current.CancellationToken);

        Assert.True(result.Kind == kind, $"{result.Kind}: {result.ErrorKind} {result.ErrorDetail}");
        var title = result.Media?.Title ?? result.Playlist!.Title;
        Assert.Contains(titlePart, title, StringComparison.Ordinal);
    }
}
