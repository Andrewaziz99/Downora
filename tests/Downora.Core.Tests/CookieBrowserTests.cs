using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Core.Tests;

public class CookieBrowserTests
{
    private static AppSettings Settings(bool use, string? browser) =>
        new() { DownloadDirectory = @"C:\Downloads", UseBrowserSession = use, BrowserForCookies = browser };

    [Fact]
    public void Browser_sign_in_is_off_by_default()
    {
        var defaults = AppSettings.CreateDefault(@"C:\Downloads");

        Assert.False(defaults.UseBrowserSession);
        Assert.Null(CookieBrowsers.For(defaults));
    }

    [Theory]
    [InlineData("edge")]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("brave")]
    public void A_supported_browser_is_used_only_when_the_option_is_on(string browser)
    {
        Assert.Equal(browser, CookieBrowsers.For(Settings(true, browser)));
        Assert.Null(CookieBrowsers.For(Settings(false, browser)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Edge")]
    [InlineData("safari")]
    [InlineData("edge:Profile 1")]
    [InlineData("chrome --exec calc")]
    public void Anything_unsupported_is_never_passed_on(string? browser)
    {
        Assert.False(CookieBrowsers.IsSupported(browser));
        Assert.Null(CookieBrowsers.For(Settings(true, browser)));
    }

    [Fact]
    public void The_four_browsers_the_app_offers_are_the_supported_ones() =>
        Assert.Equal(["edge", "chrome", "firefox", "brave"], CookieBrowsers.Supported);

    [Fact]
    public void Settings_with_an_unsupported_browser_are_rejected()
    {
        var errors = AppSettingsValidator.Validate(Settings(true, "netscape"));

        Assert.Contains(errors, e => e.Contains("must be one of", StringComparison.Ordinal));
    }

    [Fact]
    public void Turning_the_option_on_requires_choosing_a_browser()
    {
        Assert.NotEmpty(AppSettingsValidator.Validate(Settings(true, null)));
        Assert.Empty(AppSettingsValidator.Validate(Settings(true, "edge")));
        Assert.Empty(AppSettingsValidator.Validate(Settings(false, null)));
        Assert.Empty(AppSettingsValidator.Validate(Settings(false, "firefox")));
    }

    [Theory]
    [InlineData("ERROR: Could not copy Chrome cookie database. See https://github.com/yt-dlp/yt-dlp/issues/7271 for more info")]
    [InlineData("ERROR: Failed to decrypt with DPAPI. See https://github.com/yt-dlp/yt-dlp/issues/10927")]
    [InlineData("ERROR: could not find firefox cookies database in C:\\Users\\me")]
    public void Unreadable_browser_cookies_are_their_own_kind_of_error(string stderr)
    {
        var (kind, detail) = ErrorClassifier.Classify(stderr);

        Assert.Equal(DownloadErrorKind.BrowserCookies, kind);
        Assert.False(string.IsNullOrWhiteSpace(detail));
    }

    [Fact]
    public void The_usual_sign_in_message_is_still_a_sign_in_error()
    {
        var (kind, _) = ErrorClassifier.Classify(
            "ERROR: [youtube] abc: Sign in to confirm you're not a bot. Use --cookies-from-browser or --cookies for the authentication.");

        Assert.Equal(DownloadErrorKind.RequiresSignIn, kind);
    }
}
