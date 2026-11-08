using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Downora.Presentation.Services;
using Downora.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Downora.Presentation.Tests;

[Collection(CultureSensitive.Name)]
public class DownloaderTests
{
    private readonly Localizer _l = new();
    private readonly FakeSettings _settings = new();
    private readonly IMediaExtractor _extractor = Substitute.For<IMediaExtractor>();
    private readonly IToolManager _tools = Substitute.For<IToolManager>();
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    private static readonly MediaInfo Zoo = new(
        "jNQXAC9IVRw", "Me at the zoo", "jawed", TimeSpan.FromSeconds(19), "https://i.ytimg.com/vi/jNQXAC9IVRw/hq.jpg",
        "https://www.youtube.com/watch?v=jNQXAC9IVRw", SourcePlatform.YouTube, "Youtube", false,
        [
            new MediaFormat("133", "mp4", 320, 240, 30, "avc1.4D400D", "none", 182, null, 433_081, false, "https"),
            new MediaFormat("140", "m4a", null, null, null, "none", "mp4a.40.2", 129.8, 129.8, 309_288, false, "https"),
        ]);

    private DownloaderViewModel Create() => new(
        new UrlValidator(new SourceDetector()), _extractor, _tools, _settings, _shell, _clipboard, _navigation, _l,
        NullLogger<DownloaderViewModel>.Instance);

