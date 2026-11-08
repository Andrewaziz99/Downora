using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Core.Tests;

public class AppSettingsValidatorTests
{
    private static readonly string ValidFolder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Downora"));

    private static AppSettings Valid() => AppSettings.CreateDefault(ValidFolder);

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.Empty(AppSettingsValidator.Validate(Valid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative\\folder")]
    public void Download_folder_must_be_a_full_path(string folder)
    {
        var settings = Valid();
        settings.DownloadDirectory = folder;

        Assert.Single(AppSettingsValidator.Validate(settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-3)]
    public void Concurrent_downloads_outside_1_to_10_are_rejected(int value)
    {
        var settings = Valid();
        settings.MaxConcurrentDownloads = value;

        var error = Assert.Single(AppSettingsValidator.Validate(settings));
        Assert.Contains("between 1 and 10", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Concurrent_download_limits_are_inclusive(int value)
    {
        var settings = Valid();
        settings.MaxConcurrentDownloads = value;

        Assert.Empty(AppSettingsValidator.Validate(settings));
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(192, true)]
    [InlineData(256, true)]
    [InlineData(320, true)]
    [InlineData(64, false)]
    [InlineData(500, false)]
    public void Only_supported_bitrates_are_accepted(int kbps, bool valid)
    {
        var settings = Valid();
        settings.DefaultAudioBitrateKbps = kbps;

        Assert.Equal(valid, AppSettingsValidator.Validate(settings).Count == 0);
    }

    [Fact]
    public void History_retention_must_be_positive_when_set()
    {
        var settings = Valid();
        settings.HistoryRetentionDays = 0;
        Assert.Single(AppSettingsValidator.Validate(settings));

        settings.HistoryRetentionDays = null;
        Assert.Empty(AppSettingsValidator.Validate(settings));
    }

    [Fact]
    public void Browser_session_requires_a_browser()
    {
        var settings = Valid();
        settings.UseBrowserSession = true;
        Assert.Single(AppSettingsValidator.Validate(settings));

        settings.BrowserForCookies = "edge";
        Assert.Empty(AppSettingsValidator.Validate(settings));
    }

    [Fact]
    public void Ffmpeg_override_must_be_a_full_path()
    {
        var settings = Valid();
        settings.FfmpegPathOverride = "ffmpeg.exe";

        Assert.Single(AppSettingsValidator.Validate(settings));
    }

    [Fact]
    public void All_problems_are_reported_together()
    {
        var settings = Valid();
        settings.DownloadDirectory = "";
        settings.MaxConcurrentDownloads = 0;
        settings.DefaultAudioBitrateKbps = 1;

        Assert.Equal(3, AppSettingsValidator.Validate(settings).Count);
    }

    [Fact]
    public void Clone_is_independent_of_the_original()
    {
        var original = Valid();
        var copy = original.Clone();

        copy.MaxConcurrentDownloads = 7;

        Assert.Equal(3, original.MaxConcurrentDownloads);
    }
}
