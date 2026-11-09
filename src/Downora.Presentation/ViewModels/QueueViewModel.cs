using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Downora.Presentation.Services;
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

/// <summary>Unfinished downloads, updating live as they run. Finished ones move to History.</summary>
public sealed partial class QueueViewModel : PageViewModel
{
    private readonly IDownloadJobQueries _jobs;
    private readonly IDownloadItemActions _actions;
    private readonly Dictionary<Guid, DownloadItemViewModel> _all = [];

    public QueueViewModel(
        IDownloadJobQueries jobs,
        DownloadFeed feed,
        IDownloadItemActions actions,
        Localizer localizer,
        ILogger<QueueViewModel> logger)
        : base(localizer, logger)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _jobs = jobs;
        _actions = actions;
        Filters =
        [
            new(QueueFilter.All, "Queue_FilterAll", localizer),
            new(QueueFilter.InProgress, "Queue_FilterActive", localizer),
            new(QueueFilter.Waiting, "Status_Waiting", localizer),
            new(QueueFilter.Paused, "Status_Paused", localizer),
        ];
        SelectedFilter = Filters[0];
        feed.Updated += OnFeedUpdated;
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
        var active = await _jobs.GetActiveAsync(cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        // Keep the cards that are already live (they hold speed and time left) and add or drop the rest.
        var loaded = new Dictionary<Guid, DownloadItemViewModel>();
        foreach (var job in active)
        {
            if (_all.TryGetValue(job.Id, out var existing))
            {
                existing.Apply(job, null);
                loaded[job.Id] = existing;
            }
            else
            {
                loaded[job.Id] = new DownloadItemViewModel(job, L, _actions);
            }
        }

        _all.Clear();
        foreach (var (id, item) in loaded)
        {
            _all[id] = item;
        }

        Refresh();
    });

    partial void OnSelectedFilterChanged(OptionViewModel<QueueFilter> value) => Refresh();

    internal static bool Matches(QueueFilter filter, DownloadStatus status) => filter switch
    {
        QueueFilter.InProgress => status is DownloadStatus.Extracting or DownloadStatus.Downloading or DownloadStatus.Processing,
        QueueFilter.Waiting => status == DownloadStatus.Waiting,
        QueueFilter.Paused => status == DownloadStatus.Paused,
        _ => true,
    };

    private void OnFeedUpdated(object? sender, DownloadFeedEvent e)
    {
        var job = e.Update.Job;
        if (job.IsFinished)
        {
            if (_all.Remove(job.Id))
            {
                Refresh();
            }

            return;
        }

        if (_all.TryGetValue(job.Id, out var item))
        {
            item.Apply(job, e.Update.Progress);
            if (e.StatusChanged)
            {
                Refresh();
            }
        }
        else
        {
            _all[job.Id] = new DownloadItemViewModel(job, L, _actions);
            Refresh();
        }
    }

    /// <summary>Recounts the tabs and rebuilds the visible list: running work first, then what is waiting, then what is paused.</summary>
    private void Refresh()
    {
        foreach (var filter in Filters)
        {
            filter.Count = _all.Values.Count(i => Matches(filter.Value, i.Status));
        }

        var visible = _all.Values
            .Where(i => Matches(SelectedFilter.Value, i.Status))
            .OrderBy(i => Rank(i.Status))
            .ThenBy(i => i.QueueOrder)
            .ToList();

        // Change only what differs, so a card being clicked isn't rebuilt under the pointer.
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (!visible.Contains(Items[index]))
            {
                Items.RemoveAt(index);
            }
        }

        for (var index = 0; index < visible.Count; index++)
        {
            if (index < Items.Count && ReferenceEquals(Items[index], visible[index]))
            {
                continue;
            }

            var current = Items.IndexOf(visible[index]);
            if (current >= 0)
            {
                Items.Move(current, index);
            }
            else
            {
                Items.Insert(index, visible[index]);
            }
        }

        IsQueueEmpty = _all.Count == 0;
        IsFilterEmpty = !IsQueueEmpty && Items.Count == 0;
    }

    private static int Rank(DownloadStatus status) => status switch
    {
        DownloadStatus.Downloading => 0,
        DownloadStatus.Processing => 1,
        DownloadStatus.Extracting => 2,
        DownloadStatus.Waiting => 3,
        DownloadStatus.Paused => 4,
        _ => 5,
    };
}