    private void Returns(AnalysisResult result) =>
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task Bad_links_are_explained_and_never_analyzed()
    {
        var vm = Create();

        vm.UrlText = "not a link";
        await vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.Equal("That doesn't look like a web link.", vm.ValidationMessage);
        Assert.True(vm.IsIdle);
        await _extractor.DidNotReceive().AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Analyze_is_disabled_until_something_is_typed()
    {
        var vm = Create();
        Assert.False(vm.AnalyzeCommand.CanExecute(null));

        vm.UrlText = "youtu.be/jNQXAC9IVRw";
        Assert.True(vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_video_shows_its_details_and_real_qualities()
    {
        Returns(AnalysisResult.ForMedia(Zoo));
        var vm = Create();

        await vm.AnalyzeAsync("youtu.be/jNQXAC9IVRw");

        Assert.True(vm.IsMedia);
        Assert.Equal("https://youtu.be/jNQXAC9IVRw", vm.UrlText);
        Assert.Equal("Me at the zoo", vm.Title);
        Assert.Equal("jawed", vm.Uploader);
        Assert.Equal("0:19", vm.DurationText);
        Assert.Equal("YouTube", vm.PlatformLabel);
        Assert.Equal(8, vm.Qualities.Count);

        var p360 = vm.Qualities.Single(q => q.Preset == QualityPreset.P360);
        Assert.True(p360.IsAvailable);
        Assert.Equal("240p · 30 fps · H.264", p360.Detail);
        Assert.Equal("725 KB", p360.SizeText);

        var p1080 = vm.Qualities.Single(q => q.Preset == QualityPreset.P1080);
        Assert.False(p1080.IsAvailable);
        Assert.Equal("Not offered", p1080.Detail);
        Assert.Equal("Best audio: AAC · 130 kbps", vm.BestAudioText);

        vm.OpenInBrowserCommand.Execute(null);
        _shell.Received(1).OpenUrl(new Uri("https://www.youtube.com/watch?v=jNQXAC9IVRw"));
    }

    [Fact]
    public async Task A_playlist_lists_its_items_with_count_and_total_time()
    {
        Returns(AnalysisResult.ForPlaylist(new PlaylistInfo(
            "PL1", "Rick Astley: Official Music Videos", "Rick Astley", "https://www.youtube.com/playlist?list=PL1", SourcePlatform.YouTube, 43,
            [
                new PlaylistEntry("a", "https://www.youtube.com/watch?v=a", "Never Gonna Give You Up", TimeSpan.FromSeconds(213), "https://i.ytimg.com/a.jpg", "Rick Astley", true),
                new PlaylistEntry("b", "https://www.youtube.com/watch?v=b", "Together Forever", TimeSpan.FromSeconds(200), null, "Rick Astley", true),
                new PlaylistEntry("c", "https://www.youtube.com/watch?v=c", "[Private video]", null, null, null, false),
            ])));
        var vm = Create();

        await vm.AnalyzeAsync("https://www.youtube.com/playlist?list=PL1");

        Assert.True(vm.IsPlaylist);
        Assert.Equal("43 items", vm.PlaylistCountText);
        Assert.Equal("Total 6:53", vm.PlaylistDurationText);
        Assert.Equal(3, vm.Entries.Count);
        Assert.Equal("01", vm.Entries[0].Index);
        Assert.Equal("https://i.ytimg.com/a.jpg", vm.ThumbnailUrl);
        Assert.False(vm.Entries[2].IsAvailable);
        Assert.Equal("Unavailable", vm.Entries[2].StatusText);
        Assert.Equal("—", vm.Entries[2].DurationText);
    }

    [Theory]
    [InlineData(DownloadErrorKind.RequiresSignIn, "Sign-in required")]
    [InlineData(DownloadErrorKind.Unavailable, "Content unavailable")]
    [InlineData(DownloadErrorKind.Network, "Couldn't reach the site")]
    [InlineData(DownloadErrorKind.Unsupported, "This link isn't supported")]
    [InlineData(DownloadErrorKind.Drm, "Protected content")]
    [InlineData(DownloadErrorKind.GeoRestricted, "Not available in your region")]
    [InlineData(DownloadErrorKind.Unknown, "Something went wrong")]
    public async Task Failures_get_a_plain_explanation_plus_the_tool_detail(DownloadErrorKind kind, string title)
    {
        Returns(AnalysisResult.Failed(kind, "Sign in to confirm you're not a bot"));
        var vm = Create();

        await vm.AnalyzeAsync("https://www.youtube.com/watch?v=jNQXAC9IVRw");

        Assert.True(vm.IsError);
        Assert.Equal(title, vm.ErrorTitle);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorBody));
        Assert.DoesNotContain("[Error_", vm.ErrorBody, StringComparison.Ordinal);
        Assert.Equal("Details from yt-dlp: Sign in to confirm you're not a bot", vm.ErrorDetail);
        Assert.False(vm.CanInstallTools);
    }

    [Fact]
    public async Task Every_error_kind_has_a_translated_title_and_body()
    {
        var vm = Create();
        foreach (var kind in Enum.GetValues<DownloadErrorKind>().Where(k => k != DownloadErrorKind.None))
        {
            Returns(AnalysisResult.Failed(kind));
            await vm.AnalyzeAsync("https://example.com/v");

            Assert.DoesNotContain("[", vm.ErrorTitle, StringComparison.Ordinal);
            Assert.DoesNotContain("[", vm.ErrorBody, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Missing_tools_can_be_installed_and_the_link_is_analyzed_again()
    {
        Returns(AnalysisResult.Failed(DownloadErrorKind.ToolMissing, "yt-dlp is not installed."));
        _tools.GetPath(ToolKind.YtDlp).Returns((string?)null);
        _tools.GetPath(ToolKind.Deno).Returns("C:/tools/deno.exe");
        var vm = Create();
        await vm.AnalyzeAsync("https://youtu.be/jNQXAC9IVRw");
        Assert.True(vm.CanInstallTools);
        Assert.Null(vm.ErrorDetail);

        Returns(AnalysisResult.ForMedia(Zoo));
        await vm.InstallToolsCommand.ExecuteAsync(null);

        await _tools.Received(1).InstallAsync(ToolKind.YtDlp, Arg.Any<IProgress<ToolInstallProgress>>(), Arg.Any<CancellationToken>());
        await _tools.DidNotReceive().InstallAsync(ToolKind.Deno, Arg.Any<IProgress<ToolInstallProgress>>(), Arg.Any<CancellationToken>());
        Assert.True(vm.IsMedia);
        Assert.False(vm.IsInstalling);
    }

    [Fact]
    public async Task A_failed_install_says_why_and_stays_on_the_error()
    {
        Returns(AnalysisResult.Failed(DownloadErrorKind.ToolMissing));
        _tools.GetPath(Arg.Any<ToolKind>()).Returns((string?)null);
        _tools.InstallAsync(ToolKind.YtDlp, Arg.Any<IProgress<ToolInstallProgress>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ToolInstallException("checksum didn't match"));
        var vm = Create();
        await vm.AnalyzeAsync("https://youtu.be/jNQXAC9IVRw");

        await vm.InstallToolsCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't install: checksum didn't match", vm.InstallStatus);
        Assert.True(vm.IsError);
        Assert.False(vm.IsInstalling);
    }

    [Fact]
    public async Task Cancelling_returns_to_idle()
    {
        var started = new TaskCompletionSource();
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return AnalysisResult.ForMedia(Zoo);
            });
        var vm = Create();

        var analysis = vm.AnalyzeAsync("https://youtu.be/jNQXAC9IVRw");
        await started.Task;
        Assert.True(vm.IsAnalyzing);
        Assert.False(vm.AnalyzeCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);
        await analysis;

        Assert.True(vm.IsIdle);
    }

    [Fact]
    public async Task Unexpected_exceptions_become_an_error_card()
    {
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var vm = Create();

        await vm.AnalyzeAsync("https://youtu.be/jNQXAC9IVRw");

        Assert.True(vm.IsError);
        Assert.Equal("Details from yt-dlp: boom", vm.ErrorDetail);
    }

    [Fact]
    public async Task Retry_analyzes_the_same_link_again()
    {
        Returns(AnalysisResult.Failed(DownloadErrorKind.Network));
        var vm = Create();
        await vm.AnalyzeAsync("https://youtu.be/jNQXAC9IVRw");
        Returns(AnalysisResult.ForMedia(Zoo));

        await vm.RetryCommand.ExecuteAsync(null);

        Assert.True(vm.IsMedia);
        await _extractor.Received(2).AnalyzeAsync(Arg.Is<UrlValidationResult>(u => u.NormalizedUrl == "https://www.youtube.com/watch?v=jNQXAC9IVRw"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Paste_fills_the_link_box()
    {
        _clipboard.GetText().Returns("  https://soundcloud.com/forss/flickermood \n");
        var vm = Create();

        vm.PasteCommand.Execute(null);

        Assert.Equal("https://soundcloud.com/forss/flickermood", vm.UrlText);
    }

    [Fact]
    public async Task Arabic_layout_keeps_the_folder_path_readable()
    {
        var vm = Create();
        try
        {
            _l.SetLanguage(AppLanguage.Arabic);
            await vm.OnNavigatedToAsync(CancellationToken.None);

            Assert.Contains(Localizer.LeftToRight(_settings.Current.DownloadDirectory), vm.SavedToText, StringComparison.Ordinal);
            Assert.StartsWith("يتم حفظ", vm.SavedToText, StringComparison.Ordinal);
        }
        finally
        {
            _l.SetLanguage(AppLanguage.English);
        }
    }
}

[Collection(CultureSensitive.Name)]
public class HomeAndToolsTests
{
    private static ServiceProvider Services(IMediaExtractor extractor, IToolManager tools, IClipboardService? clipboard = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddDownoraPresentation();
        services.AddSingleton(new Localizer());
        services.AddSingleton<ISettingsService>(new FakeSettings());
        services.AddSingleton<IThemeService>(new FakeTheme());
        var jobs = Substitute.For<IDownloadJobQueries>();
        jobs.GetStatisticsAsync(Arg.Any<CancellationToken>()).Returns(new DownloadStatistics(0, 0, 0, 0, null));
        jobs.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        jobs.GetHistoryAsync(Arg.Any<HistoryQuery>(), Arg.Any<CancellationToken>()).Returns([]);
        services.AddSingleton(jobs);
        services.AddSingleton(Substitute.For<IMediaLibraryService>());
        services.AddSingleton(Substitute.For<IShellService>());
        services.AddSingleton(Substitute.For<IDialogService>());
        services.AddSingleton(Substitute.For<IDiskSpaceService>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Fakes.AppInfo());
        services.AddSingleton(clipboard ?? Substitute.For<IClipboardService>());
        services.AddSingleton(extractor);
        services.AddSingleton(tools);
        services.AddSingleton<IUrlValidator>(new UrlValidator(new SourceDetector()));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Home_hands_a_valid_link_to_the_downloader()
    {
        var extractor = Substitute.For<IMediaExtractor>();
        extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>()).Returns(AnalysisResult.Failed(DownloadErrorKind.Network));
        await using var services = Services(extractor, Substitute.For<IToolManager>());
        var home = services.GetRequiredService<HomeViewModel>();

        home.UrlText = "soundcloud.com/forss/flickermood";
        await home.AnalyzeCommand.ExecuteAsync(null);

        var downloader = Assert.IsType<DownloaderViewModel>(services.GetRequiredService<INavigationService>().Current);
        Assert.Equal("https://soundcloud.com/forss/flickermood", downloader.UrlText);
        Assert.True(downloader.IsError);
        Assert.Equal(string.Empty, home.UrlText);
        await extractor.Received(1).AnalyzeAsync(Arg.Is<UrlValidationResult>(u => u.Platform == SourcePlatform.SoundCloud), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Home_keeps_bad_links_and_explains()
    {
        var extractor = Substitute.For<IMediaExtractor>();
        await using var services = Services(extractor, Substitute.For<IToolManager>());
        var home = services.GetRequiredService<HomeViewModel>();

        home.UrlText = "ftp://example.com/a.mp4";
        await home.AnalyzeCommand.ExecuteAsync(null);

        Assert.Equal("Only http and https links are supported.", home.ValidationMessage);
        Assert.Equal("ftp://example.com/a.mp4", home.UrlText);
        Assert.Null(services.GetRequiredService<INavigationService>().Current);
    }

    [Fact]
    public async Task Home_paste_reads_the_clipboard()
    {
        var clipboard = Substitute.For<IClipboardService>();
        clipboard.GetText().Returns("https://youtu.be/jNQXAC9IVRw");
        await using var services = Services(Substitute.For<IMediaExtractor>(), Substitute.For<IToolManager>(), clipboard);
        var home = services.GetRequiredService<HomeViewModel>();

        home.PasteCommand.Execute(null);

        Assert.Equal("https://youtu.be/jNQXAC9IVRw", home.UrlText);
    }

    [Fact]
    public async Task Settings_shows_tool_versions_and_missing_tools()
    {
        var tools = Substitute.For<IToolManager>();
        tools.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ToolStatus(ToolKind.YtDlp, true, "2026.10.26", "C:/t/yt-dlp.exe"),
            new ToolStatus(ToolKind.Ffmpeg, true, "7.1", "C:/t/ffmpeg.exe"),
            new ToolStatus(ToolKind.Ffprobe, false, null, null),
            new ToolStatus(ToolKind.Deno, true, null, "C:/t/deno.exe"),
        ]);
        await using var services = Services(Substitute.For<IMediaExtractor>(), tools);
        var settings = services.GetRequiredService<SettingsViewModel>();

        await settings.OnNavigatedToAsync(CancellationToken.None);

        var ytDlp = settings.ToolRows[0];
        Assert.Equal("Version 2026.10.26", ytDlp.StatusText);
        Assert.True(ytDlp.ShowUpdate);
        Assert.False(ytDlp.ShowInstall);

        // FFmpeg counts as installed only when FFprobe is there too.
        var ffmpeg = settings.ToolRows[1];
        Assert.Equal("Not installed", ffmpeg.StatusText);
        Assert.True(ffmpeg.ShowInstall);

        var deno = settings.ToolRows[2];
        Assert.Equal("Installed", deno.StatusText);
        Assert.False(deno.ShowUpdate);
    }

    [Fact]
    public async Task Installing_from_settings_refreshes_the_row()
    {
        var tools = Substitute.For<IToolManager>();
        tools.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(
            [new ToolStatus(ToolKind.Deno, false, null, null)],
            [new ToolStatus(ToolKind.Deno, true, "2.9.8", "C:/t/deno.exe")]);
        await using var services = Services(Substitute.For<IMediaExtractor>(), tools);
        var settings = services.GetRequiredService<SettingsViewModel>();
        await settings.OnNavigatedToAsync(CancellationToken.None);
        var deno = settings.ToolRows[2];

        await deno.InstallCommand.ExecuteAsync(null);

        await tools.Received(1).InstallAsync(ToolKind.Deno, Arg.Any<IProgress<ToolInstallProgress>>(), Arg.Any<CancellationToken>());
        Assert.Equal("Version 2.9.8", deno.StatusText);
        Assert.False(deno.IsBusy);
    }

    [Fact]
    public async Task A_failed_yt_dlp_update_is_shown_on_its_row()
    {
        var tools = Substitute.For<IToolManager>();
        tools.GetStatusAsync(Arg.Any<CancellationToken>()).Returns([new ToolStatus(ToolKind.YtDlp, true, "2026.10.26", "x")]);
        tools.UpdateYtDlpAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new ToolInstallException("yt-dlp couldn't update itself: offline"));
        await using var services = Services(Substitute.For<IMediaExtractor>(), tools);
        var settings = services.GetRequiredService<SettingsViewModel>();
        await settings.OnNavigatedToAsync(CancellationToken.None);

        await settings.ToolRows[0].UpdateCommand.ExecuteAsync(null);

        Assert.Equal("yt-dlp couldn't update itself: offline", settings.ToolRows[0].ErrorMessage);
        Assert.Equal("Version 2026.10.26", settings.ToolRows[0].StatusText);
    }
}
