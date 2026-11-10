using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Downora.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.ViewModels;

public enum DownloaderState
{
    Idle,
    Analyzing,
    Media,
    Playlist,
    Error,
}

/// <summary>Paste a link, analyze it, pick a quality (or audio only) and download it.</summary>
public sealed partial class DownloaderViewModel : PageViewModel, IDisposable
{
    /// <summary>
    /// Everything Downora needs to read and download links: yt-dlp, FFmpeg (joins video and audio, converts audio)
    /// and Deno (YouTube's JavaScript challenges).
    /// </summary>
    internal static readonly ToolKind[] RequiredTools = [ToolKind.YtDlp, ToolKind.Ffmpeg, ToolKind.Deno];

    /// <summary>Tools a download cannot run without. Deno is only needed for some sites, so it never blocks a download.</summary>
    private static readonly ToolKind[] DownloadTools = [ToolKind.YtDlp, ToolKind.Ffmpeg];

    private readonly IUrlValidator _validator;
    private readonly IMediaExtractor _extractor;
    private readonly IToolManager _tools;
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private readonly IClipboardService _clipboard;
    private readonly INavigationService _navigation;
    private readonly IDownloadJobService _downloads;
    private readonly IDownloadItemActions _actions;
    private CancellationTokenSource? _analysis;
    private string? _lastAnalyzedUrl;
    private UrlValidationResult? _analyzedUrl;
    private MediaInfo? _media;
    private PlaylistInfo? _playlist;
    private readonly Dictionary<Guid, DownloadStatus> _batch = [];
    private bool _selecting;

    public DownloaderViewModel(
        IUrlValidator validator,
        IMediaExtractor extractor,
        IToolManager tools,
        ISettingsService settings,
        IShellService shell,
        IClipboardService clipboard,
        INavigationService navigation,
        IDownloadJobService downloads,
        DownloadFeed feed,
        IDownloadItemActions actions,
        Localizer localizer,
        ILogger<DownloaderViewModel> logger)
        : base(localizer, logger)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _validator = validator;
        _extractor = extractor;
        _tools = tools;
        _settings = settings;
        _shell = shell;
        _clipboard = clipboard;
        _navigation = navigation;
        _downloads = downloads;
        _actions = actions;
        AudioFormats =
        [
            new(AudioFormat.Mp3, "AudioFormat_Mp3", localizer),
            new(AudioFormat.M4a, "AudioFormat_M4a", localizer),
            new(AudioFormat.Opus, "AudioFormat_Opus", localizer),
            new(AudioFormat.Flac, "AudioFormat_Flac", localizer),
            new(AudioFormat.Wav, "AudioFormat_Wav", localizer),
        ];
        SelectedAudioFormat = AudioFormats[0];
        PlaylistKinds =
        [
            new(MediaKind.Video, "Downloader_AsVideo", localizer),
            new(MediaKind.Audio, "Downloader_AsAudio", localizer),
        ];
        PlaylistQualities =
        [
            new(QualityPreset.Best, "Quality_Best", localizer),
            new(QualityPreset.P1080, "Quality_P1080", localizer),
            new(QualityPreset.P720, "Quality_P720", localizer),
            new(QualityPreset.P480, "Quality_P480", localizer),
            new(QualityPreset.P360, "Quality_P360", localizer),
        ];
        SelectedPlaylistKind = PlaylistKinds[0];
        SelectedPlaylistQuality = PlaylistQualities[0];
        feed.Updated += OnFeedUpdated;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial string UrlText { get; set; } = string.Empty;

