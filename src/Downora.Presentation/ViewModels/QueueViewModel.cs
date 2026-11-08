using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.ViewModels;

/// <summary>Which slice of the queue is shown.</summary>
public enum QueueFilter
{
    All,
    InProgress,
    Waiting,
    Paused,
}

/// <summary>Unfinished downloads. Finished ones move to History.</summary>
public sealed partial class QueueViewModel : PageViewModel
{
    private readonly IDownloadJobQueries _jobs;
    private IReadOnlyList<DownloadJob> _all = [];

    public QueueViewModel(IDownloadJobQueries jobs, Localizer localizer, ILogger<QueueViewModel> logger)
        : base(localizer, logger)
    {
        _jobs = jobs;
        Filters =
        [
            new(QueueFilter.All, "Queue_FilterAll", localizer),
            new(QueueFilter.InProgress, "Queue_FilterActive", localizer),
            new(QueueFilter.Waiting, "Status_Waiting", localizer),
            new(QueueFilter.Paused, "Status_Paused", localizer),
        ];
        SelectedFilter = Filters[0];
    }

    public IReadOnlyList<OptionViewModel<QueueFilter>> Filters { get; }

    [ObservableProperty]
    public partial OptionViewModel<QueueFilter> SelectedFilter { get; set; }

    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

    /// <summary>Nothing in the queue at all.</summary>
    [ObservableProperty]
    public partial bool IsQueueEmpty { get; private set; } = true;

    /// <summary>The queue has jobs, but none match the selected tab.</summary>
    [ObservableProperty]
    public partial bool IsFilterEmpty { get; private set; }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => RunSafeAsync(async () =>
    {
        _all = await _jobs.GetActiveAsync(cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var filter in Filters)
        {
            filter.Count = _all.Count(j => Matches(filter.Value, j.Status));
        }

        Apply();
    });

    partial void OnSelectedFilterChanged(OptionViewModel<QueueFilter> value) => Apply();

    internal static bool Matches(QueueFilter filter, DownloadStatus status) => filter switch
    {
        QueueFilter.InProgress => status is DownloadStatus.Extracting or DownloadStatus.Downloading or DownloadStatus.Processing,
        QueueFilter.Waiting => status == DownloadStatus.Waiting,
        QueueFilter.Paused => status == DownloadStatus.Paused,
        _ => true,
    };

    private void Apply()
    {
        Items.Clear();
        foreach (var job in _all.Where(j => Matches(SelectedFilter.Value, j.Status)))
        {
            Items.Add(new DownloadItemViewModel(job, L));
        }

        IsQueueEmpty = _all.Count == 0;
        IsFilterEmpty = !IsQueueEmpty && Items.Count == 0;
    }
}
