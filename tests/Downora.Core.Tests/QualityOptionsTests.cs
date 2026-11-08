using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Core.Tests;

public class QualityOptionsTests
{
    private static MediaFormat Video(string id, int width, int height, double fps = 30, long? size = null, double? tbr = null, string codec = "avc1.640028", bool approx = false) =>
        new(id, "mp4", width, height, fps, codec, "none", tbr, null, size, approx, "https");

    private static MediaFormat Audio(string id, double abr, long? size, string codec = "mp4a.40.2", string ext = "m4a") =>
        new(id, ext, null, null, null, "none", codec, abr, abr, size, false, "https");

    private static MediaInfo Media(TimeSpan? duration, params MediaFormat[] formats) =>
        new("id", "Title", "Uploader", duration, null, "https://example.com", SourcePlatform.YouTube, "youtube", false, formats);

    private static QualityOption Get(IReadOnlyList<QualityOption> options, QualityPreset preset) => options.Single(o => o.Preset == preset);

    [Fact]
    public void Only_resolutions_the_source_has_are_available()
    {
        var media = Media(TimeSpan.FromMinutes(1),
            Video("137", 1920, 1080, size: 50_000_000),
            Video("136", 1280, 720, size: 25_000_000),
            Video("18", 640, 360, size: 8_000_000),
            Audio("140", 129, 1_000_000));

        var options = QualityOptions.Build(media);

        Assert.True(Get(options, QualityPreset.P1080).IsAvailable);
        Assert.True(Get(options, QualityPreset.P720).IsAvailable);
        Assert.True(Get(options, QualityPreset.P360).IsAvailable);
        Assert.False(Get(options, QualityPreset.P2160).IsAvailable);
        Assert.False(Get(options, QualityPreset.P1440).IsAvailable);
        Assert.False(Get(options, QualityPreset.P480).IsAvailable);
        Assert.Equal(1080, Get(options, QualityPreset.Best).Height);
    }

    [Fact]
    public void Size_is_video_plus_best_audio_and_exact_when_both_are_known()
    {
        var media = Media(TimeSpan.FromMinutes(1),
            Video("137", 1920, 1080, size: 50_000_000),
            Audio("139", 48, 400_000),
            Audio("140", 129, 1_000_000));

        var p1080 = Get(QualityOptions.Build(media), QualityPreset.P1080);

        Assert.Equal(51_000_000, p1080.EstimatedBytes);
        Assert.False(p1080.IsEstimateApproximate);
        Assert.Equal("H.264", p1080.VideoCodec);
    }

    [Fact]
    public void Unknown_size_is_estimated_from_bitrate_and_marked_approximate()
    {
        var media = Media(TimeSpan.FromSeconds(100),
            Video("hls-1080", 1920, 1080, size: null, tbr: 4000),
            Audio("140", 128, 1_600_000));

        var option = Get(QualityOptions.Build(media), QualityPreset.P1080);

        Assert.Equal(50_000_000 + 1_600_000, option.EstimatedBytes);
        Assert.True(option.IsEstimateApproximate);
    }

    [Fact]
    public void Vertical_videos_use_their_short_side()
    {
        var media = Media(null, Video("shorts", 1080, 1920, size: 10_000_000), Audio("140", 129, 100_000));

        var options = QualityOptions.Build(media);

        Assert.True(Get(options, QualityPreset.P1080).IsAvailable);
        Assert.False(Get(options, QualityPreset.P2160).IsAvailable);
    }

    [Fact]
    public void Higher_frame_rate_wins_within_a_band()
    {
        var media = Media(null, Video("30", 1920, 1080, 30, 10), Video("60", 1920, 1080, 60, 20), Audio("a", 128, 1));

        Assert.Equal(60, Get(QualityOptions.Build(media), QualityPreset.P1080).Fps);
    }

    [Fact]
    public void Audio_only_source_offers_best_and_audio_but_no_resolutions()
    {
        // SoundCloud returns audio-only HLS streams with no file size.
        var media = Media(TimeSpan.FromSeconds(214),
            new MediaFormat("hls_aac_96k", "m4a", null, null, null, "none", "mp4a.40.2", 96, 96, null, false, "m3u8_native"),
            new MediaFormat("hls_aac_160k", "m4a", null, null, null, "none", "mp4a.40.2", 160, 160, null, false, "m3u8_native"));

        var options = QualityOptions.Build(media);

        var audioOnly = Get(options, QualityPreset.AudioOnly);
        Assert.True(audioOnly.IsAvailable);
        Assert.Equal((long)(160 * 1000 / 8 * 214.0), audioOnly.EstimatedBytes);
        Assert.True(audioOnly.IsEstimateApproximate);
        Assert.True(Get(options, QualityPreset.Best).IsAvailable);
        Assert.All(options.Where(o => o.Preset is not QualityPreset.Best and not QualityPreset.AudioOnly), o => Assert.False(o.IsAvailable));
        Assert.Equal("hls_aac_160k", QualityOptions.BestAudio(media)!.FormatId);
    }

    [Fact]
    public void Combined_formats_do_not_add_audio_twice()
    {
        var combined = new MediaFormat("18", "mp4", 640, 360, 25, "avc1.42001E", "mp4a.40.2", 266, null, 635_110, true, "https");
        var media = Media(TimeSpan.FromSeconds(19), combined, Audio("140", 129, 309_288));

        var p360 = Get(QualityOptions.Build(media), QualityPreset.P360);

        Assert.Equal(635_110, p360.EstimatedBytes);
        Assert.True(p360.IsEstimateApproximate);
    }

    [Fact]
    public void Nothing_downloadable_means_nothing_available()
    {
        var options = QualityOptions.Build(Media(null));

        Assert.All(options, o => Assert.False(o.IsAvailable));
    }

    [Theory]
    [InlineData("avc1.4D400B", "H.264")]
    [InlineData("vp09.00.10.08", "VP9")]
    [InlineData("vp9", "VP9")]
    [InlineData("av01.0.00M.08", "AV1")]
    [InlineData("hev1.1.6.L93", "H.265")]
    [InlineData("mp4a.40.2", "AAC")]
    [InlineData("opus", "Opus")]
    [InlineData("none", null)]
    [InlineData(null, null)]
    public void Codecs_get_friendly_names(string? codec, string? expected) =>
        Assert.Equal(expected, QualityOptions.ShortCodec(codec));
}
