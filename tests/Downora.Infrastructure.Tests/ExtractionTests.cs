using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Infrastructure.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

/// <summary>Records what would be run and answers with canned output.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Func<ProcessSpec, ProcessResult> _respond;

    public FakeProcessRunner(Func<ProcessSpec, ProcessResult> respond) => _respond = respond;

    public List<ProcessSpec> Calls { get; } = [];

    public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onOutputLine = null, Action<string>? onErrorLine = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(spec);
        return Task.FromResult(_respond(spec));
    }

    public static ProcessResult Ok(string stdout) => new(0, stdout, string.Empty, false);

    public static ProcessResult Fail(string stderr, int exitCode = 1) => new(exitCode, "null", stderr, false);
}

public class ExtractionTests
{
    private static readonly UrlValidator Validator = new(new SourceDetector());

    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static IToolManager Tools(bool ytDlp = true, bool ffmpeg = false, bool deno = false)
    {
        var tools = Substitute.For<IToolManager>();
        tools.GetPath(ToolKind.YtDlp).Returns(ytDlp ? "C:/tools/yt-dlp.exe" : null);
        tools.GetPath(ToolKind.Ffmpeg).Returns(ffmpeg ? "C:/tools/ffmpeg.exe" : null);
        tools.GetPath(ToolKind.Deno).Returns(deno ? "C:/tools/deno.exe" : null);
        return tools;
    }

    private static YtDlpMediaExtractor Extractor(FakeProcessRunner runner, IToolManager? tools = null) =>
        new(tools ?? Tools(), runner, NullLogger<YtDlpMediaExtractor>.Instance);

    // ---- Parsing real yt-dlp output ----

    [Fact]
    public void Youtube_video_is_parsed_with_its_formats()
    {
        var result = YtDlpJsonParser.Parse(Fixture("yt_video.json"), SourcePlatform.YouTube);

        Assert.Equal(AnalysisKind.Media, result.Kind);
        var media = result.Media!;
        Assert.Equal("jNQXAC9IVRw", media.Id);
        Assert.Equal("Me at the zoo", media.Title);
        Assert.Equal("jawed", media.Uploader);
        Assert.Equal(TimeSpan.FromSeconds(19), media.Duration);
        Assert.StartsWith("https://i.ytimg.com/vi/jNQXAC9IVRw/", media.ThumbnailUrl, StringComparison.Ordinal);
        Assert.Equal("https://www.youtube.com/watch?v=jNQXAC9IVRw", media.WebpageUrl);
        Assert.Equal(SourcePlatform.YouTube, media.Platform);
        Assert.False(media.IsLive);

        Assert.Contains(media.Formats, f => f is { FormatId: "140", HasAudio: true, HasVideo: false, FileSize: 309288 });
        Assert.Contains(media.Formats, f => f is { FormatId: "18", HasAudio: true, HasVideo: true, Height: 240, IsSizeApproximate: true });
        Assert.DoesNotContain(media.Formats, f => f.Extension == "mhtml");
    }

    [Fact]
    public void Youtube_video_offers_its_real_qualities()
    {
        var media = YtDlpJsonParser.Parse(Fixture("yt_video.json"), SourcePlatform.YouTube).Media!;

        var options = QualityOptions.Build(media);

        // "Me at the zoo" (2005) tops out at 240p, which falls in the 360p band.
        var p360 = options.Single(o => o.Preset == QualityPreset.P360);
        Assert.True(p360.IsAvailable);
        Assert.Equal(240, p360.Height);
        Assert.False(options.Single(o => o.Preset == QualityPreset.P720).IsAvailable);
        Assert.True(options.Single(o => o.Preset == QualityPreset.AudioOnly).IsAvailable);
        Assert.Equal("140", QualityOptions.BestAudio(media)!.FormatId);
    }

    [Fact]
    public void Soundcloud_track_is_audio_only()
    {
        var media = YtDlpJsonParser.Parse(Fixture("sc_track.json"), SourcePlatform.SoundCloud).Media!;

        Assert.Equal("Flickermood", media.Title);
        Assert.Equal("Forss", media.Uploader);
        Assert.Equal(SourcePlatform.SoundCloud, media.Platform);
        Assert.All(media.Formats, f => Assert.False(f.HasVideo));
        Assert.Equal("hls_aac_160k", QualityOptions.BestAudio(media)!.FormatId);
    }

