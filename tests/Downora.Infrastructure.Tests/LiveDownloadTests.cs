using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Downloads;
using Downora.Infrastructure.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

/// <summary>
/// Real downloads with the real tools. Skipped unless DOWNORA_LIVE_TESTS=1 and yt-dlp, FFmpeg are installed
/// (set DOWNORA_TOOLS to their folder; it defaults to %LocalAppData%\Downora\tools).
/// </summary>
public sealed class LiveDownloadTests : IDisposable
{
    private const string Zoo = "https://www.youtube.com/watch?v=jNQXAC9IVRw";

    private readonly string _folder = Directory.CreateTempSubdirectory("downora-live-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static string? ToolsFolder()
    {
        if (Environment.GetEnvironmentVariable("DOWNORA_LIVE_TESTS") != "1")
        {
            return null;
        }

        var folder = Environment.GetEnvironmentVariable("DOWNORA_TOOLS")
                     ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Downora", "tools");
        return File.Exists(Path.Combine(folder, "yt-dlp.exe")) && File.Exists(Path.Combine(folder, "ffmpeg.exe")) ? folder : null;
    }

    private static YtDlpDownloadEngine Engine(string tools)
    {
        var manager = Substitute.For<IToolManager>();
        manager.GetPath(ToolKind.YtDlp).Returns(Path.Combine(tools, "yt-dlp.exe"));
        manager.GetPath(ToolKind.Ffmpeg).Returns(Path.Combine(tools, "ffmpeg.exe"));
        manager.GetPath(ToolKind.Deno).Returns(File.Exists(Path.Combine(tools, "deno.exe")) ? Path.Combine(tools, "deno.exe") : null);
        var disk = Substitute.For<IDiskSpaceService>();
        manager.GetPath(ToolKind.Ffprobe).Returns(File.Exists(Path.Combine(tools, "ffprobe.exe")) ? Path.Combine(tools, "ffprobe.exe") : null);
        var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        return new YtDlpDownloadEngine(manager, runner, disk, new FfprobeFileVerifier(manager, runner, NullLogger<FfprobeFileVerifier>.Instance), NullLogger<YtDlpDownloadEngine>.Instance);
    }

    [Fact]
    public async Task A_real_video_downloads_with_progress_into_the_chosen_folder()
    {
        var tools = ToolsFolder();
        Assert.SkipWhen(tools is null, "Set DOWNORA_LIVE_TESTS=1 with yt-dlp and FFmpeg installed to run live tests.");
        var request = new DownloadRequest { JobId = Guid.NewGuid(), Url = Zoo, OutputDirectory = _folder, Quality = QualityPreset.P360, Title = "Me at the zoo" };
        var progress = new RecordingProgress();

        var result = await Engine(tools!).DownloadAsync(request, progress, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorDetail);
        Assert.StartsWith(_folder, result.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Me at the zoo.", Path.GetFileName(result.FilePath), StringComparison.Ordinal);
        Assert.True(result.FileSize > 100_000);
        Assert.Contains(progress.Reports, r => r.Stage == DownloadStage.Downloading && r.Percent > 0);
        Assert.False(Directory.Exists(Path.Combine(_folder, YtDlpDownloadEngine.PartialFolderName)));
    }

    [Fact]
    public async Task A_real_link_downloads_as_mp3()
    {
        var tools = ToolsFolder();
        Assert.SkipWhen(tools is null, "Set DOWNORA_LIVE_TESTS=1 with yt-dlp and FFmpeg installed to run live tests.");
        var request = new DownloadRequest
        {
            JobId = Guid.NewGuid(), Url = Zoo, OutputDirectory = _folder, Kind = MediaKind.Audio, AudioFormat = AudioFormat.Mp3, Title = "Me at the zoo",
        };

        var result = await Engine(tools!).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorDetail);
        Assert.EndsWith(".mp3", result.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AudioFormat.Mp3)]
    [InlineData(AudioFormat.Flac)]
    public async Task A_real_audio_download_carries_its_title_and_cover_art(AudioFormat format)
    {
        var tools = ToolsFolder();
        Assert.SkipWhen(tools is null || !File.Exists(Path.Combine(tools, "ffprobe.exe")), "Set DOWNORA_LIVE_TESTS=1 with yt-dlp, FFmpeg and FFprobe installed to run live tests.");
        var request = new DownloadRequest
        {
            JobId = Guid.NewGuid(), Url = Zoo, OutputDirectory = _folder, Kind = MediaKind.Audio, AudioFormat = format, Title = "Me at the zoo",
            EmbedMetadata = true, EmbedCoverArt = true, ExpectedDuration = TimeSpan.FromSeconds(19),
        };

        var result = await Engine(tools!).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorDetail);
        var probe = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var info = await probe.RunAsync(
            new ProcessSpec(Path.Combine(tools!, "ffprobe.exe"), ["-v", "error", "-print_format", "json", "-show_entries", "format_tags=title:stream=codec_type", "-i", result.FilePath!]),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("Me at the zoo", info.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"video\"", info.StandardOutput, StringComparison.Ordinal); // the cover picture shows up as a video stream
    }

    [Fact]
    public async Task Cancelling_a_real_download_kills_the_tool_and_discarding_removes_its_files()
    {
        var tools = ToolsFolder();
        Assert.SkipWhen(tools is null, "Set DOWNORA_LIVE_TESTS=1 with yt-dlp and FFmpeg installed to run live tests.");
        var request = new DownloadRequest { JobId = Guid.NewGuid(), Url = Zoo, OutputDirectory = _folder, Title = "Me at the zoo" };
        var engine = Engine(tools!);
        using var cancel = new CancellationTokenSource();

        var download = engine.DownloadAsync(request, cancellationToken: cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        await engine.DiscardPartialFilesAsync(request);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_folder));
    }
}
