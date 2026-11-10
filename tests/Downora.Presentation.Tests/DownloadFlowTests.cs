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

/// <summary>A UI thread the test drives by hand, to prove updates are handed over instead of applied on the calling thread.</summary>
internal sealed class QueuedUiThread : IUiThread
{
    public Queue<Action> Pending { get; } = new();

    public void Post(Action action) => Pending.Enqueue(action);

    public void RunAll()
    {
        while (Pending.Count > 0)
        {
            Pending.Dequeue()();
        }
    }
}

/// <summary>Downloading from the Downloader screen and following it live on every screen that shows downloads.</summary>
[Collection(CultureSensitive.Name)]
public sealed class DownloadFlowTests : IAsyncDisposable
{
    private static readonly MediaInfo Zoo = new(
        "jNQXAC9IVRw", "Me at the zoo", "jawed", TimeSpan.FromSeconds(19), "https://i.ytimg.com/vi/jNQXAC9IVRw/hq.jpg",
        "https://www.youtube.com/watch?v=jNQXAC9IVRw", SourcePlatform.YouTube, "Youtube", false,
        [
            new MediaFormat("133", "mp4", 320, 240, 30, "avc1.4D400D", "none", 182, null, 433_081, false, "https"),
            new MediaFormat("140", "m4a", null, null, null, "none", "mp4a.40.2", 129.8, 129.8, 309_288, false, "https"),
        ]);

    private static readonly MediaInfo FullLadder = Zoo with
    {
        Formats =
        [
            new MediaFormat("137", "mp4", 1920, 1080, 30, "avc1.640028", "none", 4000, null, 40_000_000, false, "https"),
            new MediaFormat("136", "mp4", 1280, 720, 30, "avc1.4d401f", "none", 2000, null, 20_000_000, false, "https"),
            new MediaFormat("133", "mp4", 320, 240, 30, "avc1.4D400D", "none", 182, null, 433_081, false, "https"),
            new MediaFormat("140", "m4a", null, null, null, "none", "mp4a.40.2", 129.8, 129.8, 309_288, false, "https"),
        ],
    };

    private readonly Localizer _l = new();
    private readonly FakeSettings _settings = new();
    private readonly IMediaExtractor _extractor = Substitute.For<IMediaExtractor>();
    private readonly IToolManager _tools = Substitute.For<IToolManager>();
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly IDownloadJobService _downloads = Substitute.For<IDownloadJobService>();
    private readonly IDownloadJobQueries _queries = Substitute.For<IDownloadJobQueries>();
    private readonly QueuedUiThread _ui = new();
    private ServiceProvider? _provider;

    public DownloadFlowTests()
    {
        _tools.GetPath(Arg.Any<ToolKind>()).Returns("C:/tools/tool.exe");
        _tools.GetStatusAsync(Arg.Any<CancellationToken>()).Returns([]);
        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        _queries.GetHistoryAsync(Arg.Any<HistoryQuery>(), Arg.Any<CancellationToken>()).Returns([]);
        _queries.GetStatisticsAsync(Arg.Any<CancellationToken>()).Returns(new DownloadStatistics(0, 0, 0, 0, null));
        _downloads.StartAsync(Arg.Any<NewDownload>(), Arg.Any<CancellationToken>())
            .Returns(call => new DownloadStartResult(JobFor(call.Arg<NewDownload>()), false));
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }

