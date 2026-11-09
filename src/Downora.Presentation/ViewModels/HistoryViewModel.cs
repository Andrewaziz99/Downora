using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Presentation.Localization;
using Downora.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.ViewModels;

public sealed partial class HistoryViewModel : SearchablePageViewModel
{
    private readonly IDownloadJobQueries jobs;
    private readonly IDialogService dialogs;
    private readonly IShellService shell;
    private readonly IDownloadItemActions actions;

    public HistoryViewModel(
        IDownloadJobQueries jobs,
        IDialogService dialogs,
        IShellService shell,
        DownloadFeed feed,
        IDownloadItemActions actions,
        TimeProvider time,
        Localizer localizer,
        ILogger<HistoryViewModel> logger)
        : base(time, localizer, logger)
    {
        ArgumentNullException.ThrowIfNull(feed);
        this.jobs = jobs;
        this.dialogs = dialogs;
        this.shell = shell;
        this.actions = actions;

        // A download that ends joins the list; one that is retried leaves it for the queue.
        feed.Updated += (_, e) =>
        {
            if (e.StatusChanged && (e.Update.Job.IsFinished || Items.Any(i => i.Id == e.Update.Job.Id)))
            {
                _ = ReloadAsync(debounce: false);
            }
        };
    }

    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsHistoryEmpty { get; private set; } = true;

    [ObservableProperty]
    public partial bool HasNoResults { get; private set; }

    /// <summary>Finished downloads in total, regardless of the search box.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearHistoryCommand))]
    public partial int TotalCount { get; private set; }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        var items = await jobs.GetHistoryAsync(new HistoryQuery(SearchText), cancellationToken).ConfigureAwait(true);
        var counts = await jobs.CountByStatusAsync(cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        Items.Clear();
        foreach (var job in items)
        {
            Items.Add(new DownloadItemViewModel(job, L, actions));
        }

        TotalCount = counts.Where(c => c.Key is Core.Enums.DownloadStatus.Completed or Core.Enums.DownloadStatus.Failed or Core.Enums.DownloadStatus.Cancelled)
            .Sum(c => c.Value);
        IsHistoryEmpty = TotalCount == 0;
        HasNoResults = !IsHistoryEmpty && Items.Count == 0;
    }

    private bool CanClearHistory() => TotalCount > 0;

    [RelayCommand(CanExecute = nameof(CanClearHistory))]
    private async Task ClearHistoryAsync()
    {
        var confirmed = await dialogs.ConfirmAsync(
            L["History_ClearConfirmTitle"],
            L.Format("History_ClearConfirmBody", TotalCount.ToString("N0", L.Culture)),
            L["History_Clear"],
            L["Common_Cancel"]).ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        await RunSafeAsync(() => jobs.ClearHistoryAsync()).ConfigureAwait(true);
        await ReloadAsync(debounce: false).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ShowInFolder(DownloadItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.OutputPath is { Length: > 0 } path && File.Exists(path))
        {
            shell.ShowFileInFolder(path);
        }
        else
        {
            shell.OpenFolder(item.OutputDirectory);
        }
    }
}
