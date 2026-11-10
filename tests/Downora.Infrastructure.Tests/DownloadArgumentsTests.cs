using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Infrastructure.Downloads;

namespace Downora.Infrastructure.Tests;

public class DownloadArgumentsTests
{
    private static readonly DownloadLayout Layout = new(
        @"C:\tools\ffmpeg.exe", @"C:\tools\deno.exe", @"D:\Downloads", @"D:\Downloads\.downora-partial\abc", "My Song");

    private static DownloadRequest Request(Action<DownloadRequestBuilder>? tweak = null)
    {
        var builder = new DownloadRequestBuilder(@"D:\Downloads");
        tweak?.Invoke(builder);
        return builder.Build();
    }

    private static List<string> Build(Action<DownloadRequestBuilder>? tweak = null, DownloadLayout? layout = null) =>
        [.. YtDlpDownloadArguments.Build(Request(tweak), layout ?? Layout)];

    private static string ValueOf(List<string> args, string option) => args[args.IndexOf(option) + 1];

    [Fact]
    public void Tags_and_cover_art_are_only_asked_for_when_chosen()
    {
        var off = Build();
        Assert.DoesNotContain("--embed-metadata", off);
        Assert.DoesNotContain("--embed-thumbnail", off);

        var on = Build(b => { b.EmbedMetadata = true; b.EmbedCoverArt = true; });
        Assert.Contains("--embed-metadata", on);
        Assert.Contains("--embed-thumbnail", on);
        Assert.Equal("jpg", ValueOf(on, "--convert-thumbnails")); // WebP covers aren't read by most players
        Assert.True(on.IndexOf("--convert-thumbnails") < on.IndexOf("--embed-thumbnail"));
    }

    [Theory]
    [InlineData(MediaKind.Audio, AudioFormat.Mp3, VideoContainer.Mp4, true)]
    [InlineData(MediaKind.Audio, AudioFormat.Flac, VideoContainer.Mp4, true)]
    [InlineData(MediaKind.Audio, AudioFormat.Opus, VideoContainer.Mp4, true)]
    [InlineData(MediaKind.Audio, AudioFormat.Wav, VideoContainer.Mp4, false)]
    [InlineData(MediaKind.Video, AudioFormat.Mp3, VideoContainer.Mp4, true)]
    [InlineData(MediaKind.Video, AudioFormat.Mp3, VideoContainer.Mkv, true)]
    [InlineData(MediaKind.Video, AudioFormat.Mp3, VideoContainer.WebM, false)]
    public void Cover_art_is_skipped_for_formats_that_cannot_hold_it(MediaKind kind, AudioFormat audio, VideoContainer container, bool expected)
    {
        var args = Build(b => { b.Kind = kind; b.AudioFormat = audio; b.Container = container; b.EmbedCoverArt = true; });

        Assert.Equal(expected, args.Contains("--embed-thumbnail"));
    }

    [Fact]
    public void Lossless_audio_gets_no_bitrate_and_its_own_format()
    {
        var flac = Build(b => { b.Kind = MediaKind.Audio; b.AudioFormat = AudioFormat.Flac; });
        var wav = Build(b => { b.Kind = MediaKind.Audio; b.AudioFormat = AudioFormat.Wav; });

        Assert.Equal("flac", ValueOf(flac, "--audio-format"));
        Assert.Equal("wav", ValueOf(wav, "--audio-format"));
        Assert.DoesNotContain("--audio-quality", flac);
        Assert.DoesNotContain("--audio-quality", wav);
    }

    [Fact]
    public void The_link_always_comes_last_after_two_dashes()
    {
        var args = Build();

        Assert.Equal(["--", "https://www.youtube.com/watch?v=jNQXAC9IVRw"], args.TakeLast(2));
        Assert.Single(args, a => a.Contains("youtube.com", StringComparison.Ordinal));
    }

    [Fact]
    public void A_link_that_looks_like_an_option_is_still_just_a_link()
    {
        var args = Build(b => b.Url = "https://example.com/watch?x=--exec%20calc");

        Assert.Equal("--", args[^2]);
        Assert.Equal("https://example.com/watch?x=--exec%20calc", args[^1]);
    }

    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.com/a.mp4")]
    [InlineData("--exec calc")]
    [InlineData("not a url")]
    public void Only_web_links_are_accepted(string url) =>
        Assert.Throws<ArgumentException>(() => Build(b => b.Url = url));

    [Fact]
    public void Every_download_is_isolated_from_the_users_yt_dlp_config_and_never_a_playlist()
    {
        var args = Build();

        Assert.Contains("--ignore-config", args);
        Assert.Contains("--no-playlist", args);
        Assert.Contains("--no-mtime", args);
        Assert.Contains("--windows-filenames", args);
        Assert.Contains("--newline", args);
        Assert.Contains("--progress", args);
        Assert.Equal("never", ValueOf(args, "--color"));
    }

    [Fact]
    public void Tool_locations_and_folders_are_passed()
    {
        var args = Build();

        Assert.Equal(@"C:\tools\ffmpeg.exe", ValueOf(args, "--ffmpeg-location"));
        Assert.Equal(@"deno:C:\tools\deno.exe", ValueOf(args, "--js-runtimes"));
        Assert.Contains(@"home:D:\Downloads", args);
        Assert.Contains(@"temp:D:\Downloads\.downora-partial\abc", args);
        Assert.Equal("My Song.%(ext)s", ValueOf(args, "-o"));
    }

