using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Downloads;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

/// <summary>Plays back recorded yt-dlp output line by line, like the real runner does, and can hold the "process" open until cancelled.</summary>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    public IReadOnlyList<string> StdoutLines { get; init; } = [];

    public string StdErr { get; init; } = string.Empty;

    /// <summary>Lines yt-dlp prints on stderr that the engine also reads (post-processing steps).</summary>
    public IReadOnlyList<string> StderrLines { get; init; } = [];

    public int ExitCode { get; init; }

    /// <summary>Runs once the "process" starts, e.g. to create the files yt-dlp would have written.</summary>
    public Action<ProcessSpec>? OnRun { get; init; }

    public bool BlockUntilCancelled { get; init; }

    public List<ProcessSpec> Calls { get; } = [];

    /// <summary>Completes once the output has been delivered and the process is "running".</summary>
    public TaskCompletionSource Running { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutputLine = null, Action<string>? onErrorLine = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(spec);
        OnRun?.Invoke(spec);
        foreach (var line in StdoutLines)
        {
            onOutputLine?.Invoke(line);
        }

        foreach (var line in StderrLines)
        {
            onErrorLine?.Invoke(line);
        }

        Running.TrySetResult();
        if (BlockUntilCancelled)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return new ProcessResult(ExitCode, string.Join('\n', StdoutLines), StdErr, false);
    }
}

internal sealed class RecordingProgress : IProgress<DownloadProgress>
{
    public List<DownloadProgress> Reports { get; } = [];

    public void Report(DownloadProgress value) => Reports.Add(value);
}