    private T Get<T>() where T : notnull
    {
        _provider ??= Build();
        return _provider.GetRequiredService<T>();
    }

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddDownoraPresentation();
        services.AddSingleton(_l);
        services.AddSingleton<ISettingsService>(_settings);
        services.AddSingleton<IThemeService>(new FakeTheme());
        services.AddSingleton(_queries);
        services.AddSingleton(Substitute.For<IMediaLibraryService>());
        services.AddSingleton(_shell);
        services.AddSingleton(Substitute.For<IDialogService>());
        services.AddSingleton(Substitute.For<IDiskSpaceService>());
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(Fakes.AppInfo());
        services.AddSingleton(Substitute.For<IClipboardService>());
        services.AddSingleton(_extractor);
        services.AddSingleton(_tools);
        services.AddSingleton(_downloads);
        services.AddSingleton<IUiThread>(_ui);
        services.AddSingleton<IUrlValidator>(new UrlValidator(new SourceDetector()));
        return services.BuildServiceProvider();
    }

    private async Task<DownloaderViewModel> AnalyzedAsync(MediaInfo? media = null)
    {
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>()).Returns(AnalysisResult.ForMedia(media ?? Zoo));
        var vm = Get<DownloaderViewModel>();
        await vm.AnalyzeAsync("https://youtu.be/jNQXAC9IVRw");
        return vm;
    }

    private static DownloadJob JobFor(NewDownload n) => new()
    {
        SourceUrl = n.SourceUrl,
        NormalizedUrl = n.NormalizedUrl,
        Title = n.Title,
        Uploader = n.Uploader,
        MediaKind = n.Kind,
        QualityPreset = n.Kind == MediaKind.Audio ? QualityPreset.AudioOnly : n.Quality,
        AudioFormat = n.Kind == MediaKind.Audio ? n.AudioFormat : null,
        AudioBitrateKbps = n.Kind == MediaKind.Audio ? n.AudioBitrateKbps : null,
        Container = n.Kind == MediaKind.Video ? n.Container : null,
        OutputDirectory = @"D:\Media",
        TotalBytes = n.EstimatedBytes,
        Status = DownloadStatus.Waiting,
        CreatedAtUtc = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc),
    };

    private void Send(DownloadJob job, DownloadProgress? progress = null)
    {
        _downloads.JobUpdated += Raise.Event<EventHandler<DownloadJobUpdate>>(_downloads, new DownloadJobUpdate(job.Clone(), progress));
        _ui.RunAll();
    }

    // ---- Starting a download ----

    [Fact]
    public async Task Download_starts_the_selected_quality_as_a_video_job()
    {
        var vm = await AnalyzedAsync();
        vm.SelectedQuality = vm.Qualities.Single(q => q.Preset == QualityPreset.P360);

        await vm.DownloadVideoCommand.ExecuteAsync(null);

        await _downloads.Received(1).StartAsync(
            Arg.Is<NewDownload>(n =>
                n.Kind == MediaKind.Video &&
                n.Quality == QualityPreset.P360 &&
                n.Container == VideoContainer.Mp4 &&
                n.Title == "Me at the zoo" &&
                n.Uploader == "jawed" &&
                n.Platform == SourcePlatform.YouTube &&
                n.SourceUrl == "https://youtu.be/jNQXAC9IVRw" &&
                n.NormalizedUrl == "https://www.youtube.com/watch?v=jNQXAC9IVRw" &&
                n.ThumbnailUrl == "https://i.ytimg.com/vi/jNQXAC9IVRw/hq.jpg" &&
                n.EstimatedBytes == 742_369),
            Arg.Any<CancellationToken>());
        Assert.NotNull(vm.StartedDownload);
        Assert.True(vm.HasStartedDownload);
        Assert.Equal("Added to your queue.", vm.StartedMessage);
        Assert.Equal("Me at the zoo", vm.StartedDownload.Title);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Download_audio_uses_the_chosen_audio_format_and_the_default_bitrate()
    {
        var vm = await AnalyzedAsync();
        vm.SelectedAudioFormat = vm.AudioFormats.Single(f => f.Value == AudioFormat.Opus);

        await vm.DownloadAudioCommand.ExecuteAsync(null);

        await _downloads.Received(1).StartAsync(
            Arg.Is<NewDownload>(n =>
                n.Kind == MediaKind.Audio &&
                n.AudioFormat == AudioFormat.Opus &&
                n.AudioBitrateKbps == 320 &&
                n.EstimatedBytes == 309_288),
            Arg.Any<CancellationToken>());
        Assert.Equal("OPUS", vm.StartedDownload!.FileType);
    }

    [Fact]
    public async Task The_audio_choices_are_mp3_m4a_and_opus_and_follow_the_saved_preference()
    {
        var settings = _settings.Current;
        settings.PreferredAudioFormat = AudioFormat.M4a;
        await _settings.SaveAsync(settings, TestContext.Current.CancellationToken);

        var vm = await AnalyzedAsync();

        Assert.Equal([AudioFormat.Mp3, AudioFormat.M4a, AudioFormat.Opus, AudioFormat.Flac, AudioFormat.Wav], vm.AudioFormats.Select(f => f.Value));
        Assert.Equal(AudioFormat.M4a, vm.SelectedAudioFormat.Value);
    }

    [Fact]
    public async Task The_container_comes_from_settings()
    {
        var settings = _settings.Current;
        settings.PreferredVideoContainer = VideoContainer.Mkv;
        await _settings.SaveAsync(settings, TestContext.Current.CancellationToken);
        var vm = await AnalyzedAsync();

        await vm.DownloadVideoCommand.ExecuteAsync(null);

        await _downloads.Received(1).StartAsync(Arg.Is<NewDownload>(n => n.Container == VideoContainer.Mkv), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rows_are_selectable_only_when_the_site_offers_them_and_the_default_is_the_nearest_available()
    {
        var settings = _settings.Current;
        settings.DefaultVideoQuality = QualityPreset.P1080;
        await _settings.SaveAsync(settings, TestContext.Current.CancellationToken);

        var zoo = await AnalyzedAsync();
        // The zoo video tops out at 240p, so the 1080p default falls back to the lower rows.
        Assert.Equal(QualityPreset.P360, zoo.SelectedQuality?.Preset);
        Assert.True(zoo.DownloadVideoCommand.CanExecute(null));

        zoo.SelectedQuality = zoo.Qualities.Single(q => q.Preset == QualityPreset.P1080);
        Assert.False(zoo.DownloadVideoCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_saved_default_is_preselected_when_it_is_available()
    {
        var settings = _settings.Current;
        settings.DefaultVideoQuality = QualityPreset.P720;
        await _settings.SaveAsync(settings, TestContext.Current.CancellationToken);

        var vm = await AnalyzedAsync(FullLadder);

        Assert.Equal(QualityPreset.P720, vm.SelectedQuality?.Preset);
    }

    [Fact]
    public async Task Best_is_preselected_by_default()
    {
        var vm = await AnalyzedAsync(FullLadder);

        Assert.Equal(QualityPreset.Best, vm.SelectedQuality?.Preset);
        Assert.Equal(7, vm.Qualities.Count);
        Assert.DoesNotContain(vm.Qualities, q => q.Preset == QualityPreset.AudioOnly);
    }

    [Fact]
    public async Task Nothing_can_be_downloaded_before_a_link_is_analyzed_or_from_a_live_stream()
    {
        var vm = Get<DownloaderViewModel>();
        Assert.False(vm.DownloadVideoCommand.CanExecute(null));
        Assert.False(vm.DownloadAudioCommand.CanExecute(null));

        var live = await AnalyzedAsync(Zoo with { IsLive = true });
        Assert.False(live.DownloadVideoCommand.CanExecute(null));
        Assert.False(live.DownloadAudioCommand.CanExecute(null));
    }

    [Fact]
    public async Task Without_an_audio_stream_the_audio_button_is_off()
    {
        var videoOnly = Zoo with { Formats = [Zoo.Formats[0]] };

        var vm = await AnalyzedAsync(videoOnly);

        Assert.False(vm.AudioOption?.IsAvailable ?? false);
        Assert.False(vm.DownloadAudioCommand.CanExecute(null));
        Assert.True(vm.DownloadVideoCommand.CanExecute(null));
    }

    [Fact]
    public async Task Downloading_the_same_thing_twice_says_it_is_already_in_the_queue()
    {
        var vm = await AnalyzedAsync();
        _downloads.StartAsync(Arg.Any<NewDownload>(), Arg.Any<CancellationToken>())
            .Returns(call => new DownloadStartResult(JobFor(call.Arg<NewDownload>()), true));

        await vm.DownloadVideoCommand.ExecuteAsync(null);

        Assert.Equal("This is already in your queue at that quality.", vm.StartedMessage);
    }

    [Fact]
    public async Task Missing_tools_are_offered_for_install_instead_of_starting_a_doomed_job()
    {
        var vm = await AnalyzedAsync();
        _tools.GetPath(ToolKind.Ffmpeg).Returns((string?)null);

        await vm.DownloadVideoCommand.ExecuteAsync(null);

        Assert.True(vm.IsError);
        Assert.True(vm.CanInstallTools);
        Assert.Equal("Required tools aren't installed", vm.ErrorTitle);
        await _downloads.DidNotReceive().StartAsync(Arg.Any<NewDownload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Installing_covers_ffmpeg_too_because_downloads_need_it()
    {
        var vm = await AnalyzedAsync();
        _tools.GetPath(ToolKind.Ffmpeg).Returns((string?)null);
        await vm.DownloadVideoCommand.ExecuteAsync(null);

        await vm.InstallToolsCommand.ExecuteAsync(null);

        await _tools.Received(1).InstallAsync(ToolKind.Ffmpeg, Arg.Any<IProgress<ToolInstallProgress>>(), Arg.Any<CancellationToken>());
        await _tools.DidNotReceive().InstallAsync(ToolKind.YtDlp, Arg.Any<IProgress<ToolInstallProgress>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failure_to_start_is_shown_and_does_not_break_the_screen()
    {
        var vm = await AnalyzedAsync();
        _downloads.StartAsync(Arg.Any<NewDownload>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database is locked"));

        await vm.DownloadVideoCommand.ExecuteAsync(null);

        Assert.Equal("Something went wrong: database is locked", vm.ErrorMessage);
        Assert.True(vm.IsMedia);
        Assert.Null(vm.StartedDownload);
    }

    [Fact]
    public async Task A_new_analysis_clears_the_previous_started_download()
    {
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        Assert.NotNull(vm.StartedDownload);

        await vm.AnalyzeAsync("https://youtu.be/jNQXAC9IVRw");

        Assert.Null(vm.StartedDownload);
        Assert.Null(vm.StartedMessage);
    }

    // ---- Following it live ----

    [Fact]
    public async Task The_started_download_follows_the_job_from_start_to_finish()
    {
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        var item = vm.StartedDownload!;
        var job = JobFor(new NewDownload { SourceUrl = "u", NormalizedUrl = "u" });
        job.Id = item.Id;

        Assert.Equal("Waiting", item.StatusLabel);

        job.Status = DownloadStatus.Extracting;
        Send(job, new DownloadProgress(DownloadStage.Starting, 0, 0, null, null, null));
        Assert.Equal("Extracting", item.StatusLabel);
        Assert.True(item.IsIndeterminate);
        Assert.Equal("Starting…", item.StageText);

        job.Status = DownloadStatus.Downloading;
        job.ProgressPercent = 42;
        job.DownloadedBytes = 3L << 20;
        job.TotalBytes = 7L << 20;
        Send(job, new DownloadProgress(DownloadStage.Downloading, 42, 3L << 20, 7L << 20, 1.5 * 1024 * 1024, TimeSpan.FromSeconds(30)));
        Assert.Equal("Downloading", item.StatusLabel);
        Assert.Equal(42, item.Progress);
        Assert.False(item.IsIndeterminate);
        Assert.Equal("3 MB of 7 MB", item.SizeText);
        Assert.Equal("1.5 MB/s", item.SpeedText);
        Assert.Equal("0:30 left", item.EtaText);
        Assert.Equal(string.Empty, item.StageText);
        Assert.True(item.IsActive);
        Assert.False(item.HasOutputFile);

        job.Status = DownloadStatus.Processing;
        job.ProgressPercent = 99;
        Send(job, new DownloadProgress(DownloadStage.Merging, 99, 7L << 20, 7L << 20, null, null));
        Assert.Equal("Processing", item.StatusLabel);
        Assert.Equal("Joining video and audio…", item.StageText);
        Assert.Equal(string.Empty, item.SpeedText);
        Assert.Equal(string.Empty, item.EtaText);

        job.Status = DownloadStatus.Completed;
        job.ProgressPercent = 100;
        job.OutputPath = @"D:\Media\Me at the zoo.mp4";
        job.CompletedAtUtc = job.CreatedAtUtc.AddMinutes(1);
        Send(job);
        Assert.Equal("Completed", item.StatusLabel);
        Assert.Equal(100, item.Progress);
        Assert.False(item.IsActive);
        Assert.True(item.HasOutputFile);
        Assert.True(item.ShowFileCommand.CanExecute(null));
        Assert.False(item.CancelCommand.CanExecute(null));
        Assert.Null(item.ErrorMessage);
    }

    [Fact]
    public async Task Updates_for_other_downloads_do_not_touch_the_started_one()
    {
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        var other = JobFor(new NewDownload { SourceUrl = "other", NormalizedUrl = "other" });
        other.Status = DownloadStatus.Downloading;
        other.ProgressPercent = 80;

        Send(other);

        Assert.Equal("Waiting", vm.StartedDownload!.StatusLabel);
        Assert.Equal(0, vm.StartedDownload.Progress);
    }

    [Fact]
    public async Task A_failed_download_explains_why_and_offers_the_browser_sign_in_when_that_would_help()
    {
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        var job = JobFor(new NewDownload { SourceUrl = "u", NormalizedUrl = "u" }).WithId(vm.StartedDownload!.Id);

        job.Status = DownloadStatus.Failed;
        job.ErrorKind = DownloadErrorKind.RequiresSignIn;
        job.ErrorMessage = "Sign in to confirm you're not a bot";
        Send(job);

        Assert.Equal("Sign-in required", vm.StartedDownload.ErrorMessage);
        Assert.Equal("Failed", vm.StartedDownload.StatusLabel);
        Assert.True(vm.ShowStartedSignInAction);
    }

    [Fact]
    public async Task The_sign_in_action_is_not_offered_when_the_option_is_already_on()
    {
        var settings = _settings.Current;
        settings.UseBrowserSession = true;
        settings.BrowserForCookies = "edge";
        await _settings.SaveAsync(settings, TestContext.Current.CancellationToken);
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        var job = JobFor(new NewDownload { SourceUrl = "u", NormalizedUrl = "u" }).WithId(vm.StartedDownload!.Id);

        job.Status = DownloadStatus.Failed;
        job.ErrorKind = DownloadErrorKind.RequiresSignIn;
        Send(job);

        Assert.False(vm.ShowStartedSignInAction);
    }

    [Fact]
    public async Task Failures_without_a_known_cause_show_the_tools_own_message()
    {
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        var job = JobFor(new NewDownload { SourceUrl = "u", NormalizedUrl = "u" }).WithId(vm.StartedDownload!.Id);

        job.Status = DownloadStatus.Failed;
        job.ErrorKind = DownloadErrorKind.Unknown;
        job.ErrorMessage = "Postprocessing: ffmpeg exited with code 1";
        Send(job);

        Assert.Equal("Postprocessing: ffmpeg exited with code 1", vm.StartedDownload!.ErrorMessage);
    }

    [Fact]
    public async Task Cancel_on_the_card_cancels_that_job_and_shows_it_is_cancelling()
    {
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        var item = vm.StartedDownload!;
        Assert.True(item.CancelCommand.CanExecute(null));

        await item.CancelCommand.ExecuteAsync(null);

        await _downloads.Received(1).CancelAsync(item.Id, Arg.Any<CancellationToken>());
        Assert.Equal("Cancelling…", item.StageText);
        Assert.False(item.CancelCommand.CanExecute(null));

        var job = JobFor(new NewDownload { SourceUrl = "u", NormalizedUrl = "u" }).WithId(item.Id);
        job.Status = DownloadStatus.Cancelled;
        Send(job);
        Assert.Equal("Cancelled", item.StatusLabel);
        Assert.Equal(string.Empty, item.StageText);
    }

    [Fact]
    public async Task Show_file_and_open_folder_use_explorer()
    {
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);
        var item = vm.StartedDownload!;
        var job = JobFor(new NewDownload { SourceUrl = "u", NormalizedUrl = "u" }).WithId(item.Id);
        job.Status = DownloadStatus.Completed;
        job.OutputPath = Path.Combine(Path.GetTempPath(), "downora-missing-file.mp4");
        Send(job);

        item.OpenFolderCommand.Execute(null);
        item.ShowFileCommand.Execute(null);

        // The file isn't there (moved or deleted), so showing it falls back to its folder.
        _shell.DidNotReceive().ShowFileInFolder(Arg.Any<string>());
        _shell.Received(2).OpenFolder(@"D:\Media");
    }

    [Fact]
    public async Task Show_file_selects_the_file_when_it_exists()
    {
        var file = Path.Combine(Path.GetTempPath(), $"downora-{Guid.NewGuid():N}.mp4");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);
        try
        {
            var vm = await AnalyzedAsync();
            await vm.DownloadVideoCommand.ExecuteAsync(null);
            var item = vm.StartedDownload!;
            var job = JobFor(new NewDownload { SourceUrl = "u", NormalizedUrl = "u" }).WithId(item.Id);
            job.Status = DownloadStatus.Completed;
            job.OutputPath = file;
            Send(job);

            item.ShowFileCommand.Execute(null);

            _shell.Received(1).ShowFileInFolder(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Explorer_failing_to_open_does_not_crash_the_card()
    {
        _shell.When(s => s.OpenFolder(Arg.Any<string>())).Do(_ => throw new IOException("explorer is gone"));
        var vm = await AnalyzedAsync();
        await vm.DownloadVideoCommand.ExecuteAsync(null);

        var open = vm.StartedDownload!.OpenFolderCommand;
        open.Execute(null);

        _shell.Received(1).OpenFolder(Arg.Any<string>());
    }

    // ---- Sign-in required ----

    [Fact]
    public async Task The_sign_in_card_offers_the_browser_option_and_opens_settings_without_turning_it_on()
    {
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>())
            .Returns(AnalysisResult.Failed(DownloadErrorKind.RequiresSignIn, "Sign in to confirm you're not a bot"));
        var vm = Get<DownloaderViewModel>();
        await vm.AnalyzeAsync("https://www.youtube.com/watch?v=jNQXAC9IVRw");
        Assert.True(vm.CanUseBrowserSignIn);
        Assert.False(vm.CanOpenSignInSettings);
        Assert.False(vm.CanInstallTools);
        var settings = Get<SettingsViewModel>();
        var revealed = 0;
        settings.CookieOptionsRevealRequested += (_, _) => revealed++;

        await vm.OpenSignInSettingsCommand.ExecuteAsync(null);

        Assert.Same(settings, Get<INavigationService>().Current);
        Assert.Equal(1, revealed);
        Assert.True(settings.ConsumeCookieReveal());
        Assert.False(settings.ConsumeCookieReveal()); // once per request, so later visits don't scroll again
        Assert.False(_settings.Current.UseBrowserSession); // never switched on silently
        Assert.Equal(0, _settings.SaveCount);
    }

    [Fact]
    public async Task The_sign_in_action_is_hidden_once_the_option_is_on()
    {
        var settings = _settings.Current;
        settings.UseBrowserSession = true;
        settings.BrowserForCookies = "chrome";
        await _settings.SaveAsync(settings, TestContext.Current.CancellationToken);
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>())
            .Returns(AnalysisResult.Failed(DownloadErrorKind.RequiresSignIn));
        var vm = Get<DownloaderViewModel>();

        await vm.AnalyzeAsync("https://www.youtube.com/watch?v=jNQXAC9IVRw");

        Assert.True(vm.IsError);
        Assert.False(vm.CanUseBrowserSignIn);
    }

    [Fact]
    public async Task When_the_browsers_sign_in_cannot_be_read_settings_is_the_place_to_fix_it()
    {
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>())
            .Returns(AnalysisResult.Failed(DownloadErrorKind.BrowserCookies, "Could not copy Chrome cookie database"));
        var vm = Get<DownloaderViewModel>();

        await vm.AnalyzeAsync("https://www.youtube.com/watch?v=jNQXAC9IVRw");

        Assert.Equal("Couldn't read your browser sign-in", vm.ErrorTitle);
        Assert.True(vm.CanOpenSignInSettings);
        Assert.False(vm.CanUseBrowserSignIn);
    }

    // ---- Settings ----

    [Fact]
    public async Task Browser_sign_in_is_off_by_default_and_turning_it_on_saves_the_browser()
    {
        var settings = Get<SettingsViewModel>();
        await settings.OnNavigatedToAsync(CancellationToken.None);
        Assert.False(settings.UseBrowserSession);
        Assert.Equal([CookieBrowsers.Edge, CookieBrowsers.Chrome, CookieBrowsers.Firefox, CookieBrowsers.Brave], settings.BrowserOptions.Select(o => o.Value));

        settings.SelectedBrowser = settings.BrowserOptions.Single(o => o.Value == CookieBrowsers.Firefox);
        Assert.False(_settings.Current.UseBrowserSession);
        Assert.Equal("firefox", _settings.Current.BrowserForCookies);

        settings.UseBrowserSession = true;
        Assert.True(_settings.Current.UseBrowserSession);
        Assert.Equal("firefox", _settings.Current.BrowserForCookies);

        settings.UseBrowserSession = false;
        Assert.False(_settings.Current.UseBrowserSession);
        Assert.Equal("firefox", _settings.Current.BrowserForCookies); // the choice is remembered
    }

    [Fact]
    public async Task Settings_shows_the_saved_browser_choice()
    {
        var saved = _settings.Current;
        saved.UseBrowserSession = true;
        saved.BrowserForCookies = "brave";
        await _settings.SaveAsync(saved, TestContext.Current.CancellationToken);
        var settings = Get<SettingsViewModel>();

        await settings.OnNavigatedToAsync(CancellationToken.None);

        Assert.True(settings.UseBrowserSession);
        Assert.Equal("brave", settings.SelectedBrowser.Value);
        Assert.Equal("Brave", settings.SelectedBrowser.Label);
    }

    [Fact]
    public void Every_browser_and_audio_format_label_is_translated()
    {
        var ar = new Localizer();
        ar.SetLanguage(AppLanguage.Arabic);
        try
        {
            foreach (var key in new[] { "Browser_Edge", "Browser_Chrome", "Browser_Firefox", "Browser_Brave", "AudioFormat_Mp3", "AudioFormat_M4a", "AudioFormat_Opus",
                "Settings_CookiesDesc", "Downloader_UseBrowserSignIn", "Stage_Merging", "Queue_Eta", "Error_BrowserCookies_Title" })
            {
                Assert.DoesNotContain("[", ar[key], StringComparison.Ordinal);
            }

            Assert.Equal("استخدام تسجيل الدخول من متصفحي", ar["Downloader_UseBrowserSignIn"]);
        }
        finally
        {
            ar.SetLanguage(AppLanguage.English);
        }
    }

    // ---- Queue ----

    [Fact]
    public async Task The_queue_updates_live_as_downloads_start_progress_and_finish()
    {
        var queue = Get<QueueViewModel>();
        await queue.OnNavigatedToAsync(CancellationToken.None);
        Assert.True(queue.IsQueueEmpty);

        var job = Fakes.Job("song", DownloadStatus.Waiting);
        Send(job);
        Assert.False(queue.IsQueueEmpty);
        var item = Assert.Single(queue.Items);
        Assert.Equal([1, 0, 1, 0], queue.Filters.Select(f => f.Count));

        job.Status = DownloadStatus.Downloading;
        job.ProgressPercent = 25;
        job.DownloadedBytes = 1L << 20;
        job.TotalBytes = 4L << 20;
        Send(job, new DownloadProgress(DownloadStage.Downloading, 25, 1L << 20, 4L << 20, 2L << 20, TimeSpan.FromSeconds(2)));
        Assert.Same(item, Assert.Single(queue.Items)); // the same card updates in place
        Assert.Equal(25, item.Progress);
        Assert.Equal("2 MB/s", item.SpeedText);
        Assert.Equal([1, 1, 0, 0], queue.Filters.Select(f => f.Count));

        job.ProgressPercent = 60;
        Send(job, new DownloadProgress(DownloadStage.Downloading, 60, 2L << 20, 4L << 20, 2L << 20, TimeSpan.FromSeconds(1)));
        Assert.Equal(60, item.Progress);

        job.Status = DownloadStatus.Completed;
        Send(job);
        Assert.Empty(queue.Items);
        Assert.True(queue.IsQueueEmpty);
        Assert.Equal([0, 0, 0, 0], queue.Filters.Select(f => f.Count));
    }

    [Fact]
    public async Task The_queue_keeps_running_work_first_and_respects_the_selected_tab()
    {
        var queue = Get<QueueViewModel>();
        await queue.OnNavigatedToAsync(CancellationToken.None);
        var waiting = Fakes.Job("waiting", DownloadStatus.Waiting);
        var running = Fakes.Job("running", DownloadStatus.Downloading);

        Send(waiting);
        Send(running);
        Assert.Equal(["running", "waiting"], queue.Items.Select(i => i.Title));

        queue.SelectedFilter = queue.Filters.Single(f => f.Value == QueueFilter.Waiting);
        Assert.Equal(["waiting"], queue.Items.Select(i => i.Title));

        waiting.Status = DownloadStatus.Downloading;
        Send(waiting);
        Assert.Empty(queue.Items); // no longer waiting, so it leaves this tab
        Assert.Equal(2, queue.Filters.Single(f => f.Value == QueueFilter.InProgress).Count);
    }

    [Fact]
    public async Task Reopening_the_queue_keeps_live_cards_and_drops_finished_ones()
    {
        var job = Fakes.Job("song", DownloadStatus.Downloading);
        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([job]);
        var queue = Get<QueueViewModel>();
        await queue.OnNavigatedToAsync(CancellationToken.None);
        var card = queue.Items.Single();
        Send(job, new DownloadProgress(DownloadStage.Downloading, 10, 10, 100, 1024, TimeSpan.FromSeconds(9)));
        Assert.Equal("1 KB/s", card.SpeedText);

        await queue.OnNavigatedToAsync(CancellationToken.None);
        Assert.Same(card, queue.Items.Single());

        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        await queue.OnNavigatedToAsync(CancellationToken.None);
        Assert.Empty(queue.Items);
    }

    [Fact]
    public async Task A_job_cancelled_from_the_queue_disappears_from_it()
    {
        var job = Fakes.Job("song", DownloadStatus.Downloading);
        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([job]);
        var queue = Get<QueueViewModel>();
        await queue.OnNavigatedToAsync(CancellationToken.None);

        await queue.Items.Single().CancelCommand.ExecuteAsync(null);
        await _downloads.Received(1).CancelAsync(job.Id, Arg.Any<CancellationToken>());

        job.Status = DownloadStatus.Cancelled;
        Send(job);
        Assert.Empty(queue.Items);
    }

    // ---- Home and the sidebar ----

    [Fact]
    public async Task Home_active_downloads_move_with_the_job_and_the_statistics_refresh_when_it_changes_state()
    {
        var job = Fakes.Job("song", DownloadStatus.Downloading);
        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([job]);
        _queries.GetStatisticsAsync(Arg.Any<CancellationToken>()).Returns(new DownloadStatistics(0, 0, 0, 1, null));
        var home = Get<HomeViewModel>();
        await home.OnNavigatedToAsync(CancellationToken.None);
        var card = home.ActiveDownloads.Single();
        Assert.Equal("1", home.ActiveNow);

        // The first update the feed sees for a job counts as a change of state; after that only progress follows.
        Send(job);
        await Task.Yield();
        _queries.ClearReceivedCalls();

        // Progress only moves the bar: no database round trip.
        job.ProgressPercent = 55;
        Send(job, new DownloadProgress(DownloadStage.Downloading, 55, 55, 100, 1024, null));
        Assert.Equal(55, card.Progress);
        await _queries.DidNotReceive().GetStatisticsAsync(Arg.Any<CancellationToken>());

        // Finishing changes the counts and the recent list, so Home reloads them.
        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        _queries.GetStatisticsAsync(Arg.Any<CancellationToken>()).Returns(new DownloadStatistics(1, 1, 5L << 20, 0, 1.0));
        var done = Fakes.Job("song", DownloadStatus.Completed);
        _queries.GetHistoryAsync(Arg.Any<HistoryQuery>(), Arg.Any<CancellationToken>()).Returns([done]);
        job.Status = DownloadStatus.Completed;
        Send(job);
        await Task.Yield();

        await _queries.Received(1).GetStatisticsAsync(Arg.Any<CancellationToken>());
        Assert.Equal("1", home.TotalDownloads);
        Assert.Equal("0", home.ActiveNow);
        Assert.Equal("100.0%", home.SuccessRate);
        Assert.Empty(home.ActiveDownloads);
        Assert.Single(home.RecentDownloads);
    }

    [Fact]
    public async Task The_sidebar_badge_follows_downloads_starting_and_ending()
    {
        var shell = Get<ShellViewModel>();
        var badge = () => shell.Items.Single(i => i.PageType == typeof(QueueViewModel)).Badge;

        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([Fakes.Job("a", DownloadStatus.Waiting), Fakes.Job("b", DownloadStatus.Downloading)]);
        Send(Fakes.Job("a", DownloadStatus.Waiting));
        await Task.Yield();
        Assert.Equal(2, badge());

        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        var finished = Fakes.Job("a", DownloadStatus.Completed);
        Send(finished);
        await Task.Yield();
        Assert.Equal(0, badge());
    }

    // ---- The feed ----

    [Fact]
    public void Updates_from_background_threads_are_handed_to_the_ui_thread_before_any_screen_sees_them()
    {
        var feed = Get<DownloadFeed>();
        var seen = new List<DownloadFeedEvent>();
        feed.Updated += (_, e) => seen.Add(e);
        var job = Fakes.Job("a", DownloadStatus.Downloading);

        _downloads.JobUpdated += Raise.Event<EventHandler<DownloadJobUpdate>>(_downloads, new DownloadJobUpdate(job.Clone(), null));

        Assert.Empty(seen);
        _ui.RunAll();
        Assert.Single(seen);
    }

    [Fact]
    public void The_feed_marks_changes_of_state_apart_from_plain_progress_and_forgets_finished_jobs()
    {
        var feed = Get<DownloadFeed>();
        var seen = new List<bool>();
        feed.Updated += (_, e) => seen.Add(e.StatusChanged);
        var job = Fakes.Job("a", DownloadStatus.Waiting);

        Send(job);                                    // first sighting
        Send(job);                                    // same state
        job.Status = DownloadStatus.Downloading;
        Send(job);                                    // new state
        Send(job);                                    // progress only
        job.Status = DownloadStatus.Completed;
        Send(job);                                    // finished
        job.Status = DownloadStatus.Waiting;
        Send(job);                                    // the id is new to the feed again

        Assert.Equal([true, false, true, false, true, true], seen);
    }

    [Fact]
    public void One_screen_failing_does_not_stop_the_others_from_updating()
    {
        var feed = Get<DownloadFeed>();
        var reached = false;
        feed.Updated += (_, _) => throw new InvalidOperationException("broken screen");
        feed.Updated += (_, _) => reached = true;

        Send(Fakes.Job("a", DownloadStatus.Downloading));

        Assert.True(reached);
    }

    // ---- Item formatting ----

    [Fact]
    public void A_card_names_the_cause_of_a_failure_in_plain_language()
    {
        var job = Fakes.Job("x", DownloadStatus.Failed);
        job.ErrorKind = DownloadErrorKind.DiskFull;
        job.ErrorMessage = "1 GB free on C:, about 4 GB needed.";

        var item = new DownloadItemViewModel(job, _l);

        Assert.Equal("The disk is full", item.ErrorMessage);
        Assert.Equal(DownloadErrorKind.DiskFull, item.ErrorKind);
    }

    [Fact]
    public void A_card_without_actions_has_inactive_buttons()
    {
        var item = new DownloadItemViewModel(Fakes.Job("x", DownloadStatus.Downloading), _l);

        Assert.False(item.CancelCommand.CanExecute(null));
        Assert.False(item.OpenFolderCommand.CanExecute(null));
        Assert.False(item.ShowFileCommand.CanExecute(null));
    }

    [Fact]
    public void A_cards_error_is_cleared_when_the_job_is_not_failed()
    {
        var job = Fakes.Job("x", DownloadStatus.Failed);
        job.ErrorKind = DownloadErrorKind.Network;
        var item = new DownloadItemViewModel(job, _l);
        Assert.NotNull(item.ErrorMessage);

        job.Status = DownloadStatus.Downloading;
        item.Apply(job, null);

        Assert.Null(item.ErrorMessage);
        Assert.Equal(DownloadErrorKind.None, item.ErrorKind);
    }

    // ---- Phase 5: pause, resume, retry, the queue summary ----

    [Fact]
    public void Each_card_offers_only_the_buttons_that_make_sense_for_its_state()
    {
        (DownloadStatus Status, bool Pause, bool Resume, bool Retry, bool Cancel)[] cases =
        [
            (DownloadStatus.Waiting, true, false, false, true),
            (DownloadStatus.Extracting, true, false, false, true),
            (DownloadStatus.Downloading, true, false, false, true),
            (DownloadStatus.Processing, false, false, false, true),
            (DownloadStatus.Paused, false, true, false, true),
            (DownloadStatus.Failed, false, false, true, false),
            (DownloadStatus.Cancelled, false, false, true, false),
            (DownloadStatus.Completed, false, false, false, false),
        ];
        var actions = Get<IDownloadItemActions>();

        foreach (var (status, pause, resume, retry, cancel) in cases)
        {
            var item = new DownloadItemViewModel(Fakes.Job("x", status), _l, actions);

            Assert.Equal(pause, item.PauseCommand.CanExecute(null));
            Assert.Equal(resume, item.ResumeCommand.CanExecute(null));
            Assert.Equal(retry, item.RetryCommand.CanExecute(null));
            Assert.Equal(cancel, item.CancelCommand.CanExecute(null));
            Assert.Equal(pause, item.CanPause);
            Assert.Equal(resume, item.IsPaused);
            Assert.Equal(retry, item.CanRetry);
        }
    }

    [Fact]
    public async Task The_card_buttons_pause_resume_and_retry_the_job()
    {
        var actions = Get<IDownloadItemActions>();
        var running = new DownloadItemViewModel(Fakes.Job("a", DownloadStatus.Downloading), _l, actions);
        var paused = new DownloadItemViewModel(Fakes.Job("b", DownloadStatus.Paused), _l, actions);
        var failed = new DownloadItemViewModel(Fakes.Job("c", DownloadStatus.Failed), _l, actions);

        await running.PauseCommand.ExecuteAsync(null);
        await paused.ResumeCommand.ExecuteAsync(null);
        await failed.RetryCommand.ExecuteAsync(null);

        await _downloads.Received(1).PauseAsync(running.Id, Arg.Any<CancellationToken>());
        await _downloads.Received(1).ResumeAsync(paused.Id, Arg.Any<CancellationToken>());
        await _downloads.Received(1).RetryAsync(failed.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failing_action_is_swallowed_and_logged_not_thrown_into_the_ui()
    {
        _downloads.PauseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db locked"));
        var item = new DownloadItemViewModel(Fakes.Job("a", DownloadStatus.Downloading), _l, Get<IDownloadItemActions>());

        await item.PauseCommand.ExecuteAsync(null);

        await _downloads.Received(1).PauseAsync(item.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Waiting_and_paused_cards_say_why_nothing_is_moving()
    {
        var waiting = new DownloadItemViewModel(Fakes.Job("a", DownloadStatus.Waiting), _l);
        var paused = new DownloadItemViewModel(Fakes.Job("b", DownloadStatus.Paused, downloaded: 5L << 20, total: 10L << 20), _l);

        Assert.Equal("Waiting for a free slot…", waiting.StageText);
        Assert.Equal("Paused. What is downloaded so far is kept.", paused.StageText);
        Assert.Equal("5 MB of 10 MB", paused.SizeText);
    }

    [Fact]
    public async Task A_card_changes_its_buttons_as_the_job_is_paused_and_resumed()
    {
        var job = Fakes.Job("a", DownloadStatus.Downloading);
        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([job]);
        var queue = Get<QueueViewModel>();
        await queue.OnNavigatedToAsync(CancellationToken.None);
        var card = queue.Items.Single();
        Assert.True(card.CanPause);

        job.Status = DownloadStatus.Paused;
        Send(job);
        Assert.False(card.CanPause);
        Assert.True(card.IsPaused);
        Assert.True(card.ResumeCommand.CanExecute(null));

        job.Status = DownloadStatus.Waiting;
        Send(job);
        Assert.True(card.CanPause);
        Assert.False(card.IsPaused);
    }

    [Fact]
    public async Task The_queue_summarises_what_is_running_waiting_and_paused_and_enables_pause_all_and_resume_all_accordingly()
    {
        _queries.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Fakes.Job("a", DownloadStatus.Downloading), Fakes.Job("b", DownloadStatus.Extracting),
            Fakes.Job("c", DownloadStatus.Waiting), Fakes.Job("d", DownloadStatus.Paused),
        ]);
        var queue = Get<QueueViewModel>();

        await queue.OnNavigatedToAsync(CancellationToken.None);

        Assert.Equal("2 downloading · 1 waiting · 1 paused", queue.SummaryText);
        Assert.True(queue.PauseAllCommand.CanExecute(null));
        Assert.True(queue.ResumeAllCommand.CanExecute(null));

        await queue.PauseAllCommand.ExecuteAsync(null);
        await queue.ResumeAllCommand.ExecuteAsync(null);
        await _downloads.Received(1).PauseAllAsync(Arg.Any<CancellationToken>());
        await _downloads.Received(1).ResumeAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pause_all_is_off_when_nothing_can_be_paused_and_resume_all_when_nothing_is_paused()
    {
        var queue = Get<QueueViewModel>();
        await queue.OnNavigatedToAsync(CancellationToken.None);
        Assert.Equal(string.Empty, queue.SummaryText);
        Assert.False(queue.PauseAllCommand.CanExecute(null));
        Assert.False(queue.ResumeAllCommand.CanExecute(null));

        var onlyPaused = Fakes.Job("p", DownloadStatus.Paused);
        Send(onlyPaused);
        Assert.False(queue.PauseAllCommand.CanExecute(null));
        Assert.True(queue.ResumeAllCommand.CanExecute(null));
        Assert.Equal("1 paused", queue.SummaryText);
    }

    [Fact]
    public async Task History_shows_a_failed_download_with_a_retry_button_and_drops_it_once_retried()
    {
        var failed = Fakes.Job("bad", DownloadStatus.Failed);
        failed.ErrorKind = DownloadErrorKind.Network;
        _queries.GetHistoryAsync(Arg.Any<HistoryQuery>(), Arg.Any<CancellationToken>()).Returns([failed]);
        _queries.CountByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DownloadStatus, int> { [DownloadStatus.Failed] = 1 });
        var history = Get<HistoryViewModel>();
        await history.OnNavigatedToAsync(CancellationToken.None);
        var item = history.Items.Single();
        Assert.True(item.CanRetry);

        await item.RetryCommand.ExecuteAsync(null);
        await _downloads.Received(1).RetryAsync(failed.Id, Arg.Any<CancellationToken>());

        // The job goes back to the queue: History reloads without it.
        _queries.GetHistoryAsync(Arg.Any<HistoryQuery>(), Arg.Any<CancellationToken>()).Returns([]);
        _queries.CountByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DownloadStatus, int>());
        failed.Status = DownloadStatus.Waiting;
        Send(failed);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(history.Items);
    }

    [Fact]
    public async Task History_gains_a_download_the_moment_it_finishes()
    {
        var history = Get<HistoryViewModel>();
        await history.OnNavigatedToAsync(CancellationToken.None);
        Assert.Empty(history.Items);

        var done = Fakes.Job("done", DownloadStatus.Completed);
        _queries.GetHistoryAsync(Arg.Any<HistoryQuery>(), Arg.Any<CancellationToken>()).Returns([done]);
        _queries.CountByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<DownloadStatus, int> { [DownloadStatus.Completed] = 1 });
        Send(done);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(["done"], history.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Settings_saves_the_number_of_simultaneous_downloads_and_automatic_start()
    {
        var settings = Get<SettingsViewModel>();
        await settings.OnNavigatedToAsync(CancellationToken.None);
        Assert.Equal(3, settings.MaxConcurrentDownloads);
        Assert.True(settings.AutoStartDownloads);
        Assert.Equal(Enumerable.Range(1, 10), settings.ConcurrencyChoices);

        settings.MaxConcurrentDownloads = 5;
        settings.AutoStartDownloads = false;

        Assert.Equal(5, _settings.Current.MaxConcurrentDownloads);
        Assert.False(_settings.Current.AutoStartDownloads);

        // Reopening the page shows what was saved.
        await settings.OnNavigatedToAsync(CancellationToken.None);
        Assert.Equal(5, settings.MaxConcurrentDownloads);
        Assert.False(settings.AutoStartDownloads);
    }

    // ---- Phase 7: playlists ----

    private static readonly PlaylistInfo Mix = new(
        "PL1", "Road Trip", "DJ Sam", "https://www.youtube.com/playlist?list=PL1", SourcePlatform.YouTube, 4,
        [
            new PlaylistEntry("a", "https://www.youtube.com/watch?v=a", "One", TimeSpan.FromSeconds(60), "https://i.ytimg.com/a.jpg", "DJ Sam", true),
            new PlaylistEntry("b", "https://www.youtube.com/watch?v=b", "Two", TimeSpan.FromSeconds(70), null, null, true),
            new PlaylistEntry("c", "https://www.youtube.com/watch?v=c", "[Private video]", null, null, null, false),
            new PlaylistEntry("d", "https://www.youtube.com/watch?v=d", "Four", TimeSpan.FromSeconds(80), null, "DJ Sam", true),
        ]);

    private async Task<DownloaderViewModel> PlaylistAsync()
    {
        _extractor.AnalyzeAsync(Arg.Any<UrlValidationResult>(), Arg.Any<CancellationToken>()).Returns(AnalysisResult.ForPlaylist(Mix));
        var vm = Get<DownloaderViewModel>();
        await vm.AnalyzeAsync("https://www.youtube.com/playlist?list=PL1");
        return vm;
    }

    private static readonly string[] OneAndFour = ["One", "Four"];

    private List<DownloadJob> _batchJobs = [];

    private void StartPlaylistReturns(int added, int already = 0, int invalid = 0) =>
        _downloads.StartPlaylistAsync(Arg.Any<PlaylistDownload>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _batchJobs = [.. call.Arg<PlaylistDownload>().Items.Take(added)
                .Select(i => JobFor(new NewDownload { SourceUrl = i.Url, NormalizedUrl = i.Url, Title = i.Title }))];
            return new PlaylistStartResult(Guid.NewGuid(), _batchJobs.Select(j => j.Clone()).ToList(), already, invalid);
        });

    [Fact]
    public async Task Every_available_playlist_item_starts_ticked_and_the_count_follows_the_ticks()
    {
        var vm = await PlaylistAsync();

        Assert.Equal(3, vm.SelectedCount);
        Assert.Equal("3 selected", vm.SelectedCountText);
        Assert.False(vm.Entries[2].IsSelected); // unavailable items can't be picked
        Assert.True(vm.DownloadPlaylistCommand.CanExecute(null));

        vm.SelectNoneCommand.Execute(null);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.DownloadPlaylistCommand.CanExecute(null));

        vm.Entries[1].IsSelected = true;
        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.DownloadPlaylistCommand.CanExecute(null));

        vm.SelectAllCommand.Execute(null);
        Assert.Equal(3, vm.SelectedCount);
        Assert.False(vm.Entries[2].IsSelected);
    }

    [Fact]
    public async Task Downloading_a_playlist_queues_only_the_ticked_available_items_in_the_chosen_video_quality()
    {
        StartPlaylistReturns(2);
        var vm = await PlaylistAsync();
        vm.Entries[1].IsSelected = false;
        vm.SelectedPlaylistQuality = vm.PlaylistQualities.Single(q => q.Value == QualityPreset.P720);

        await vm.DownloadPlaylistCommand.ExecuteAsync(null);

        await _downloads.Received(1).StartPlaylistAsync(
            Arg.Is<PlaylistDownload>(p =>
                p.Title == "Road Trip" &&
                p.Kind == MediaKind.Video &&
                p.Quality == QualityPreset.P720 &&
                p.Container == VideoContainer.Mp4 &&
                p.Items.Select(i => i.Title).SequenceEqual(OneAndFour) &&
                p.Items[0].Url == "https://www.youtube.com/watch?v=a" &&
                p.Items[0].ThumbnailUrl == "https://i.ytimg.com/a.jpg"),
            Arg.Any<CancellationToken>());
        Assert.Equal("2 items added to your queue.", vm.PlaylistStartedMessage);
        Assert.True(vm.HasPlaylistStarted);
        Assert.Equal("0 of 2 finished", vm.PlaylistProgressText);
    }

    [Fact]
    public async Task An_audio_playlist_uses_the_chosen_audio_format_and_no_video_quality()
    {
        StartPlaylistReturns(3);
        var vm = await PlaylistAsync();
        vm.SelectedPlaylistKind = vm.PlaylistKinds.Single(k => k.Value == MediaKind.Audio);
        vm.SelectedAudioFormat = vm.AudioFormats.Single(f => f.Value == AudioFormat.Flac);
        Assert.False(vm.PlaylistIsVideo);

        await vm.DownloadPlaylistCommand.ExecuteAsync(null);

        await _downloads.Received(1).StartPlaylistAsync(
            Arg.Is<PlaylistDownload>(p => p.Kind == MediaKind.Audio && p.Quality == QualityPreset.AudioOnly && p.AudioFormat == AudioFormat.Flac && p.Items.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_playlist_progress_follows_its_jobs_and_counts_failures()
    {
        StartPlaylistReturns(3);
        var vm = await PlaylistAsync();
        await vm.DownloadPlaylistCommand.ExecuteAsync(null);

        _batchJobs[0].Status = DownloadStatus.Completed;
        Send(_batchJobs[0]);
        Assert.Equal("1 of 3 finished", vm.PlaylistProgressText);

        _batchJobs[1].Status = DownloadStatus.Failed;
        Send(_batchJobs[1]);
        Assert.Equal("1 of 3 finished · 1 failed", vm.PlaylistProgressText);

        _batchJobs[2].Status = DownloadStatus.Completed;
        Send(_batchJobs[2]);
        Assert.Equal("2 of 3 finished · 1 failed", vm.PlaylistProgressText);

        // A download that isn't part of the playlist changes nothing.
        Send(Fakes.Job("other", DownloadStatus.Completed));
        Assert.Equal("2 of 3 finished · 1 failed", vm.PlaylistProgressText);
    }

    [Fact]
    public async Task Starting_a_playlist_whose_items_are_all_queued_already_says_so_and_adds_nothing()
    {
        StartPlaylistReturns(0, already: 3);
        var vm = await PlaylistAsync();

        await vm.DownloadPlaylistCommand.ExecuteAsync(null);

        Assert.Equal("Everything you picked is already in your queue.", vm.PlaylistStartedMessage);
        Assert.Null(vm.PlaylistProgressText);
    }

    [Fact]
    public async Task Skipped_items_are_mentioned_and_a_new_analysis_clears_the_playlist_message()
    {
        StartPlaylistReturns(2, already: 1);
        var vm = await PlaylistAsync();

        await vm.DownloadPlaylistCommand.ExecuteAsync(null);
        Assert.Equal("2 items added to your queue. 1 already in your queue were skipped.", vm.PlaylistStartedMessage);

        await vm.AnalyzeAsync("https://www.youtube.com/playlist?list=PL1");
        Assert.False(vm.HasPlaylistStarted);
        Assert.Null(vm.PlaylistProgressText);
    }

    [Fact]
    public async Task A_playlist_download_without_the_tools_shows_the_install_card_and_starts_nothing()
    {
        var vm = await PlaylistAsync();
        _tools.GetPath(ToolKind.Ffmpeg).Returns((string?)null);

        await vm.DownloadPlaylistCommand.ExecuteAsync(null);

        Assert.True(vm.IsError);
        await _downloads.DidNotReceiveWithAnyArgs().StartPlaylistAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void The_phase_7_labels_are_translated()
    {
        var ar = new Localizer();
        ar.SetLanguage(AppLanguage.Arabic);
        try
        {
            foreach (var key in new[] { "Library_Remove", "Library_RemoveTip", "Downloader_AsVideo", "Downloader_AsAudio", "Downloader_PlaylistFormat", "Downloader_SelectAll",
                "Downloader_SelectNone", "Downloader_SelectedCount", "Downloader_DownloadSelected", "Downloader_PlaylistAdded", "Downloader_PlaylistSkipped",
                "Downloader_PlaylistNothingNew", "Downloader_PlaylistProgress", "Downloader_PlaylistFailed" })
            {
                Assert.DoesNotContain("[", ar[key], StringComparison.Ordinal);
            }
        }
        finally
        {
            ar.SetLanguage(AppLanguage.English);
        }
    }

    [Fact]
    public async Task Settings_saves_whether_tags_and_cover_art_are_embedded()
    {
        var settings = Get<SettingsViewModel>();
        await settings.OnNavigatedToAsync(CancellationToken.None);
        Assert.True(settings.EmbedMetadata);
        Assert.True(settings.EmbedCoverArt);

        settings.EmbedMetadata = false;
        settings.EmbedCoverArt = false;

        Assert.False(_settings.Current.EmbedMetadata);
        Assert.False(_settings.Current.EmbedCoverArt);
        await settings.OnNavigatedToAsync(CancellationToken.None);
        Assert.False(settings.EmbedMetadata);
        Assert.False(settings.EmbedCoverArt);
    }

    [Fact]
    public void A_card_says_when_the_finished_file_is_being_checked()
    {
        var item = new DownloadItemViewModel(Fakes.Job("a", DownloadStatus.Processing), _l);

        item.Apply(Fakes.Job("a", DownloadStatus.Processing), new DownloadProgress(DownloadStage.Verifying, 99, 10, 10, null, null));

        Assert.Equal("Checking the file…", item.StageText);
    }

    [Fact]
    public void The_phase_6_labels_and_the_corrupt_file_message_are_translated()
    {
        var ar = new Localizer();
        ar.SetLanguage(AppLanguage.Arabic);
        try
        {
            foreach (var key in new[] { "AudioFormat_Flac", "AudioFormat_Wav", "Stage_Verifying", "Error_CorruptFile_Title", "Error_CorruptFile_Body",
                "Settings_EmbedMetadata", "Settings_EmbedMetadataDesc", "Settings_EmbedCover", "Settings_EmbedCoverDesc" })
            {
                Assert.DoesNotContain("[", ar[key], StringComparison.Ordinal);
            }
        }
        finally
        {
            ar.SetLanguage(AppLanguage.English);
        }
    }

    [Fact]
    public void The_new_queue_labels_are_translated()
    {
        var ar = new Localizer();
        ar.SetLanguage(AppLanguage.Arabic);
        try
        {
            foreach (var key in new[] { "Queue_Pause", "Queue_Resume", "Queue_Retry", "Queue_PauseAll", "Queue_ResumeAll", "Queue_SummaryRunning",
                "Queue_SummaryWaiting", "Queue_SummaryPaused", "Stage_Queued", "Stage_Paused", "Settings_Concurrent", "Settings_AutoStart" })
            {
                Assert.DoesNotContain("[", ar[key], StringComparison.Ordinal);
            }
        }
        finally
        {
            ar.SetLanguage(AppLanguage.English);
        }
    }
}

internal static class JobExtensions
{
    /// <summary>The same job under a known id, so a test can send updates for the job the screen started.</summary>
    public static DownloadJob WithId(this DownloadJob job, Guid id)
    {
        job.Id = id;
        return job;
    }
}