    [Fact]
    public void Without_deno_the_runtime_option_is_left_out()
    {
        var args = Build(layout: Layout with { DenoPath = null });

        Assert.DoesNotContain("--js-runtimes", args);
    }

    [Fact]
    public void Percent_signs_in_a_name_are_escaped_so_yt_dlp_does_not_treat_them_as_fields()
    {
        var args = Build(layout: Layout with { BaseName = "100% Pure %(title)s" });

        Assert.Equal("100%% Pure %%(title)s.%(ext)s", ValueOf(args, "-o"));
    }

    [Fact]
    public void Names_with_spaces_quotes_and_ampersands_stay_one_argument()
    {
        var args = Build(layout: Layout with { BaseName = "Tom & Jerry's \"best\" ^ | > <" });

        Assert.Single(args, a => a.StartsWith("Tom & Jerry's", StringComparison.Ordinal));
    }

    [Fact]
    public void The_progress_and_file_templates_are_what_the_parser_reads()
    {
        var args = Build();
        var templates = args.Where((a, i) => i > 0 && args[i - 1] == "--progress-template").ToList();

        Assert.Equal([YtDlpProgressParser.DownloadTemplate, YtDlpProgressParser.PostProcessTemplate], templates);
        Assert.Equal(YtDlpProgressParser.FileTemplate, ValueOf(args, "--print"));
    }

    // ---- Video ----

    [Theory]
    [InlineData(QualityPreset.Best, "res,fps,vcodec:h264,acodec:aac")]
    [InlineData(QualityPreset.P2160, "res,fps,vcodec:h264,acodec:aac")]
    [InlineData(QualityPreset.P1440, "res:1440,fps,vcodec:h264,acodec:aac")]
    [InlineData(QualityPreset.P1080, "res:1080,fps,vcodec:h264,acodec:aac")]
    [InlineData(QualityPreset.P720, "res:720,fps,vcodec:h264,acodec:aac")]
    [InlineData(QualityPreset.P480, "res:480,fps,vcodec:h264,acodec:aac")]
    [InlineData(QualityPreset.P360, "res:360,fps,vcodec:h264,acodec:aac")]
    public void Video_quality_caps_resolution_by_the_shorter_side(QualityPreset quality, string sort)
    {
        var args = Build(b => b.Quality = quality);

        Assert.Equal("bv*+ba/b", ValueOf(args, "-f"));
        Assert.Equal(sort, ValueOf(args, "-S"));
        Assert.DoesNotContain("-x", args);
        Assert.DoesNotContain("--audio-format", args);
    }

    [Theory]
    [InlineData(VideoContainer.Mp4, "mp4/mkv")]
    [InlineData(VideoContainer.Mkv, "mkv")]
    [InlineData(VideoContainer.WebM, "webm/mkv")]
    public void The_container_falls_back_to_mkv_when_the_codecs_do_not_fit(VideoContainer container, string expected)
    {
        var args = Build(b => b.Container = container);

        Assert.Equal(expected, ValueOf(args, "--merge-output-format"));
    }

    [Fact]
    public void Codec_preference_is_only_added_when_mp4_is_wanted()
    {
        var args = Build(b =>
        {
            b.Container = VideoContainer.Mkv;
            b.Quality = QualityPreset.P720;
        });

        Assert.Equal("res:720,fps", ValueOf(args, "-S"));
    }

    // ---- Audio ----

    [Theory]
    [InlineData(AudioFormat.Mp3, "mp3", "ba/b", true)]
    [InlineData(AudioFormat.M4a, "m4a", "ba[ext=m4a]/ba/b", true)]
    [InlineData(AudioFormat.Opus, "opus", "ba[acodec=opus]/ba/b", true)]
    [InlineData(AudioFormat.Wav, "wav", "ba/b", false)]
    [InlineData(AudioFormat.Flac, "flac", "ba/b", false)]
    public void Audio_downloads_extract_to_the_chosen_format(AudioFormat format, string name, string selector, bool lossy)
    {
        var args = Build(b =>
        {
            b.Kind = MediaKind.Audio;
            b.AudioFormat = format;
            b.AudioBitrateKbps = 192;
        });

        Assert.Contains("-x", args);
        Assert.Equal(name, ValueOf(args, "--audio-format"));
        Assert.Equal(selector, ValueOf(args, "-f"));
        Assert.DoesNotContain("--merge-output-format", args);
        Assert.DoesNotContain("-S", args);
        Assert.Equal(lossy, args.Contains("--audio-quality"));
        if (lossy)
        {
            Assert.Equal("192K", ValueOf(args, "--audio-quality"));
        }
    }

    // ---- Browser sign-in ----

    [Fact]
    public void Browser_sign_in_is_off_unless_asked_for()
    {
        Assert.DoesNotContain("--cookies-from-browser", Build());
        Assert.DoesNotContain("--cookies", Build());
    }

    [Theory]
    [InlineData("edge")]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("brave")]
    public void Supported_browsers_are_passed_by_name_only(string browser)
    {
        var args = Build(b => b.CookieBrowser = browser);

        Assert.Equal(browser, ValueOf(args, "--cookies-from-browser"));
        Assert.DoesNotContain("--cookies", args);
    }

    [Theory]
    [InlineData("opera:Profile 1")]
    [InlineData("edge --exec calc")]
    [InlineData("")]
    public void Anything_else_is_rejected_rather_than_passed_to_the_tool(string browser) =>
        Assert.Throws<ArgumentException>(() => Build(b => b.CookieBrowser = browser));
}
