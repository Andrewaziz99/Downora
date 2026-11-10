using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Services;
using Downora.Infrastructure.Downloads;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

public class FileVerificationRulesTests
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    [Fact]
    public void A_file_with_the_right_streams_and_length_passes() =>
        Assert.True(FileVerificationRules.Evaluate(new MediaProbe(Minute, true, true), MediaKind.Video, Minute).IsValid);

    [Fact]
    public void A_cover_picture_does_not_make_audio_into_video_and_audio_needs_no_video()
    {
        // FFprobe reports an embedded cover as a video stream; an audio file must still pass either way.
        Assert.True(FileVerificationRules.Evaluate(new MediaProbe(Minute, true, true), MediaKind.Audio, Minute).IsValid);
        Assert.True(FileVerificationRules.Evaluate(new MediaProbe(Minute, false, true), MediaKind.Audio, Minute).IsValid);
    }

    [Theory]
    [InlineData(MediaKind.Audio, false, false)]
    [InlineData(MediaKind.Audio, true, false)]
    [InlineData(MediaKind.Video, false, true)]
    public void A_file_missing_the_kind_of_stream_that_was_asked_for_fails(MediaKind kind, bool hasVideo, bool hasAudio) =>
        Assert.False(FileVerificationRules.Evaluate(new MediaProbe(Minute, hasVideo, hasAudio), kind, Minute).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void A_file_without_a_length_fails(double? seconds) =>
        Assert.False(FileVerificationRules.Evaluate(
            new MediaProbe(seconds is { } s ? TimeSpan.FromSeconds(s) : null, true, true), MediaKind.Video, null).IsValid);

    [Theory]
    [InlineData(600, 590, true)] // 10 s short of 10 minutes is inside the 10% allowance
    [InlineData(600, 539, false)] // more than 10% short: cut off
    [InlineData(600, 700, true)] // longer is fine
    [InlineData(20, 15.5, true)] // short clips use the 5 s allowance, not 10%
    [InlineData(20, 14, false)]
    public void A_file_much_shorter_than_expected_was_cut_off(double expected, double actual, bool valid) =>
        Assert.Equal(valid, FileVerificationRules.Evaluate(
            new MediaProbe(TimeSpan.FromSeconds(actual), true, true), MediaKind.Video, TimeSpan.FromSeconds(expected)).IsValid);

    [Fact]
    public void An_unknown_expected_length_is_not_checked() =>
        Assert.True(FileVerificationRules.Evaluate(new MediaProbe(TimeSpan.FromSeconds(1), true, true), MediaKind.Video, null).IsValid);
}

public sealed class FfprobeFileVerifierTests
{
    private const string VideoJson = """{"streams":[{"codec_type":"video"},{"codec_type":"audio"}],"format":{"duration":"19.233000"}}""";

    private readonly IToolManager _tools = Substitute.For<IToolManager>();
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();

    private FfprobeFileVerifier Verifier()
    {
        _tools.GetPath(ToolKind.Ffprobe).Returns("C:/tools/ffprobe.exe");
        return new FfprobeFileVerifier(_tools, _runner, NullLogger<FfprobeFileVerifier>.Instance);
    }

    private void Probe(int exit, string output, string error = "", bool timedOut = false) =>
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<Action<string>?>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(exit, output, error, timedOut));

    [Fact]
    public void The_json_is_read_for_streams_and_length()
    {
        var probe = FfprobeFileVerifier.Parse(VideoJson);

        Assert.NotNull(probe);
        Assert.True(probe.HasVideo);
        Assert.True(probe.HasAudio);
        Assert.Equal(TimeSpan.FromSeconds(19.233), probe.Duration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void Anything_that_is_not_the_expected_document_is_rejected(string json) =>
        Assert.Null(FfprobeFileVerifier.Parse(json));

    [Fact]
    public void A_missing_length_or_stream_list_reads_as_nothing_found()
    {
        var probe = FfprobeFileVerifier.Parse("{}");

        Assert.NotNull(probe);
        Assert.Null(probe.Duration);
        Assert.False(probe.HasVideo);
        Assert.False(probe.HasAudio);
    }

    [Fact]
    public void The_path_is_one_argument_after_dash_i_never_part_of_a_command_line()
    {
        var args = FfprobeFileVerifier.Arguments(@"D:\My Videos\-weird & name.mp4");

        Assert.Equal(@"D:\My Videos\-weird & name.mp4", args[^1]);
        Assert.Equal("-i", args[^2]);
    }

    [Fact]
    public async Task A_good_file_is_valid()
    {
        Probe(0, VideoJson);

        var result = await Verifier().VerifyAsync("a.mp4", MediaKind.Video, TimeSpan.FromSeconds(19), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.True(result.WasChecked);
    }

    [Fact]
    public async Task A_file_ffprobe_cannot_open_is_invalid()
    {
        Probe(1, string.Empty, "Invalid data found when processing input");

        var result = await Verifier().VerifyAsync("a.mp4", MediaKind.Video, null, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Problem);
    }

    [Fact]
    public async Task Without_ffprobe_the_file_is_accepted_unchecked()
    {
        _tools.GetPath(ToolKind.Ffprobe).Returns((string?)null);
        var verifier = new FfprobeFileVerifier(_tools, _runner, NullLogger<FfprobeFileVerifier>.Instance);

        var result = await verifier.VerifyAsync("a.mp4", MediaKind.Video, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.False(result.WasChecked);
    }

    [Fact]
    public async Task A_probe_that_times_out_says_nothing_about_the_file()
    {
        Probe(-1, string.Empty, timedOut: true);

        var result = await Verifier().VerifyAsync("a.mp4", MediaKind.Video, null, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.False(result.WasChecked);
    }
}
