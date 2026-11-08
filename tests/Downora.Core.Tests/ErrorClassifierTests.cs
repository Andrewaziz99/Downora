using Downora.Core.Enums;
using Downora.Core.Services;

namespace Downora.Core.Tests;

/// <summary>The stderr samples are real yt-dlp 2026.10 output captured while building Phase 3.</summary>
public class ErrorClassifierTests
{
    private const string BotCheck = """
        WARNING: [youtube] aqz-KE-bpKQ: Unable to download webpage: HTTP Error 429: Too Many Requests (caused by <HTTPError 429: Too Many Requests>)
        WARNING: [youtube] No title found in player responses; falling back to title from initial data. Other metadata may also be missing
        ERROR: [youtube] aqz-KE-bpKQ: Sign in to confirm you’re not a bot. Use --cookies-from-browser or --cookies for the authentication.
        """;

    private const string Unavailable = """
        WARNING: [youtube] aaaaaaaaaaa: Unable to download webpage: HTTP Error 429: Too Many Requests (caused by <HTTPError 429: Too Many Requests>)
        ERROR: [youtube] aaaaaaaaaaa: Video unavailable
        """;

    private const string NotFound =
        "ERROR: [generic] nothing-here: Unable to download webpage: HTTP Error 404: Not Found (caused by <HTTPError 404: Not Found>)";

    [Fact]
    public void Youtube_bot_check_needs_sign_in_and_warnings_before_it_are_ignored()
    {
        var (kind, detail) = ErrorClassifier.Classify(BotCheck);

        Assert.Equal(DownloadErrorKind.RequiresSignIn, kind);
        Assert.StartsWith("Sign in to confirm", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Removed_video_is_unavailable_not_a_network_problem()
    {
        var (kind, detail) = ErrorClassifier.Classify(Unavailable);

        Assert.Equal(DownloadErrorKind.Unavailable, kind);
        Assert.Equal("Video unavailable", detail);
    }

    [Fact]
    public void Missing_page_is_unavailable() =>
        Assert.Equal(DownloadErrorKind.Unavailable, ErrorClassifier.Classify(NotFound).Kind);

    [Theory]
    [InlineData("ERROR: Unsupported URL: https://example.com/", DownloadErrorKind.Unsupported)]
    [InlineData("ERROR: [Netflix] 123: This video is DRM protected", DownloadErrorKind.Drm)]
    [InlineData("ERROR: [youtube] x: Private video. Sign in if you've been granted access", DownloadErrorKind.RequiresSignIn)]
    [InlineData("ERROR: [youtube] x: Join this channel to get access to members-only content", DownloadErrorKind.RequiresSignIn)]
    [InlineData("ERROR: [vimeo] 1: The uploader has not made this video available in your country", DownloadErrorKind.GeoRestricted)]
    [InlineData("ERROR: Unable to download webpage: <urlopen error [Errno 11001] getaddrinfo failed>", DownloadErrorKind.Network)]
    [InlineData("ERROR: [youtube] x: Read timed out.", DownloadErrorKind.Network)]
    [InlineData("ERROR: unable to write data: [Errno 28] No space left on device", DownloadErrorKind.DiskFull)]
    [InlineData("ERROR: something nobody has seen before", DownloadErrorKind.Unknown)]
    public void Common_failures_are_classified(string stderr, DownloadErrorKind expected) =>
        Assert.Equal(expected, ErrorClassifier.Classify(stderr).Kind);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_output_is_unknown(string? stderr)
    {
        var (kind, detail) = ErrorClassifier.Classify(stderr);

        Assert.Equal(DownloadErrorKind.Unknown, kind);
        Assert.Null(detail);
    }

    [Fact]
    public void A_warning_alone_is_not_an_error_line() =>
        Assert.Null(ErrorClassifier.LastErrorLine("WARNING: [soundcloud] Original download format is only available for registered users."));
}