    /// <summary>Why the typed link was rejected; null when it's fine.</summary>
    [ObservableProperty]
    public partial string? ValidationMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(IsAnalyzing), nameof(IsMedia), nameof(IsPlaylist), nameof(IsError))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand), nameof(DownloadVideoCommand), nameof(DownloadAudioCommand))]
    public partial DownloaderState State { get; private set; }

    public bool IsIdle => State == DownloaderState.Idle;
    public bool IsAnalyzing => State == DownloaderState.Analyzing;
    public bool IsMedia => State == DownloaderState.Media;
    public bool IsPlaylist => State == DownloaderState.Playlist;
    public bool IsError => State == DownloaderState.Error;

    [ObservableProperty]
    public partial string SavedToText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string IdleBody { get; private set; } = string.Empty;

    // ---- Single media ----

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? Uploader { get; private set; }

    [ObservableProperty]
    public partial string? DurationText { get; private set; }

    [ObservableProperty]
    public partial string? ThumbnailUrl { get; private set; }

    [ObservableProperty]
    public partial string PlatformLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLive { get; private set; }

    [ObservableProperty]
    public partial string? BestAudioText { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenInBrowserCommand))]
    public partial string? WebpageUrl { get; private set; }

    /// <summary>The video qualities (Best, 4K … 360p). Rows the site doesn't offer are shown but can't be picked.</summary>
    public ObservableCollection<QualityOptionViewModel> Qualities { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadVideoCommand))]
    public partial QualityOptionViewModel? SelectedQuality { get; set; }

    /// <summary>The audio-only choice, with the size of the best audio stream. Null when the site has no audio.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadAudioCommand))]
    public partial QualityOptionViewModel? AudioOption { get; private set; }

    public IReadOnlyList<OptionViewModel<AudioFormat>> AudioFormats { get; }

    [ObservableProperty]
    public partial OptionViewModel<AudioFormat> SelectedAudioFormat { get; set; }

    /// <summary>The download most recently started from this screen, followed live (progress, merging, failure).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartedDownload), nameof(ShowStartedSignInAction))]
    public partial DownloadItemViewModel? StartedDownload { get; private set; }

    public bool HasStartedDownload => StartedDownload is not null;

    /// <summary>"Added to your queue" / "Already in your queue".</summary>
    [ObservableProperty]
    public partial string? StartedMessage { get; private set; }

    /// <summary>The started download failed because the site wants a signed-in user, and the browser sign-in option is off.</summary>
    public bool ShowStartedSignInAction =>
        StartedDownload is { ErrorKind: DownloadErrorKind.RequiresSignIn } && !_settings.Current.UseBrowserSession;

    // ---- Playlist ----

    [ObservableProperty]
    public partial string? PlaylistCountText { get; private set; }

    [ObservableProperty]
    public partial string? PlaylistDurationText { get; private set; }

    public ObservableCollection<PlaylistEntryViewModel> Entries { get; } = [];

    public IReadOnlyList<OptionViewModel<MediaKind>> PlaylistKinds { get; }

    public IReadOnlyList<OptionViewModel<QualityPreset>> PlaylistQualities { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaylistIsVideo))]
    public partial OptionViewModel<MediaKind> SelectedPlaylistKind { get; set; }

    [ObservableProperty]
    public partial OptionViewModel<QualityPreset> SelectedPlaylistQuality { get; set; }

    /// <summary>Video playlists pick a quality; audio playlists pick a format (the same list as for a single link).</summary>
    public bool PlaylistIsVideo => SelectedPlaylistKind.Value == MediaKind.Video;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadPlaylistCommand))]
    public partial int SelectedCount { get; private set; }

    [ObservableProperty]
    public partial string? SelectedCountText { get; private set; }

    /// <summary>"12 items added to your queue", after the playlist was started.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlaylistStarted))]
    public partial string? PlaylistStartedMessage { get; private set; }

    public bool HasPlaylistStarted => PlaylistStartedMessage is not null;

    /// <summary>"3 of 12 finished · 1 failed", following the started playlist live.</summary>
    [ObservableProperty]
    public partial string? PlaylistProgressText { get; private set; }

    [RelayCommand]
    private Task OpenQueueAsync() => _navigation.NavigateToAsync<QueueViewModel>();

    [RelayCommand]
    private void SelectAll() => SetSelection(true);

    [RelayCommand]
    private void SelectNone() => SetSelection(false);

    private void SetSelection(bool selected)
    {
        _selecting = true;
        try
        {
            foreach (var entry in Entries.Where(e => e.IsAvailable))
            {
                entry.IsSelected = selected;
            }
        }
        finally
        {
            _selecting = false;
        }

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_selecting)
        {
            return;
        }

        SelectedCount = Entries.Count(e => e.IsSelected);
        SelectedCountText = L.Format("Downloader_SelectedCount", SelectedCount.ToString("N0", L.Culture));
    }

    private bool CanDownloadPlaylist() => IsPlaylist && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanDownloadPlaylist))]
    private Task DownloadPlaylistAsync() => RunSafeAsync(async () =>
    {
        if (_playlist is null)
        {
            return;
        }

        if (DownloadTools.Any(t => _tools.GetPath(t) is null))
        {
            ShowError(DownloadErrorKind.ToolMissing, null);
            return;
        }

        var settings = _settings.Current;
        var request = new PlaylistDownload
        {
            SourceUrl = _playlist.WebpageUrl.Length > 0 ? _playlist.WebpageUrl : _lastAnalyzedUrl ?? string.Empty,
            Title = _playlist.Title,
            Uploader = _playlist.Uploader,
            Platform = _playlist.Platform,
            Items = [.. Entries.Where(e => e.IsSelected && e.IsAvailable).Select(e => e.ToDownloadItem())],
            Kind = SelectedPlaylistKind.Value,
            Quality = SelectedPlaylistKind.Value == MediaKind.Audio ? QualityPreset.AudioOnly : SelectedPlaylistQuality.Value,
            Container = settings.PreferredVideoContainer,
            AudioFormat = SelectedAudioFormat.Value,
            AudioBitrateKbps = settings.DefaultAudioBitrateKbps,
        };

        var started = await _downloads.StartPlaylistAsync(request).ConfigureAwait(true);
        _batch.Clear();
        foreach (var job in started.Jobs)
        {
            _batch[job.Id] = job.Status;
        }

        var message = started.Jobs.Count == 0
            ? L["Downloader_PlaylistNothingNew"]
            : L.Format("Downloader_PlaylistAdded", started.Jobs.Count.ToString("N0", L.Culture));
        if (started.AlreadyQueued > 0 && started.Jobs.Count > 0)
        {
            message += " " + L.Format("Downloader_PlaylistSkipped", started.AlreadyQueued.ToString("N0", L.Culture));
        }

        PlaylistStartedMessage = message;
        UpdateBatchProgress();
    }, showLoading: false);

    private void UpdateBatchProgress()
    {
        if (_batch.Count == 0)
        {
            PlaylistProgressText = null;
            return;
        }

        var done = _batch.Values.Count(s => s == DownloadStatus.Completed);
        var failed = _batch.Values.Count(s => s == DownloadStatus.Failed);
        var text = L.Format("Downloader_PlaylistProgress", done.ToString("N0", L.Culture), _batch.Count.ToString("N0", L.Culture));
        if (failed > 0)
        {
            text += " · " + L.Format("Downloader_PlaylistFailed", failed.ToString("N0", L.Culture));
        }

        PlaylistProgressText = text;
    }

    // ---- Failure ----

    [ObservableProperty]
    public partial DownloadErrorKind ErrorKind { get; private set; }

    [ObservableProperty]
    public partial string ErrorTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorBody { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorDetail { get; private set; }

    public bool CanInstallTools => ErrorKind == DownloadErrorKind.ToolMissing;

    /// <summary>"Use my browser sign-in": offered only while the option is off. It opens Settings; it never turns the option on by itself.</summary>
    public bool CanUseBrowserSignIn => ErrorKind == DownloadErrorKind.RequiresSignIn && !_settings.Current.UseBrowserSession;

    /// <summary>The sign-in option is on but the browser's sign-in couldn't be read, so Settings is where to fix it.</summary>
    public bool CanOpenSignInSettings => ErrorKind == DownloadErrorKind.BrowserCookies;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallToolsCommand))]
    public partial bool IsInstalling { get; private set; }

    [ObservableProperty]
    public partial string? InstallStatus { get; private set; }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        var folder = Localizer.LeftToRight(_settings.Current.DownloadDirectory);
        SavedToText = L.Format("Downloader_SavedTo", folder);
        IdleBody = L.Format("Downloader_EmptyBody", folder);
        return Task.CompletedTask;
    }

    /// <summary>Used by Home: fill in the link and analyze it right away.</summary>
    public Task AnalyzeAsync(string url)
    {
        UrlText = url;
        return AnalyzeCommand.ExecuteAsync(null);
    }

    private bool CanAnalyze() => State != DownloaderState.Analyzing && !string.IsNullOrWhiteSpace(UrlText);

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        var url = _validator.Validate(UrlText);
        if (!url.IsValid)
        {
            ValidationMessage = L[$"Url_{url.Problem}"];
            return;
        }

        ValidationMessage = null;
        UrlText = url.Url!.AbsoluteUri;
        _lastAnalyzedUrl = UrlText;
        _analyzedUrl = url;
        StartedDownload = null;
        StartedMessage = null;
        PlaylistStartedMessage = null;
        PlaylistProgressText = null;
        _batch.Clear();
        ErrorMessage = null;

        _analysis?.Dispose();
        _analysis = new CancellationTokenSource();
        var token = _analysis.Token;
        State = DownloaderState.Analyzing;

        try
        {
            var result = await _extractor.AnalyzeAsync(url, token).ConfigureAwait(true);
            token.ThrowIfCancellationRequested();
            Show(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            State = DownloaderState.Idle;
        }
#pragma warning disable CA1031 // Any failure becomes a readable error card, never a crash.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ShowError(DownloadErrorKind.Unknown, ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => _analysis?.Cancel();

    [RelayCommand]
    private void Paste()
    {
        if (_clipboard.GetText() is { Length: > 0 } text)
        {
            UrlText = text.Trim();
            ValidationMessage = null;
        }
    }

    private bool CanOpenInBrowser() => WebpageUrl is not null;

    [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
    private void OpenInBrowser()
    {
        if (Uri.TryCreate(WebpageUrl, UriKind.Absolute, out var url))
        {
            _shell.OpenUrl(url);
        }
    }

    [RelayCommand]
    private Task ChangeFolderAsync() => _navigation.NavigateToAsync<SettingsViewModel>();

    private bool CanInstallToolsNow() => !IsInstalling;

    [RelayCommand(CanExecute = nameof(CanInstallToolsNow))]
    private async Task InstallToolsAsync()
    {
        IsInstalling = true;
        try
        {
            foreach (var tool in RequiredTools.Where(t => _tools.GetPath(t) is null))
            {
                var name = L[$"Tool_{tool}"];
                InstallStatus = L.Format("Downloader_Installing", name, string.Empty);
                var progress = new Progress<ToolInstallProgress>(p =>
                    InstallStatus = L.Format("Downloader_Installing", name, p.Fraction is { } f ? f.ToString("P0", L.Culture) : string.Empty));
                await _tools.InstallAsync(tool, progress).ConfigureAwait(true);
            }

            InstallStatus = null;
        }
        catch (ToolInstallException ex)
        {
            InstallStatus = L.Format("Error_InstallFailed", ex.Message);
            return;
        }
        finally
        {
            IsInstalling = false;
        }

        if (_lastAnalyzedUrl is { } url)
        {
            await AnalyzeAsync(url).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenSignInSettingsAsync()
    {
        await _navigation.NavigateToAsync<SettingsViewModel>().ConfigureAwait(true);
        if (_navigation.Current is SettingsViewModel settings)
        {
            settings.RevealCookieOptions();
        }
    }

    private bool CanDownloadVideo() => IsMedia && !IsLive && SelectedQuality is { IsAvailable: true };

    [RelayCommand(CanExecute = nameof(CanDownloadVideo))]
    private Task DownloadVideoAsync() => StartDownloadAsync(MediaKind.Video, SelectedQuality);

    private bool CanDownloadAudio() => IsMedia && !IsLive && AudioOption is { IsAvailable: true };

    [RelayCommand(CanExecute = nameof(CanDownloadAudio))]
    private Task DownloadAudioAsync() => StartDownloadAsync(MediaKind.Audio, AudioOption);

    private Task StartDownloadAsync(MediaKind kind, QualityOptionViewModel? option) => RunSafeAsync(async () =>
    {
        if (_media is null || _analyzedUrl is null || option is null)
        {
            return;
        }

        // Fail here, with a button that fixes it, rather than later as a failed job.
        if (DownloadTools.Any(t => _tools.GetPath(t) is null))
        {
            ShowError(DownloadErrorKind.ToolMissing, null);
            return;
        }

        var settings = _settings.Current;
        var request = new NewDownload
        {
            SourceUrl = _analyzedUrl.Url!.AbsoluteUri,
            NormalizedUrl = _analyzedUrl.NormalizedUrl ?? _analyzedUrl.Url.AbsoluteUri,
            Platform = _media.Platform,
            Title = _media.Title,
            Uploader = _media.Uploader,
            ThumbnailUrl = _media.ThumbnailUrl,
            Duration = _media.Duration,
            Kind = kind,
            Quality = kind == MediaKind.Audio ? QualityPreset.AudioOnly : option.Preset,
            Container = settings.PreferredVideoContainer,
            AudioFormat = SelectedAudioFormat.Value,
            AudioBitrateKbps = settings.DefaultAudioBitrateKbps,
            EstimatedBytes = option.EstimatedBytes,
        };

        var started = await _downloads.StartAsync(request).ConfigureAwait(true);
        StartedDownload = new DownloadItemViewModel(started.Job, L, _actions);
        StartedMessage = L[started.WasAlreadyActive ? "Downloader_AlreadyActive" : "Downloader_Started"];
    }, showLoading: false);

    /// <summary>Follows the download this screen started, so its card shows live progress and, if it fails, why.</summary>
    private void OnFeedUpdated(object? sender, DownloadFeedEvent e)
    {
        if (StartedDownload is { } started && started.Id == e.Update.Job.Id)
        {
            started.Apply(e.Update.Job, e.Update.Progress);
            OnPropertyChanged(nameof(ShowStartedSignInAction));
        }

        if (_batch.TryGetValue(e.Update.Job.Id, out var known) && known != e.Update.Job.Status)
        {
            _batch[e.Update.Job.Id] = e.Update.Job.Status;
            UpdateBatchProgress();
        }
    }

    [RelayCommand]
    private Task RetryAsync() => _lastAnalyzedUrl is { } url ? AnalyzeAsync(url) : Task.CompletedTask;

    private void Show(AnalysisResult result)
    {
        switch (result.Kind)
        {
            case AnalysisKind.Media:
                ShowMedia(result.Media!);
                break;
            case AnalysisKind.Playlist:
                ShowPlaylist(result.Playlist!);
                break;
            default:
                ShowError(result.ErrorKind, result.ErrorDetail);
                break;
        }
    }

    private void ShowMedia(MediaInfo media)
    {
        var culture = L.Culture;
        Title = media.Title;
        Uploader = media.Uploader;
        DurationText = Formatting.Duration(media.Duration, culture);
        ThumbnailUrl = media.ThumbnailUrl;
        PlatformLabel = PlatformName(media.Platform);
        WebpageUrl = media.WebpageUrl.Length > 0 ? media.WebpageUrl : _lastAnalyzedUrl;
        IsLive = media.IsLive;

        _media = media;
        Qualities.Clear();
        AudioOption = null;
        foreach (var option in QualityOptions.Build(media))
        {
            var row = new QualityOptionViewModel(option, L);
            if (option.Preset == QualityPreset.AudioOnly)
            {
                AudioOption = row;
            }
            else
            {
                Qualities.Add(row);
            }
        }

        SelectedQuality = DefaultQuality(Qualities, _settings.Current.DefaultVideoQuality);
        SelectedAudioFormat = AudioFormats.FirstOrDefault(f => f.Value == _settings.Current.PreferredAudioFormat) ?? AudioFormats[0];

        var audio = QualityOptions.BestAudio(media);
        BestAudioText = audio is null
            ? null
            : L.Format("Downloader_BestAudio", string.Join(" · ", new[]
            {
                QualityOptions.ShortCodec(audio.AudioCodec),
                audio.AudioBitrateKbps is { } abr ? $"{abr.ToString("0", culture)} kbps" : null,
            }.Where(p => p is not null)));

        State = DownloaderState.Media;
    }

    /// <summary>
    /// The saved default if the site offers it, otherwise the nearest lower quality, otherwise the best there is.
    /// (Rows run from highest to lowest, so "lower" means further down the list.)
    /// </summary>
    internal static QualityOptionViewModel? DefaultQuality(IReadOnlyList<QualityOptionViewModel> rows, QualityPreset wanted)
    {
        var start = Math.Max(0, rows.ToList().FindIndex(r => r.Preset == wanted));
        return rows.Skip(start).FirstOrDefault(r => r.IsAvailable) ?? rows.FirstOrDefault(r => r.IsAvailable);
    }

    private void ShowPlaylist(PlaylistInfo playlist)
    {
        var culture = L.Culture;
        Title = playlist.Title;
        Uploader = playlist.Uploader;
        ThumbnailUrl = playlist.Entries.FirstOrDefault(e => e.ThumbnailUrl is not null)?.ThumbnailUrl;
        PlatformLabel = PlatformName(playlist.Platform);
        WebpageUrl = playlist.WebpageUrl.Length > 0 ? playlist.WebpageUrl : _lastAnalyzedUrl;
        PlaylistCountText = L.Format("Downloader_PlaylistCount", (playlist.TotalCount ?? playlist.Entries.Count).ToString("N0", culture));

        var total = playlist.Entries.Aggregate(TimeSpan.Zero, (sum, e) => sum + (e.Duration ?? TimeSpan.Zero));
        PlaylistDurationText = total > TimeSpan.Zero ? L.Format("Downloader_PlaylistTotal", Formatting.Duration(total, culture)) : null;

        _playlist = playlist;
        _selecting = true;
        Entries.Clear();
        var index = 1;
        foreach (var entry in playlist.Entries)
        {
            Entries.Add(new PlaylistEntryViewModel(index++, entry, L, UpdateSelection) { IsSelected = entry.IsAvailable });
        }

        _selecting = false;
        UpdateSelection();
        SelectedPlaylistQuality = PlaylistQualities.FirstOrDefault(q => q.Value == _settings.Current.DefaultVideoQuality) ?? PlaylistQualities[0];
        State = DownloaderState.Playlist;
        DownloadPlaylistCommand.NotifyCanExecuteChanged();
    }

    private void ShowError(DownloadErrorKind kind, string? detail)
    {
        ErrorKind = kind;
        ErrorTitle = L[$"Error_{kind}_Title"];
        ErrorBody = L[$"Error_{kind}_Body"];
        ErrorDetail = kind == DownloadErrorKind.ToolMissing || string.IsNullOrWhiteSpace(detail)
            ? null
            : L.Format("Downloader_Details", detail);
        OnPropertyChanged(nameof(CanInstallTools));
        OnPropertyChanged(nameof(CanUseBrowserSignIn));
        OnPropertyChanged(nameof(CanOpenSignInSettings));
        State = DownloaderState.Error;
    }

    private string PlatformName(SourcePlatform platform) => platform switch
    {
        SourcePlatform.YouTube => "YouTube",
        SourcePlatform.YouTubeShorts => "YouTube Shorts",
        SourcePlatform.SoundCloud => "SoundCloud",
        SourcePlatform.Vimeo => "Vimeo",
        SourcePlatform.Dailymotion => "Dailymotion",
        SourcePlatform.Facebook => "Facebook",
        SourcePlatform.Instagram => "Instagram",
        SourcePlatform.X => "X",
        _ => L[$"Platform_{platform}"],
    };

    public void Dispose() => _analysis?.Dispose();
}

/// <summary>One row of the "Available qualities" list.</summary>
public sealed class QualityOptionViewModel
{
    public QualityOptionViewModel(QualityOption option, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(localizer);
        var culture = localizer.Culture;

        Preset = option.Preset;
        Label = localizer[$"Quality_{option.Preset}"];
        IsAvailable = option.IsAvailable;
        EstimatedBytes = option.EstimatedBytes;
        Detail = !option.IsAvailable
            ? localizer["Downloader_NotOffered"]
            : string.Join(" · ", new[]
            {
                option.Height is { } h ? $"{h}p" : null,
                option.Fps is { } fps and > 0 ? $"{fps.ToString("0", culture)} fps" : null,
                option.VideoCodec,
            }.Where(p => p is not null));
        SizeText = !option.IsAvailable
            ? string.Empty
            : option.EstimatedBytes is { } bytes
                ? option.IsEstimateApproximate
                    ? localizer.Format("Downloader_SizeApprox", Formatting.Bytes(bytes, culture))
                    : Formatting.Bytes(bytes, culture)
                : localizer["Downloader_SizeUnknown"];
    }

    public QualityPreset Preset { get; }
    public long? EstimatedBytes { get; }
    public string Label { get; }
    public string Detail { get; }
    public string SizeText { get; }
    public bool IsAvailable { get; }
}

public sealed partial class PlaylistEntryViewModel : ObservableObject
{
    private readonly PlaylistEntry _entry;
    private readonly Action? _selectionChanged;

    public PlaylistEntryViewModel(int index, PlaylistEntry entry, Localizer localizer, Action? selectionChanged = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(localizer);
        _entry = entry;
        _selectionChanged = selectionChanged;
        Index = index.ToString("00", localizer.Culture);
        Title = entry.Title;
        Uploader = entry.Uploader;
        DurationText = Formatting.Duration(entry.Duration, localizer.Culture) ?? "—";
        ThumbnailUrl = entry.ThumbnailUrl;
        IsAvailable = entry.IsAvailable;
        StatusText = entry.IsAvailable ? string.Empty : localizer["Downloader_EntryUnavailable"];
    }

    public string Index { get; }
    public string Title { get; }
    public string? Uploader { get; }
    public string DurationText { get; }
    public string? ThumbnailUrl { get; }
    public bool IsAvailable { get; }
    public string StatusText { get; }

    /// <summary>Ticked items are downloaded. Unavailable items (private, deleted) can't be ticked.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _selectionChanged?.Invoke();

    internal PlaylistDownloadItem ToDownloadItem() => new(_entry.Url, _entry.Title, _entry.Uploader, _entry.Duration, _entry.ThumbnailUrl);
}
