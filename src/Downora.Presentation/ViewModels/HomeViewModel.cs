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

public sealed partial class HomeViewModel(
    IDownloadJobQueries jobs,
    ISettingsService settings,
    IShellService shell,
    INavigationService navigation,
    TimeProvider time,
    Localizer localizer,
    ILogger<HomeViewModel> logger) : PageViewModel(localizer, logger)
{
    public const int ActivePreviewCount = 3;
    public const int RecentPreviewCount = 4;

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

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => RunSafeAsync(async () =>
    {
        Greeting = L[GreetingKey(time.GetLocalNow().Hour)];
        SavedInText = L.Format("Home_SavedIn", Localizer.LeftToRight(settings.Current.DownloadDirectory));

        var stats = await jobs.GetStatisticsAsync(cancellationToken).ConfigureAwait(true);
        var active = await jobs.GetActiveAsync(cancellationToken).ConfigureAwait(true);
        var recent = await jobs.GetHistoryAsync(new HistoryQuery(Status: DownloadStatus.Completed, Take: RecentPreviewCount), cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        var culture = L.Culture;
        TotalDownloads = stats.TotalCompleted.ToString("N0", culture);
        TotalDownloadsHint = L.Format("Home_StatTotalHint", stats.CompletedLast7Days.ToString("N0", culture));
        DataDownloaded = Formatting.Bytes(stats.BytesDownloaded, culture);
        ActiveNow = stats.ActiveCount.ToString("N0", culture);
        ActiveNowHint = L.Format("Home_StatActiveHint", (active.Count - stats.ActiveCount).ToString("N0", culture));
        SuccessRate = stats.SuccessRate is { } rate ? rate.ToString("P1", culture) : "—";

        Replace(ActiveDownloads, active.Take(ActivePreviewCount).Select(j => new DownloadItemViewModel(j, L)));
        Replace(RecentDownloads, recent.Select(j => new DownloadItemViewModel(j, L)));
    });

    /// <summary>Morning until noon, afternoon until 6 pm, evening after.</summary>
    internal static string GreetingKey(int hour) => hour switch
    {
        < 12 => "Home_GreetingMorning",
        < 18 => "Home_GreetingAfternoon",
        _ => "Home_GreetingEvening",
    };

    [RelayCommand]
    private void OpenDownloadFolder() => shell.OpenFolder(settings.Current.DownloadDirectory);

    [RelayCommand]
    private Task ViewQueueAsync() => navigation.NavigateToAsync<QueueViewModel>();

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}

public sealed partial class DownloaderViewModel(
    ISettingsService settings,
    INavigationService navigation,
    Localizer localizer,
    ILogger<DownloaderViewModel> logger) : PageViewModel(localizer, logger)
{
    [ObservableProperty]
    public partial string EmptyBody { get; private set; } = string.Empty;

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        EmptyBody = L.Format("Downloader_EmptyBody", Localizer.LeftToRight(settings.Current.DownloadDirectory));
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ChangeFolderAsync() => navigation.NavigateToAsync<SettingsViewModel>();
}

