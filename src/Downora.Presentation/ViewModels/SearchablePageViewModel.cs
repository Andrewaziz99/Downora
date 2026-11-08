using CommunityToolkit.Mvvm.ComponentModel;
using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.ViewModels;

/// <summary>A page with a search box: typing reloads after a short pause, and a newer search cancels an older one.</summary>
public abstract partial class SearchablePageViewModel(TimeProvider time, Localizer localizer, ILogger logger)
    : PageViewModel(localizer, logger), IDisposable
{
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _pending;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => ReloadAsync(debounce: false, cancellationToken);

    /// <summary>Loads the page's items for the current search and filters.</summary>
    protected abstract Task LoadAsync(CancellationToken cancellationToken);

    protected Task ReloadAsync(bool debounce, CancellationToken outer = default)
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = CancellationTokenSource.CreateLinkedTokenSource(outer);
        var token = _pending.Token;

        return RunSafeAsync(async () =>
        {
            if (debounce)
            {
                await Task.Delay(SearchDelay, time, token).ConfigureAwait(true);
            }

            await LoadAsync(token).ConfigureAwait(true);
        }, showLoading: !debounce);
    }

    partial void OnSearchTextChanged(string value) => _ = ReloadAsync(debounce: true);

    public void Dispose()
    {
        _pending?.Dispose();
        GC.SuppressFinalize(this);
    }
}