public sealed class DownloadEngineTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("downora-engine-").FullName;
    private readonly IToolManager _tools = Substitute.For<IToolManager>();
    private readonly IDiskSpaceService _disk = Substitute.For<IDiskSpaceService>();
    private readonly IMediaFileVerifier _verifier = Substitute.For<IMediaFileVerifier>();

    public DownloadEngineTests()
    {
        _verifier.VerifyAsync(Arg.Any<string>(), Arg.Any<MediaKind>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(FileVerification.Ok);
        _tools.GetPath(ToolKind.YtDlp).Returns("C:/tools/yt-dlp.exe");
        _tools.GetPath(ToolKind.Ffmpeg).Returns("C:/tools/ffmpeg.exe");
        _tools.GetPath(ToolKind.Deno).Returns("C:/tools/deno.exe");
        _disk.GetUsage(Arg.Any<string>()).Returns(new DiskUsage("C:", 500L << 30, 400L << 30));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private YtDlpDownloadEngine Engine(IProcessRunner runner) =>
        new(_tools, runner, _disk, _verifier, NullLogger<YtDlpDownloadEngine>.Instance);

    private DownloadRequest Request(Action<DownloadRequestBuilder>? tweak = null)
    {
        var builder = new DownloadRequestBuilder(_folder);
        tweak?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>The recorded video output, with the final path pointed at a real file the fake "yt-dlp" writes.</summary>
    private ScriptedProcessRunner SuccessfulRun(string fixture, string fileName, DownloadRequest request, byte[]? content = null)
    {
        var finalPath = Path.Combine(_folder, fileName);
        var lines = ExtractionTests.Fixture(fixture)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith("FILE|", StringComparison.Ordinal))
            .Append("FILE|" + finalPath)
            .ToList();

        return new ScriptedProcessRunner
        {
            StdoutLines = lines,
            OnRun = _ =>
            {
                // yt-dlp keeps its work in the job's own folder and moves the finished file out of it.
                var temp = YtDlpDownloadEngine.TempDirectoryFor(request);
                Directory.CreateDirectory(temp);
                File.WriteAllText(Path.Combine(temp, "x.f251.webm.part"), "partial");
                File.WriteAllBytes(finalPath, content ?? new byte[4096]);
            },
        };
    }

    [Fact]
    public async Task A_video_download_reports_progress_and_returns_the_saved_file()
    {
        var request = Request();
        var runner = SuccessfulRun("dl_video_merge.out", "Me at the zoo.mkv", request);
        var progress = new RecordingProgress();

        var result = await Engine(runner).DownloadAsync(request, progress, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(Path.Combine(_folder, "Me at the zoo.mkv"), result.FilePath);
        Assert.Equal(4096, result.FileSize);
        Assert.Equal(DownloadStage.Starting, progress.Reports[0].Stage);
        Assert.Contains(progress.Reports, r => r.Stage == DownloadStage.Downloading && r.Percent > 0);
        Assert.Contains(progress.Reports, r => r.Stage == DownloadStage.Merging);
        Assert.Contains(progress.Reports, r => r.Stage == DownloadStage.PostProcessing);

        // The job's temporary folder (and the hidden parent, once empty) is gone after success.
        Assert.False(Directory.Exists(Path.Combine(_folder, YtDlpDownloadEngine.PartialFolderName)));
    }

    [Fact]
    public async Task The_finished_file_is_checked_against_the_expected_length_before_it_counts_as_done()
    {
        var request = Request(b => b.ExpectedDuration = TimeSpan.FromSeconds(19));
        var runner = SuccessfulRun("dl_video_merge.out", "Me at the zoo.mkv", request);
        var progress = new RecordingProgress();

        var result = await Engine(runner).DownloadAsync(request, progress, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        await _verifier.Received(1).VerifyAsync(Path.Combine(_folder, "Me at the zoo.mkv"), MediaKind.Video, TimeSpan.FromSeconds(19), Arg.Any<CancellationToken>());
        Assert.Equal(DownloadStage.Verifying, progress.Reports[^1].Stage);
    }

    [Fact]
    public async Task A_file_that_fails_the_check_is_removed_and_the_download_fails_as_corrupt()
    {
        var request = Request();
        var runner = SuccessfulRun("dl_video_merge.out", "Me at the zoo.mkv", request);
        _verifier.VerifyAsync(Arg.Any<string>(), Arg.Any<MediaKind>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(FileVerification.Bad("The file can't be opened."));

        var result = await Engine(runner).DownloadAsync(request, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(DownloadErrorKind.CorruptFile, result.ErrorKind);
        Assert.Equal("The file can't be opened.", result.ErrorDetail);
        Assert.False(File.Exists(Path.Combine(_folder, "Me at the zoo.mkv")));
        Assert.False(Directory.Exists(Path.Combine(_folder, YtDlpDownloadEngine.PartialFolderName)));
    }

    [Fact]
    public async Task A_failed_download_is_never_sent_to_the_check()
    {
        var request = Request();
        var runner = new ScriptedProcessRunner { ExitCode = 1, StdErr = "ERROR: Video unavailable" };

        var result = await Engine(runner).DownloadAsync(request, null, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        await _verifier.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Post_processing_lines_on_stderr_are_followed_too()
    {
        var request = Request();
        var finalPath = Path.Combine(_folder, "a.mp3");
        var runner = new ScriptedProcessRunner
        {
            StdoutLines = ["DWN|downloading|1024|2048|NA|100.0|10|NA|NA|251", "FILE|" + finalPath],
            StderrLines = ["PP|started|ExtractAudio", "PP|finished|ExtractAudio"],
            OnRun = _ => File.WriteAllBytes(finalPath, new byte[10]),
        };
        var progress = new RecordingProgress();

        var result = await Engine(runner).DownloadAsync(request, progress, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(progress.Reports, r => r.Stage == DownloadStage.PostProcessing);
    }

    [Fact]
    public async Task A_garbled_final_path_is_recovered_by_the_name_the_engine_chose()
    {
        // yt-dlp prints non-Latin characters as blanks, so the path it reports doesn't exist; the saved file does.
        var title = "طلبتك من عمق قلبى - ألبوم";
        var request = Request(b => b.Title = title);
        var real = Path.Combine(_folder, title + ".m4a");
        var runner = new ScriptedProcessRunner
        {
            StdoutLines = ["DWN|finished|10|10|NA|NA|NA|NA|NA|x", @"FILE|" + Path.Combine(_folder, "    -  .m4a")],
            OnRun = _ =>
            {
                File.WriteAllBytes(real, new byte[777]);
                File.WriteAllText(Path.Combine(_folder, title + ".m4a.part"), "leftover"); // never mistaken for the result
            },
        };

        var result = await Engine(runner).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorDetail);
        Assert.Equal(real, result.FilePath);
        Assert.Equal(777, result.FileSize);
    }

    [Fact]
    public void Unfinished_files_are_never_taken_for_the_result()
    {
        File.WriteAllText(Path.Combine(_folder, "Song.mp3.part"), "x");
        File.WriteAllText(Path.Combine(_folder, "Song.f251.webm.ytdl"), "x");
        Assert.Null(YtDlpDownloadEngine.FindSavedFile(_folder, "Song"));

        File.WriteAllText(Path.Combine(_folder, "Song.mp3"), "x");
        Assert.Equal(Path.Combine(_folder, "Song.mp3"), YtDlpDownloadEngine.FindSavedFile(_folder, "Song"));
        Assert.Null(YtDlpDownloadEngine.FindSavedFile(_folder, "Song (2)"));
    }

    [Fact]
    public async Task The_command_that_runs_matches_the_request()
    {
        var request = Request(b => b.Title = "Me at the zoo");
        var runner = SuccessfulRun("dl_video_merge.out", "Me at the zoo.mkv", request);

        await Engine(runner).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(runner.Calls);
        Assert.Equal("C:/tools/yt-dlp.exe", call.FileName);
        Assert.Null(call.Timeout);
        Assert.Equal("1", call.Environment!["PYTHONUTF8"]);
        Assert.Equal(["--", request.Url], call.Arguments.TakeLast(2));
        Assert.Contains("Me at the zoo.%(ext)s", call.Arguments);
        Assert.Contains("home:" + _folder, call.Arguments);
        Assert.Contains("temp:" + YtDlpDownloadEngine.TempDirectoryFor(request), call.Arguments);
        Assert.Equal("deno:C:/tools/deno.exe", call.Arguments[call.Arguments.ToList().IndexOf("--js-runtimes") + 1]);
    }

    [Fact]
    public async Task An_audio_download_extracts_audio()
    {
        var request = Request(b =>
        {
            b.Kind = MediaKind.Audio;
            b.AudioFormat = AudioFormat.Mp3;
        });
        var runner = SuccessfulRun("dl_audio_extract.out", "Me at the zoo.mp3", request);

        var result = await Engine(runner).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("-x", runner.Calls[0].Arguments);
        Assert.Contains("mp3", runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task A_new_download_never_overwrites_a_file_that_is_already_there()
    {
        File.WriteAllText(Path.Combine(_folder, "Me at the zoo.mp4"), "older download");
        var request = Request(b => b.Title = "Me at the zoo");
        var runner = SuccessfulRun("dl_video_merge.out", "Me at the zoo (2).mkv", request);

        await Engine(runner).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Me at the zoo (2).%(ext)s", runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task Unsafe_titles_become_safe_file_names()
    {
        var request = Request(b => b.Title = "CON");
        var runner = SuccessfulRun("dl_video_merge.out", "_CON.mkv", request);

        await Engine(runner).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("_CON.%(ext)s", runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task Browser_sign_in_is_passed_only_when_requested()
    {
        var plain = Request();
        var withCookies = Request(b => b.CookieBrowser = "edge");
        var runnerPlain = SuccessfulRun("dl_video_merge.out", "a.mkv", plain);
        var runnerCookies = SuccessfulRun("dl_video_merge.out", "a.mkv", withCookies);

        await Engine(runnerPlain).DownloadAsync(plain, cancellationToken: TestContext.Current.CancellationToken);
        Directory.Delete(_folder, recursive: true);
        Directory.CreateDirectory(_folder);
        await Engine(runnerCookies).DownloadAsync(withCookies, cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("--cookies-from-browser", runnerPlain.Calls[0].Arguments);
        var args = runnerCookies.Calls[0].Arguments.ToList();
        Assert.Equal("edge", args[args.IndexOf("--cookies-from-browser") + 1]);
    }

    [Fact]
    public async Task A_missing_tool_is_reported_without_starting_anything()
    {
        var runner = new ScriptedProcessRunner();
        _tools.GetPath(ToolKind.Ffmpeg).Returns((string?)null);

        var result = await Engine(runner).DownloadAsync(Request(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(DownloadErrorKind.ToolMissing, result.ErrorKind);
        Assert.Contains("FFmpeg", result.ErrorDetail, StringComparison.Ordinal);
        Assert.Empty(runner.Calls);

        _tools.GetPath(ToolKind.YtDlp).Returns((string?)null);
        var noYtDlp = await Engine(runner).DownloadAsync(Request(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("yt-dlp", noYtDlp.ErrorDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Not_enough_disk_space_stops_before_downloading()
    {
        _disk.GetUsage(Arg.Any<string>()).Returns(new DiskUsage("C:", 500L << 30, 100L << 20));
        var runner = new ScriptedProcessRunner();

        var result = await Engine(runner).DownloadAsync(Request(b => b.EstimatedBytes = 200L << 20), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(DownloadErrorKind.DiskFull, result.ErrorKind);
        Assert.Contains("100 MB free on C:", result.ErrorDetail, StringComparison.Ordinal);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Space_must_cover_both_streams_and_the_joined_file()
    {
        // 300 MB free is enough for a 100 MB download (needs about 264 MB) but not for 200 MB (about 464 MB).
        _disk.GetUsage(Arg.Any<string>()).Returns(new DiskUsage("C:", 500L << 30, 300L << 20));
        var small = Request(b => b.EstimatedBytes = 100L << 20);
        var runner = SuccessfulRun("dl_video_merge.out", "a.mkv", small);

        Assert.True((await Engine(runner).DownloadAsync(small, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(DownloadErrorKind.DiskFull,
            (await Engine(new ScriptedProcessRunner()).DownloadAsync(Request(b => b.EstimatedBytes = 200L << 20), cancellationToken: TestContext.Current.CancellationToken)).ErrorKind);
    }

    [Fact]
    public async Task An_unreadable_disk_does_not_block_the_download()
    {
        _disk.GetUsage(Arg.Any<string>()).Returns((DiskUsage?)null);
        var request = Request();
        var runner = SuccessfulRun("dl_video_merge.out", "a.mkv", request);

        Assert.True((await Engine(runner).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("ERROR: [youtube] abc: Sign in to confirm you're not a bot. Use --cookies-from-browser or --cookies for the authentication.", DownloadErrorKind.RequiresSignIn)]
    [InlineData("ERROR: [youtube] abc: Video unavailable", DownloadErrorKind.Unavailable)]
    [InlineData("ERROR: unable to download video data: HTTP Error 503", DownloadErrorKind.Network)]
    [InlineData("ERROR: Could not copy Chrome cookie database. See https://github.com/yt-dlp/yt-dlp/issues/7271", DownloadErrorKind.BrowserCookies)]
    [InlineData("ERROR: [Errno 28] No space left on device", DownloadErrorKind.DiskFull)]
    [InlineData("ERROR: something nobody has seen before", DownloadErrorKind.Unknown)]
    public async Task Failures_are_classified_and_partial_files_are_kept_for_a_later_resume(string stderr, DownloadErrorKind expected)
    {
        var request = Request();
        var runner = new ScriptedProcessRunner
        {
            StdoutLines = ["DWN|downloading|1024|223779|NA|1000.0|10|NA|NA|395"],
            StdErr = stderr,
            ExitCode = 1,
            OnRun = _ =>
            {
                Directory.CreateDirectory(YtDlpDownloadEngine.TempDirectoryFor(request));
                File.WriteAllText(Path.Combine(YtDlpDownloadEngine.TempDirectoryFor(request), "x.part"), "partial");
            },
        };

        var result = await Engine(runner).DownloadAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.ErrorKind);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorDetail));
        Assert.True(File.Exists(Path.Combine(YtDlpDownloadEngine.TempDirectoryFor(request), "x.part")));
    }

    [Fact]
    public async Task Finishing_without_a_file_is_a_failure_not_a_success()
    {
        var runner = new ScriptedProcessRunner { StdoutLines = ["PP|started|MoveFiles"], ExitCode = 0 };

        var result = await Engine(runner).DownloadAsync(Request(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(DownloadErrorKind.Unknown, result.ErrorKind);
    }

    [Fact]
    public async Task Cancelling_stops_the_tool_and_keeps_partial_files_until_they_are_discarded()
    {
        var request = Request();
        var temp = YtDlpDownloadEngine.TempDirectoryFor(request);
        var runner = new ScriptedProcessRunner
        {
            StdoutLines = ["DWN|downloading|1024|223779|NA|1000.0|10|NA|NA|395"],
            BlockUntilCancelled = true,
            OnRun = _ =>
            {
                Directory.CreateDirectory(temp);
                File.WriteAllText(Path.Combine(temp, "x.f395.mp4.part"), "partial");
                File.WriteAllText(Path.Combine(temp, "x.f395.mp4.ytdl"), "state");
            },
        };
        using var cancel = new CancellationTokenSource();
        var engine = Engine(runner);

        var download = engine.DownloadAsync(request, cancellationToken: cancel.Token);
        await runner.Running.Task;
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);

        // A crash or a shutdown also looks like this, so nothing is deleted by cancelling alone.
        Assert.True(File.Exists(Path.Combine(temp, "x.f395.mp4.part")));

        // The user's cancel then asks for the clean-up explicitly.
        await engine.DiscardPartialFilesAsync(request);
        Assert.False(Directory.Exists(temp));
        Assert.False(Directory.Exists(Path.Combine(_folder, YtDlpDownloadEngine.PartialFolderName)));
    }

    [Fact]
    public async Task Discarding_only_touches_its_own_job_and_tolerates_nothing_to_delete()
    {
        var mine = Request();
        var other = Request(b => b.JobId = Guid.NewGuid());
        Directory.CreateDirectory(YtDlpDownloadEngine.TempDirectoryFor(other));
        File.WriteAllText(Path.Combine(YtDlpDownloadEngine.TempDirectoryFor(other), "keep.part"), "other job");
        var engine = Engine(new ScriptedProcessRunner());

        await engine.DiscardPartialFilesAsync(mine);

        Assert.True(File.Exists(Path.Combine(YtDlpDownloadEngine.TempDirectoryFor(other), "keep.part")));
    }

    [Fact]
    public void Required_space_scales_with_the_estimate_and_has_a_floor()
    {
        Assert.Equal(256L << 20, YtDlpDownloadEngine.RequiredFreeBytes(null));
        Assert.Equal(256L << 20, YtDlpDownloadEngine.RequiredFreeBytes(0));
        Assert.Equal((2L * (100L << 20)) + (64L << 20), YtDlpDownloadEngine.RequiredFreeBytes(100L << 20));
    }
}

/// <summary>Mutable stand-in for the init-only request, so each test can change just what it cares about.</summary>
internal sealed class DownloadRequestBuilder(string folder)
{
    public Guid JobId { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = "https://www.youtube.com/watch?v=jNQXAC9IVRw";
    public MediaKind Kind { get; set; } = MediaKind.Video;
    public QualityPreset Quality { get; set; } = QualityPreset.Best;
    public VideoContainer Container { get; set; } = VideoContainer.Mp4;
    public AudioFormat AudioFormat { get; set; } = AudioFormat.Mp3;
    public int AudioBitrateKbps { get; set; } = 320;
    public string? Title { get; set; } = "Me at the zoo";
    public string? Uploader { get; set; } = "jawed";
    public long? EstimatedBytes { get; set; }
    public string? CookieBrowser { get; set; }
    public string FileNameTemplate { get; set; } = "{title}";
    public bool EmbedMetadata { get; set; }
    public bool EmbedCoverArt { get; set; }
    public TimeSpan? ExpectedDuration { get; set; }

    public DownloadRequest Build() => new()
    {
        JobId = JobId,
        Url = Url,
        Kind = Kind,
        Quality = Quality,
        Container = Container,
        AudioFormat = AudioFormat,
        AudioBitrateKbps = AudioBitrateKbps,
        OutputDirectory = folder,
        FileNameTemplate = FileNameTemplate,
        Title = Title,
        Uploader = Uploader,
        EstimatedBytes = EstimatedBytes,
        CookieBrowser = CookieBrowser,
        EmbedMetadata = EmbedMetadata,
        EmbedCoverArt = EmbedCoverArt,
        ExpectedDuration = ExpectedDuration,
    };
}
