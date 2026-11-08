using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Core.Tests;

public class UrlAndSourceTests
{
    private static readonly SourceDetector Sources = new();
    private static readonly UrlValidator Validator = new(Sources);

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw", SourcePlatform.YouTube)]
    [InlineData("https://m.youtube.com/watch?v=jNQXAC9IVRw", SourcePlatform.YouTube)]
    [InlineData("https://music.youtube.com/watch?v=jNQXAC9IVRw", SourcePlatform.YouTube)]
    [InlineData("https://youtu.be/jNQXAC9IVRw", SourcePlatform.YouTube)]
    [InlineData("https://www.youtube.com/shorts/abcdefghijk", SourcePlatform.YouTubeShorts)]
    [InlineData("https://soundcloud.com/forss/flickermood", SourcePlatform.SoundCloud)]
    [InlineData("https://on.soundcloud.com/abc", SourcePlatform.SoundCloud)]
    [InlineData("https://vimeo.com/76979871", SourcePlatform.Vimeo)]
    [InlineData("https://player.vimeo.com/video/76979871", SourcePlatform.Vimeo)]
    [InlineData("https://www.dailymotion.com/video/x7tgad0", SourcePlatform.Dailymotion)]
    [InlineData("https://dai.ly/x7tgad0", SourcePlatform.Dailymotion)]
    [InlineData("https://www.facebook.com/watch/?v=123", SourcePlatform.Facebook)]
    [InlineData("https://fb.watch/abc/", SourcePlatform.Facebook)]
    [InlineData("https://www.instagram.com/reel/abc/", SourcePlatform.Instagram)]
    [InlineData("https://x.com/user/status/1", SourcePlatform.X)]
    [InlineData("https://twitter.com/user/status/1", SourcePlatform.X)]
    [InlineData("https://cdn.example.com/media/clip.MP4", SourcePlatform.DirectFile)]
    [InlineData("https://example.com/song.mp3?token=1", SourcePlatform.DirectFile)]
    [InlineData("https://example.com/page", SourcePlatform.Other)]
    [InlineData("https://notyoutube.com/watch?v=jNQXAC9IVRw", SourcePlatform.Other)]
    public void Platform_is_detected_from_the_host(string url, SourcePlatform expected) =>
        Assert.Equal(expected, Sources.Detect(new Uri(url)));

    [Theory]
    [InlineData("https://youtu.be/jNQXAC9IVRw?si=tracking", "https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("https://m.youtube.com/watch?v=jNQXAC9IVRw&feature=share", "https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com/shorts/abcdefghijk", "https://www.youtube.com/watch?v=abcdefghijk")]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw&list=PL123&index=2", "https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com/playlist?list=PL123", "https://www.youtube.com/playlist?list=PL123")]
    [InlineData("HTTPS://SoundCloud.com/forss/flickermood/?utm_source=x&in=set", "https://soundcloud.com/forss/flickermood?in=set")]
    [InlineData("https://vimeo.com/76979871#t=10", "https://vimeo.com/76979871")]
    public void Normalization_removes_tracking_and_unifies_youtube_links(string url, string expected) =>
        Assert.Equal(expected, Sources.Normalize(new Uri(url)));

    [Fact]
    public void A_video_pasted_from_a_playlist_is_analyzed_as_a_single_video()
    {
        Assert.True(Sources.IsSingleItemInPlaylist(new Uri("https://www.youtube.com/watch?v=jNQXAC9IVRw&list=PL123")));
        Assert.False(Sources.IsSingleItemInPlaylist(new Uri("https://www.youtube.com/playlist?list=PL123")));
        Assert.False(Sources.IsSingleItemInPlaylist(new Uri("https://www.youtube.com/watch?v=jNQXAC9IVRw")));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("  https://youtu.be/jNQXAC9IVRw  ")]
    [InlineData("http://example.com/clip.mp4")]
    public void Valid_links_pass(string input)
    {
        var result = Validator.Validate(input);

        Assert.True(result.IsValid);
        Assert.Equal(UrlProblem.None, result.Problem);
        Assert.NotNull(result.NormalizedUrl);
    }

    [Theory]
    [InlineData("youtube.com/watch?v=jNQXAC9IVRw", "https://youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("www.example.com/a.mp4", "https://www.example.com/a.mp4")]
    public void A_missing_scheme_becomes_https(string input, string expected) =>
        Assert.Equal(expected, Validator.Validate(input).Url!.AbsoluteUri);

    [Theory]
    [InlineData(null, UrlProblem.Empty)]
    [InlineData("", UrlProblem.Empty)]
    [InlineData("   ", UrlProblem.Empty)]
    [InlineData("hello world", UrlProblem.NotAUrl)]
    [InlineData("not a link", UrlProblem.NotAUrl)]
    [InlineData("justtext", UrlProblem.NotAUrl)]
    [InlineData("--exec calc.exe", UrlProblem.NotAUrl)]
    [InlineData("-o C:/Windows/x", UrlProblem.NotAUrl)]
    [InlineData("https://example.com/a b", UrlProblem.NotAUrl)]
    [InlineData("https://example.com/a\nb", UrlProblem.NotAUrl)]
    [InlineData("https://localhost/x", UrlProblem.NotAUrl)]
    [InlineData("file:///C:/Windows/win.ini", UrlProblem.UnsupportedScheme)]
    [InlineData("ftp://example.com/a.mp4", UrlProblem.UnsupportedScheme)]
    [InlineData("javascript:alert(1)", UrlProblem.NotAUrl)]
    public void Bad_input_is_rejected_before_anything_runs(string? input, UrlProblem expected) =>
        Assert.Equal(expected, Validator.Validate(input).Problem);

    [Fact]
    public void Overlong_links_are_rejected() =>
        Assert.Equal(UrlProblem.TooLong, Validator.Validate("https://example.com/" + new string('a', UrlValidator.MaxLength)).Problem);
}
