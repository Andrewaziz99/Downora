using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Presentation.Localization;
using Downora.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.ViewModels;

public sealed partial class LibraryViewModel : SearchablePageViewModel
{
    private readonly IMediaLibraryService _library;
    private readonly IShellService _shell;
    private readonly bool _ready;

    public LibraryViewModel(
        IMediaLibraryService library,
        IShellService shell,
        TimeProvider time,
        Localizer localizer,
        ILogger<LibraryViewModel> logger)
        : base(time, localizer, logger)
    {
        _library = library;
        _shell = shell;
        KindFilters =
        [
            new(null, "Library_All", localizer),
            new(MediaKind.Video, "Library_Video", localizer),
            new(MediaKind.Audio, "Library_Audio", localizer),
        ];
        SortOptions =
        [
            new(LibrarySort.DateAdded, "Library_SortDate", localizer),
            new(LibrarySort.Title, "Library_SortTitle", localizer),
            new(LibrarySort.Duration, "Library_SortDuration", localizer),
            new(LibrarySort.FileSize, "Library_SortSize", localizer),
        ];
        SelectedKind = KindFilters[0];
        SelectedSort = SortOptions[0];
        _ready = true;
    }

    public IReadOnlyList<OptionViewModel<MediaKind?>> KindFilters { get; }

    public IReadOnlyList<OptionViewModel<LibrarySort>> SortOptions { get; }

    [ObservableProperty]
    public partial OptionViewModel<MediaKind?> SelectedKind { get; set; }

    [ObservableProperty]
    public partial OptionViewModel<LibrarySort> SelectedSort { get; set; }

    public ObservableCollection<MediaItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsLibraryEmpty { get; private set; } = true;

    [ObservableProperty]
    public partial bool HasNoResults { get; private set; }

    protected override async Task LoadAsync(CancellationToken cancellationToken)
    {
        // Title sorts A→Z; the others show largest / newest first.
        var descending = SelectedSort.Value != LibrarySort.Title;
        var query = new LibraryQuery(SearchText, SelectedKind.Value, SelectedSort.Value, descending);
        var items = await _library.QueryAsync(query, cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(new MediaItemViewModel(item, L));
        }

        var filtering = !string.IsNullOrWhiteSpace(SearchText) || SelectedKind.Value is not null;
        IsLibraryEmpty = Items.Count == 0 && !filtering;
        HasNoResults = Items.Count == 0 && filtering;
    }

    partial void OnSelectedKindChanged(OptionViewModel<MediaKind?> value)
    {
        if (_ready)
        {
            _ = ReloadAsync(debounce: false);
        }
    }

    partial void OnSelectedSortChanged(OptionViewModel<LibrarySort> value)
    {
        if (_ready)
        {
            _ = ReloadAsync(debounce: false);
        }
    }

    [RelayCommand]
    private void Open(MediaItemViewModel? item)
    {
        if (item is { IsMissing: false })
        {
            _shell.OpenFile(item.FilePath);
        }
    }

    [RelayCommand]
    private void ShowInFolder(MediaItemViewModel? item)
    {
        if (item is { IsMissing: false })
        {
            _shell.ShowFileInFolder(item.FilePath);
        }
    }
}