    [Fact]
    public void Playlist_lists_entries_and_marks_private_ones()
    {
        var result = YtDlpJsonParser.Parse(Fixture("yt_playlist.json"), SourcePlatform.YouTube);

        Assert.Equal(AnalysisKind.Playlist, result.Kind);
        var playlist = result.Playlist!;
        Assert.Equal("Rick Astley: Official Music Videos", playlist.Title);
        Assert.Equal("Rick Astley", playlist.Uploader);
        Assert.Equal(43, playlist.TotalCount);
        Assert.Equal(7, playlist.Entries.Count);

        var first = playlist.Entries[0];
        Assert.Equal("LLFhKaqnWwk", first.Id);
        Assert.Equal("https://www.youtube.com/watch?v=LLFhKaqnWwk", first.Url);
        Assert.Equal(TimeSpan.FromSeconds(213), first.Duration);
        Assert.NotNull(first.ThumbnailUrl);
        Assert.True(first.IsAvailable);

        Assert.False(playlist.Entries[^1].IsAvailable);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void Non_object_output_is_a_failure(string json) =>
        Assert.Equal(AnalysisKind.Failed, YtDlpJsonParser.Parse(json, SourcePlatform.Other).Kind);

    [Fact]
    public void Odd_numbers_and_missing_fields_are_tolerated()
    {
        const string json = """{"id":"x","formats":[{"format_id":"a","height":1080.0,"width":1920,"fps":29.97,"vcodec":"avc1","acodec":"none","filesize":null,"filesize_approx":1234.6}]}""";

        var media = YtDlpJsonParser.Parse(json, SourcePlatform.Other).Media!;

        Assert.Equal("x", media.Title);
        var format = Assert.Single(media.Formats);
        Assert.Equal(1080, format.Height);
        Assert.Equal(1235, format.FileSize);
        Assert.True(format.IsSizeApproximate);
    }

    // ---- Running yt-dlp ----

    [Fact]
    public async Task Url_always_comes_after_double_dash_and_config_is_ignored()
    {
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok(Fixture("yt_video.json")));
        var url = Validator.Validate("https://www.youtube.com/watch?v=jNQXAC9IVRw");

        var result = await Extractor(runner).AnalyzeAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisKind.Media, result.Kind);
        var spec = Assert.Single(runner.Calls);
        Assert.Equal("C:/tools/yt-dlp.exe", spec.FileName);
        Assert.Equal("--", spec.Arguments[^2]);
        Assert.Equal("https://www.youtube.com/watch?v=jNQXAC9IVRw", spec.Arguments[^1]);
        Assert.Contains("--ignore-config", spec.Arguments);
        Assert.Contains("--dump-single-json", spec.Arguments);
        Assert.DoesNotContain("--no-playlist", spec.Arguments);
        Assert.Equal("utf-8", spec.Environment!["PYTHONIOENCODING"]);
        Assert.Equal(YtDlpMediaExtractor.AnalyzeTimeout, spec.Timeout);
    }

    [Fact]
    public async Task Video_from_a_playlist_link_is_analyzed_alone()
    {
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok(Fixture("yt_video.json")));

        await Extractor(runner).AnalyzeAsync(Validator.Validate("https://www.youtube.com/watch?v=jNQXAC9IVRw&list=PL1"), TestContext.Current.CancellationToken);

        Assert.Contains("--no-playlist", runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task Ffmpeg_and_deno_are_passed_when_installed()
    {
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok(Fixture("yt_video.json")));

        await Extractor(runner, Tools(ffmpeg: true, deno: true))
            .AnalyzeAsync(Validator.Validate("https://youtu.be/jNQXAC9IVRw"), TestContext.Current.CancellationToken);

        var args = runner.Calls[0].Arguments.ToList();
        Assert.Equal("C:/tools/ffmpeg.exe", args[args.IndexOf("--ffmpeg-location") + 1]);
        Assert.Equal("deno:C:/tools/deno.exe", args[args.IndexOf("--js-runtimes") + 1]);
        Assert.True(args.IndexOf("--js-runtimes") < args.IndexOf("--"));
    }

    [Theory]
    [InlineData("bot_check.err", DownloadErrorKind.RequiresSignIn)]
    [InlineData("unavailable.err", DownloadErrorKind.Unavailable)]
    [InlineData("unsupported.err", DownloadErrorKind.Unavailable)]
    public async Task Failures_are_classified_from_real_stderr(string fixture, DownloadErrorKind expected)
    {
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Fail(Fixture(fixture)));

        var result = await Extractor(runner).AnalyzeAsync(Validator.Validate("https://example.com/x"), TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisKind.Failed, result.Kind);
        Assert.Equal(expected, result.ErrorKind);
        Assert.NotNull(result.ErrorDetail);
    }

    [Fact]
    public async Task Missing_yt_dlp_is_reported_without_running_anything()
    {
        var runner = new FakeProcessRunner(_ => throw new InvalidOperationException("must not run"));

        var result = await Extractor(runner, Tools(ytDlp: false)).AnalyzeAsync(Validator.Validate("https://youtu.be/jNQXAC9IVRw"), TestContext.Current.CancellationToken);

        Assert.Equal(DownloadErrorKind.ToolMissing, result.ErrorKind);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Timeout_is_a_network_failure()
    {
        var runner = new FakeProcessRunner(_ => new ProcessResult(-1, string.Empty, string.Empty, TimedOut: true));

        var result = await Extractor(runner).AnalyzeAsync(Validator.Validate("https://youtu.be/jNQXAC9IVRw"), TestContext.Current.CancellationToken);

        Assert.Equal(DownloadErrorKind.Network, result.ErrorKind);
    }

    [Fact]
    public async Task Unreadable_output_is_reported_not_thrown()
    {
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok("{not json"));

        var result = await Extractor(runner).AnalyzeAsync(Validator.Validate("https://youtu.be/jNQXAC9IVRw"), TestContext.Current.CancellationToken);

        Assert.Equal(AnalysisKind.Failed, result.Kind);
        Assert.Equal(DownloadErrorKind.Unknown, result.ErrorKind);
    }

    [Fact]
    public async Task Invalid_urls_never_reach_the_tool()
    {
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Extractor(runner).AnalyzeAsync(Validator.Validate("--exec calc"), TestContext.Current.CancellationToken));
        Assert.Empty(runner.Calls);
    }
}
