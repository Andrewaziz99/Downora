using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Downora.Presentation.Services;
using Downora.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Downora.Presentation.Tests;

[Collection(CultureSensitive.Name)]
public class ViewModelTests
{
    private readonly Localizer _l = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeTheme _theme = new();
    private readonly IDownloadJobQueries _jobs = Substitute.For<IDownloadJobQueries>();
    private readonly IMediaLibraryService _library = Substitute.For<IMediaLibraryService>();
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IDiskSpaceService _disk = Substitute.For<IDiskSpaceService>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 8, 19, 0, 0, TimeSpan.Zero));
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IMediaExtractor _extractor = Substitute.For<IMediaExtractor>();
    private readonly IToolManager _tools = Substitute.For<IToolManager>();

    public ViewModelTests()
    {
        _jobs.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        _jobs.GetHistoryAsync(Arg.Any<HistoryQuery>(), Arg.Any<CancellationToken>()).Returns([]);
        _jobs.CountByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DownloadStatus, int>());
        _jobs.GetStatisticsAsync(Arg.Any<CancellationToken>()).Returns(new DownloadStatistics(0, 0, 0, 0, null));
        _library.QueryAsync(Arg.Any<LibraryQuery>(), Arg.Any<CancellationToken>()).Returns([]);
        _tools.GetStatusAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddDownoraPresentation();
        services.AddSingleton(_l);
        services.AddSingleton<ISettingsService>(_settings);
        services.AddSingleton<IThemeService>(_theme);
        services.AddSingleton(_jobs);
        services.AddSingleton(_library);
        services.AddSingleton(_shell);
        services.AddSingleton(_dialogs);
        services.AddSingleton(_disk);
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Fakes.AppInfo());
        services.AddSingleton(_clipboard);
        services.AddSingleton(_extractor);
        services.AddSingleton(_tools);
        services.AddSingleton<IUrlValidator>(new Core.Services.UrlValidator(new Core.Services.SourceDetector()));
        return services.BuildServiceProvider();
    }

    // ---- Navigation and shell ----

    [Fact]
    public async Task Every_sidebar_entry_opens_its_page_and_only_that_entry_is_selected()
    {
        await using var services = BuildServices();
        var shell = services.GetRequiredService<ShellViewModel>();
        var all = shell.Items.Concat(shell.FooterItems).ToList();
        Assert.Equal(7, all.Count);

        foreach (var item in all)
        {
            await shell.NavigateCommand.ExecuteAsync(item);

            Assert.IsType(item.PageType, shell.CurrentPage);
            Assert.True(item.IsSelected);
            Assert.Single(all, i => i.IsSelected);
        }
    }

    [Fact]
    public async Task Start_opens_home()
    {
        await using var services = BuildServices();
        var shell = services.GetRequiredService<ShellViewModel>();

        await shell.StartAsync();

        Assert.IsType<HomeViewModel>(shell.CurrentPage);
    }

    [Fact]
    public async Task Navigating_to_a_non_page_type_is_rejected()
    {
        await using var services = BuildServices();
        var navigation = services.GetRequiredService<INavigationService>();

        await Assert.ThrowsAsync<ArgumentException>(() => navigation.NavigateToAsync(typeof(string)));
    }

    [Fact]
    public async Task Theme_toggle_flips_dark_and_light_and_saves_it()
    {
        await using var services = BuildServices();
        var shell = services.GetRequiredService<ShellViewModel>();

        await shell.ToggleThemeCommand.ExecuteAsync(null);
        Assert.False(shell.IsDarkTheme);
        Assert.Equal(AppTheme.Light, _settings.Current.Theme);

        await shell.ToggleThemeCommand.ExecuteAsync(null);
        Assert.True(shell.IsDarkTheme);
        Assert.Equal(AppTheme.Dark, _settings.Current.Theme);
    }

    [Fact]
    public async Task Sidebar_shows_free_space_and_queue_badge()
    {
        _disk.GetUsage(Arg.Any<string>()).Returns(new DiskUsage("D:", 512L << 30, 128L << 30));
        _jobs.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([Fakes.Job("a", DownloadStatus.Waiting), Fakes.Job("b", DownloadStatus.Paused)]);
        await using var services = BuildServices();
        var shell = services.GetRequiredService<ShellViewModel>();

        await shell.StartAsync();

        Assert.True(shell.HasStorage);
        Assert.Equal("Storage (D:)", shell.StorageTitle);
        Assert.Equal("128 GB free of 512 GB", shell.StorageDetail);
        Assert.Equal(75, shell.StorageUsedPercent);
        Assert.Equal(2, shell.Items.Single(i => i.PageType == typeof(QueueViewModel)).Badge);
    }

    [Fact]
    public async Task Sidebar_reports_unreadable_disk_instead_of_failing()
    {
        await using var services = BuildServices();
        var shell = services.GetRequiredService<ShellViewModel>();

        await shell.StartAsync();

        Assert.False(shell.HasStorage);
        Assert.Equal("Disk space unavailable", shell.StorageTitle);
    }

    // ---- Home ----

    [Theory]
    [InlineData(6, "Home_GreetingMorning")]
    [InlineData(11, "Home_GreetingMorning")]
    [InlineData(12, "Home_GreetingAfternoon")]
    [InlineData(17, "Home_GreetingAfternoon")]
    [InlineData(18, "Home_GreetingEvening")]
    [InlineData(23, "Home_GreetingEvening")]
    public void Greeting_follows_the_time_of_day(int hour, string key) =>
        Assert.Equal(key, HomeViewModel.GreetingKey(hour));

    [Fact]
    public async Task Home_shows_real_statistics_and_previews()
    {
        _jobs.GetStatisticsAsync(Arg.Any<CancellationToken>()).Returns(new DownloadStatistics(1284, 18, 443_000_000_000, 3, 0.994));
        _jobs.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(
            [.. Enumerable.Range(1, 5).Select(i => Fakes.Job($"job{i}", i <= 3 ? DownloadStatus.Downloading : DownloadStatus.Waiting))]);
        _jobs.GetHistoryAsync(Arg.Is<HistoryQuery>(q => q.Status == DownloadStatus.Completed), Arg.Any<CancellationToken>())
            .Returns([Fakes.Job("done", DownloadStatus.Completed)]);
        await using var services = BuildServices();
        var home = services.GetRequiredService<HomeViewModel>();

        await home.OnNavigatedToAsync(CancellationToken.None);

        Assert.Null(home.ErrorMessage);
        Assert.Equal("1,284", home.TotalDownloads);
        Assert.Equal("+18 this week", home.TotalDownloadsHint);
        Assert.Equal("413 GB", home.DataDownloaded);
        Assert.Equal("3", home.ActiveNow);
        Assert.Equal("2 in queue", home.ActiveNowHint);
        Assert.Equal("99.4%", home.SuccessRate.Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Equal(HomeViewModel.ActivePreviewCount, home.ActiveDownloads.Count);
        Assert.Equal("done", Assert.Single(home.RecentDownloads).Title);
    }

    [Fact]
    public async Task Home_without_finished_downloads_shows_a_dash_for_success_rate()
    {
        await using var services = BuildServices();
        var home = services.GetRequiredService<HomeViewModel>();

        await home.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal("—", home.SuccessRate);
    }

    [Fact]
    public async Task A_failing_query_shows_a_message_instead_of_crashing()
    {
        _jobs.GetStatisticsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database is locked"));
        await using var services = BuildServices();
        var home = services.GetRequiredService<HomeViewModel>();

        await home.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal("Something went wrong: database is locked", home.ErrorMessage);
        Assert.False(home.IsLoading);
    }

    [Fact]
    public async Task Home_opens_the_download_folder()
    {
        await using var services = BuildServices();
        var home = services.GetRequiredService<HomeViewModel>();

        home.OpenDownloadFolderCommand.Execute(null);

        _shell.Received(1).OpenFolder(_settings.Current.DownloadDirectory);
    }

    // ---- Queue ----

    [Fact]
    public async Task Queue_counts_and_filters_by_tab()
    {
        _jobs.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Fakes.Job("d", DownloadStatus.Downloading),
            Fakes.Job("x", DownloadStatus.Extracting),
            Fakes.Job("w1", DownloadStatus.Waiting),
            Fakes.Job("w2", DownloadStatus.Waiting),
        ]);
        await using var services = BuildServices();
        var queue = services.GetRequiredService<QueueViewModel>();

        await queue.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal([4, 2, 2, 0], queue.Filters.Select(f => f.Count));
        Assert.Equal(4, queue.Items.Count);

        queue.SelectedFilter = queue.Filters.Single(f => f.Value == QueueFilter.Waiting);
        Assert.Equal(["w1", "w2"], queue.Items.Select(i => i.Title));

        queue.SelectedFilter = queue.Filters.Single(f => f.Value == QueueFilter.Paused);
        Assert.Empty(queue.Items);
        Assert.True(queue.IsFilterEmpty);
        Assert.False(queue.IsQueueEmpty);
    }

    [Fact]
    public async Task Empty_queue_shows_the_empty_state()
    {
        await using var services = BuildServices();
        var queue = services.GetRequiredService<QueueViewModel>();

        await queue.OnNavigatedToAsync(CancellationToken.None);

        Assert.True(queue.IsQueueEmpty);
        Assert.False(queue.IsFilterEmpty);
    }

    [Fact]
    public void Download_item_formats_size_quality_and_status()
    {
        var job = Fakes.Job("Song", DownloadStatus.Downloading, downloaded: 840L << 20, total: 2400L << 20);
        job.MediaKind = MediaKind.Audio;
        job.AudioFormat = AudioFormat.Flac;
        job.AudioBitrateKbps = 320;
        job.ProgressPercent = 150;

        var item = new DownloadItemViewModel(job, _l);

        Assert.Equal("FLAC", item.FileType);
        Assert.Equal("320 kbps", item.Quality);
        Assert.Equal("840 MB of 2.34 GB", item.SizeText);
        Assert.Equal("Downloading", item.StatusLabel);
        Assert.Equal(100, item.Progress);
        Assert.False(item.IsIndeterminate);
    }

    [Fact]
    public void Download_item_without_title_falls_back_to_the_url_and_shows_failures()
    {
        var job = Fakes.Job("x", DownloadStatus.Failed);
        job.Title = null;
        job.ErrorMessage = "This video requires sign-in.";

        var item = new DownloadItemViewModel(job, _l);

        Assert.Equal("https://example.com/x", item.Title);
        Assert.Equal("This video requires sign-in.", item.ErrorMessage);
        Assert.Equal(string.Empty, item.SizeText);
    }

    // ---- Library ----

    [Fact]
    public async Task Library_search_waits_for_typing_to_pause_then_queries_once()
    {
        await using var services = BuildServices();
        var library = services.GetRequiredService<LibraryViewModel>();
        await library.OnNavigatedToAsync(CancellationToken.None);
        _library.ClearReceivedCalls();

        library.SearchText = "n";
        library.SearchText = "ni";
        library.SearchText = "night";
        await _library.DidNotReceive().QueryAsync(Arg.Any<LibraryQuery>(), Arg.Any<CancellationToken>());

        _time.Advance(SearchablePageViewModel.SearchDelay);
        await Task.Yield();

        await _library.Received(1).QueryAsync(Arg.Is<LibraryQuery>(q => q.Search == "night"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Library_filters_and_sort_go_into_the_query()
    {
        await using var services = BuildServices();
        var library = services.GetRequiredService<LibraryViewModel>();

        library.SelectedKind = library.KindFilters.Single(k => k.Value == MediaKind.Audio);
        library.SelectedSort = library.SortOptions.Single(s => s.Value == LibrarySort.Title);
        await Task.Yield();

        await _library.Received().QueryAsync(
            Arg.Is<LibraryQuery>(q => q.Kind == MediaKind.Audio && q.Sort == LibrarySort.Title && !q.Descending),
            Arg.Any<CancellationToken>());
        Assert.True(library.HasNoResults);
        Assert.False(library.IsLibraryEmpty);
    }

    [Fact]
    public async Task Library_opens_files_but_not_missing_ones()
    {
        var present = new MediaItemViewModel(new MediaItem { FilePath = "/m/a.mp4" }, _l);
        var missing = new MediaItemViewModel(new MediaItem { FilePath = "/m/b.mp4", IsMissing = true }, _l);
        await using var services = BuildServices();
        var library = services.GetRequiredService<LibraryViewModel>();

        library.OpenCommand.Execute(present);
        library.OpenCommand.Execute(missing);
        library.ShowInFolderCommand.Execute(present);

        _shell.Received(1).OpenFile("/m/a.mp4");
        _shell.DidNotReceive().OpenFile("/m/b.mp4");
        _shell.Received(1).ShowFileInFolder("/m/a.mp4");
    }

    [Fact]
    public void Media_item_uses_file_name_when_untitled_and_shows_resolution()
    {
        var item = new MediaItemViewModel(
            new MediaItem { FilePath = "/m/Night Drive.mkv", Height = 1080, MediaKind = MediaKind.Video, FileSizeBytes = 88_400_000, Duration = TimeSpan.FromSeconds(245) },
            _l);

        Assert.Equal("Night Drive", item.Title);
        Assert.Equal("MKV", item.FileType);
        Assert.Equal("1080p", item.Resolution);
        Assert.Equal("4:05", item.DurationText);
        Assert.Equal("84.3 MB", item.SizeText);
    }

    // ---- History ----

    [Fact]
    public async Task Clearing_history_asks_first_and_does_nothing_when_cancelled()
    {
        _jobs.CountByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DownloadStatus, int> { [DownloadStatus.Completed] = 5, [DownloadStatus.Waiting] = 2 });
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        await using var services = BuildServices();
        var history = services.GetRequiredService<HistoryViewModel>();
        await history.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal(5, history.TotalCount);
        Assert.True(history.ClearHistoryCommand.CanExecute(null));

        await history.ClearHistoryCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(
            "Clear download history?",
            Arg.Is<string>(m => m.Contains("5 finished downloads", StringComparison.Ordinal)),
            "Clear history",
            "Cancel");
        await _jobs.DidNotReceive().ClearHistoryAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirming_clears_history_and_reloads()
    {
        _jobs.CountByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DownloadStatus, int> { [DownloadStatus.Failed] = 1 });
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        await using var services = BuildServices();
        var history = services.GetRequiredService<HistoryViewModel>();
        await history.OnNavigatedToAsync(CancellationToken.None);

        _jobs.CountByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DownloadStatus, int>());
        await history.ClearHistoryCommand.ExecuteAsync(null);

        await _jobs.Received(1).ClearHistoryAsync(Arg.Any<CancellationToken>());
        Assert.True(history.IsHistoryEmpty);
        Assert.False(history.ClearHistoryCommand.CanExecute(null));
    }

    // ---- Settings ----

    [Fact]
    public async Task Settings_page_shows_stored_values()
    {
        var stored = _settings.Current;
        stored.Theme = AppTheme.Light;
        await _settings.SaveAsync(stored, TestContext.Current.CancellationToken);
        await using var services = BuildServices();
        var page = services.GetRequiredService<SettingsViewModel>();

        await page.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal(AppTheme.Light, page.SelectedTheme.Value);
        Assert.Equal(AppLanguage.English, page.SelectedLanguage.Value);
        Assert.Equal(_settings.Current.DownloadDirectory, page.DownloadDirectory);
        Assert.Equal(1, _settings.SaveCount);
        Assert.Equal(AppTheme.Dark, _theme.Theme);
    }

    [Fact]
    public async Task Opening_settings_never_overwrites_stored_values()
    {
        var stored = _settings.Current;
        stored.Theme = AppTheme.Light;
        stored.Language = AppLanguage.English;
        await _settings.SaveAsync(stored, TestContext.Current.CancellationToken);
        await using var services = BuildServices();

        var page = services.GetRequiredService<SettingsViewModel>();
        await page.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal(1, _settings.SaveCount);
        Assert.Equal(AppTheme.Light, _settings.Current.Theme);
    }

    [Fact]
    public async Task Library_does_not_query_until_it_is_shown()
    {
        await using var services = BuildServices();

        _ = services.GetRequiredService<LibraryViewModel>();

        await _library.DidNotReceive().QueryAsync(Arg.Any<LibraryQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Changing_theme_applies_and_saves_immediately()
    {
        await using var services = BuildServices();
        var page = services.GetRequiredService<SettingsViewModel>();
        await page.OnNavigatedToAsync(CancellationToken.None);

        page.SelectedTheme = page.ThemeOptions.Single(o => o.Value == AppTheme.Light);

        Assert.Equal(AppTheme.Light, _theme.Theme);
        Assert.Equal(AppTheme.Light, _settings.Current.Theme);
    }

    [Fact]
    public async Task Changing_language_switches_the_ui_and_saves()
    {
        await using var services = BuildServices();
        var page = services.GetRequiredService<SettingsViewModel>();
        await page.OnNavigatedToAsync(CancellationToken.None);
        try
        {
            page.SelectedLanguage = page.LanguageOptions.Single(o => o.Value == AppLanguage.Arabic);

            Assert.True(_l.IsRightToLeft);
            Assert.Equal(AppLanguage.Arabic, _settings.Current.Language);
            Assert.Equal("داكن", page.ThemeOptions.Single(o => o.Value == AppTheme.Dark).Label);
        }
        finally
        {
            _l.SetLanguage(AppLanguage.English);
        }
    }

    [Fact]
    public async Task Browsing_to_a_new_folder_saves_it_and_cancel_keeps_the_old_one()
    {
        var picked = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Picked"));
        await using var services = BuildServices();
        var page = services.GetRequiredService<SettingsViewModel>();
        await page.OnNavigatedToAsync(CancellationToken.None);
        var original = page.DownloadDirectory;

        _dialogs.PickFolder(default!, default).ReturnsForAnyArgs((string?)null);
        await page.BrowseFolderCommand.ExecuteAsync(null);
        Assert.Equal(original, page.DownloadDirectory);

        _dialogs.PickFolder(default!, default).ReturnsForAnyArgs(picked);
        await page.BrowseFolderCommand.ExecuteAsync(null);
        Assert.Equal(picked, page.DownloadDirectory);
        Assert.Equal(picked, _settings.Current.DownloadDirectory);
    }

    [Fact]
    public async Task An_invalid_folder_is_refused_with_a_message()
    {
        await using var services = BuildServices();
        var page = services.GetRequiredService<SettingsViewModel>();
        await page.OnNavigatedToAsync(CancellationToken.None);
        var original = page.DownloadDirectory;

        _dialogs.PickFolder(default!, default).ReturnsForAnyArgs("relative-folder");
        await page.BrowseFolderCommand.ExecuteAsync(null);

        Assert.Equal(original, page.DownloadDirectory);
        Assert.StartsWith("Couldn't save the setting:", page.ErrorMessage, StringComparison.Ordinal);
    }

    // ---- About ----

    [Fact]
    public async Task About_shows_version_and_opens_links()
    {
        await using var services = BuildServices();
        var about = services.GetRequiredService<AboutViewModel>();

        Assert.Equal("Version 1.2.3", about.VersionText);
        about.OpenSourceCommand.Execute(null);
        about.OpenLinkCommand.Execute(about.ThirdParty[0]);
        about.OpenLogsCommand.Execute(null);

        _shell.Received(1).OpenUrl(new Uri("https://github.com/Andrewaziz99/Downora"));
        _shell.Received(1).OpenUrl(about.ThirdParty[0].Url);
        _shell.Received(1).OpenFolder("/data/Downora/logs");
    }

    [Fact]
    public async Task Downloader_points_to_the_download_folder_and_settings()
    {
        await using var services = BuildServices();
        var downloader = services.GetRequiredService<DownloaderViewModel>();

        await downloader.OnNavigatedToAsync(CancellationToken.None);
        await downloader.ChangeFolderCommand.ExecuteAsync(null);

        Assert.Contains(_settings.Current.DownloadDirectory, downloader.IdleBody, StringComparison.Ordinal);
        Assert.True(downloader.IsIdle);
        Assert.IsType<SettingsViewModel>(services.GetRequiredService<INavigationService>().Current);
    }
}
