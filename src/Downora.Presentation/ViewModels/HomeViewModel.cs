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

public sealed partial class HomeViewModel : PageViewModel
{
    public const int ActivePreviewCount = 3;
    public const int RecentPreviewCount = 4;

    private readonly IDownloadJobQueries _jobs;
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private readonly INavigationService _navigation;
    private readonly IUrlValidator _urlValidator;
    private readonly IClipboardService _clipboard;
    private readonly IDownloadItemActions _actions;
    private readonly TimeProvider _time;

    public HomeViewModel(
        IDownloadJobQueries jobs,
        ISettingsService settings,
        IShellService shell,
        INavigationService navigation,
        IUrlValidator urlValidator,
        IClipboardService clipboard,
        DownloadFeed feed,
        IDownloadItemActions actions,
        TimeProvider time,
        Localizer localizer,
        ILogger<HomeViewModel> logger)
        : base(localizer, logger)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _jobs = jobs;
        _settings = settings;
        _shell = shell;
        _navigation = navigation;
        _urlValidator = urlValidator;
        _clipboard = clipboard;
        _actions = actions;
        _time = time;
        feed.Updated += OnFeedUpdated;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial string UrlText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ValidationMessage { get; private set; }

    [ObservableProperty]
    public partial string Greeting { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalDownloads { get; private set; } = "0";

    [ObservableProperty]
    public partial string TotalDownloadsHint { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DataDownloaded { get; private set; } = "0 B";

    [ObservableProperty]
    public partial string ActiveNow { get; private set; } = "0";

    [ObservableProperty]
    public partial string ActiveNowHint { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string SuccessRate { get; private set; } = "—";

    [ObservableProperty]
    public partial string SavedInText { get; private set; } = string.Empty;

    public ObservableCollection<DownloadItemViewModel> ActiveDownloads { get; } = [];

    public ObservableCollection<DownloadItemViewModel> RecentDownloads { get; } = [];

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) =>
        RunSafeAsync(() => LoadAsync(cancellationToken));

    /// <summary>
    /// Progress only moves the bars. A download starting, merging or ending also changes the counts and the
    /// recent list, so those reload from the database.
    /// </summary>
    private void OnFeedUpdated(object? sender, DownloadFeedEvent e)
    {
        var job = e.Update.Job;
        ActiveDownloads.FirstOrDefault(i => i.Id == job.Id)?.Apply(job, e.Update.Progress);

        if (e.StatusChanged)
        {
            _ = RunSafeAsync(() => LoadAsync(CancellationToken.None), showLoading: false);
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Greeting = L[GreetingKey(_time.GetLocalNow().Hour)];
        SavedInText = L.Format("Home_SavedIn", Localizer.LeftToRight(_settings.Current.DownloadDirectory));

        var stats = await _jobs.GetStatisticsAsync(cancellationToken).ConfigureAwait(true);
        var active = await _jobs.GetActiveAsync(cancellationToken).ConfigureAwait(true);
        var recent = await _jobs.GetHistoryAsync(new HistoryQuery(Status: DownloadStatus.Completed, Take: RecentPreviewCount), cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        var culture = L.Culture;
        TotalDownloads = stats.TotalCompleted.ToString("N0", culture);
        TotalDownloadsHint = L.Format("Home_StatTotalHint", stats.CompletedLast7Days.ToString("N0", culture));
        DataDownloaded = Formatting.Bytes(stats.BytesDownloaded, culture);
        ActiveNow = stats.ActiveCount.ToString("N0", culture);
        ActiveNowHint = L.Format("Home_StatActiveHint", (active.Count - stats.ActiveCount).ToString("N0", culture));
        SuccessRate = stats.SuccessRate is { } rate ? rate.ToString("P1", culture) : "—";

        // Keep the cards that are already live so their speed and time left don't flicker on every reload.
        var previews = active.Take(ActivePreviewCount).Select(job =>
        {
            var existing = ActiveDownloads.FirstOrDefault(i => i.Id == job.Id);
            existing?.Apply(job, null);
            return existing ?? new DownloadItemViewModel(job, L, _actions);
        }).ToList();
        Replace(ActiveDownloads, previews);
        Replace(RecentDownloads, recent.Select(j => new DownloadItemViewModel(j, L, _actions)));
    }

    /// <summary>Morning until noon, afternoon until 6 pm, evening after.</summary>
    internal static string GreetingKey(int hour) => hour switch
    {
        < 12 => "Home_GreetingMorning",
        < 18 => "Home_GreetingAfternoon",
        _ => "Home_GreetingEvening",
    };

    private bool CanAnalyze() => !string.IsNullOrWhiteSpace(UrlText);

    /// <summary>Checks the link here, then hands it to the Downloader page to analyze.</summary>
    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        var url = _urlValidator.Validate(UrlText);
        if (!url.IsValid)
        {
            ValidationMessage = L[$"Url_{url.Problem}"];
            return;
        }

        ValidationMessage = null;
        UrlText = string.Empty;
        await _navigation.NavigateToAsync<DownloaderViewModel>().ConfigureAwait(true);
        if (_navigation.Current is DownloaderViewModel downloader)
        {
            await downloader.AnalyzeAsync(url.Url!.AbsoluteUri).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void Paste()
    {
        if (_clipboard.GetText() is { Length: > 0 } text)
        {
            UrlText = text.Trim();
            ValidationMessage = null;
        }
    }

    [RelayCommand]
    private void OpenDownloadFolder() => _shell.OpenFolder(_settings.Current.DownloadDirectory);

    [RelayCommand]
    private Task ViewQueueAsync() => _navigation.NavigateToAsync<QueueViewModel>();

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        target.Clear();
        foreach (var item in list)
        {
            target.Add(item);
        }
    }
}
