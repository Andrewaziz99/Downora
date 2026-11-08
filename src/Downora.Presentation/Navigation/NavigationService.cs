using Microsoft.Extensions.DependencyInjection;

namespace Downora.Presentation.Navigation;

public interface INavigationService
{
    PageViewModel? Current { get; }

    event EventHandler<PageViewModel>? Navigated;

    Task NavigateToAsync<TPage>() where TPage : PageViewModel;

    Task NavigateToAsync(Type pageType);
}

/// <summary>Swaps the page shown in the shell. Pages are singletons, so filters and scroll-relevant state survive navigation.</summary>
internal sealed class NavigationService(IServiceProvider services) : INavigationService, IDisposable
{
    private CancellationTokenSource? _loading;

    public PageViewModel? Current { get; private set; }

    public event EventHandler<PageViewModel>? Navigated;

    public Task NavigateToAsync<TPage>() where TPage : PageViewModel => NavigateToAsync(typeof(TPage));

    public async Task NavigateToAsync(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (!typeof(PageViewModel).IsAssignableFrom(pageType))
        {
            throw new ArgumentException($"{pageType.Name} is not a page.", nameof(pageType));
        }

        var page = (PageViewModel)services.GetRequiredService(pageType);

        // Cancel the previous page's load so a slow query can't overwrite the new screen.
        if (_loading is not null)
        {
            await _loading.CancelAsync().ConfigureAwait(true);
            _loading.Dispose();
        }

        _loading = new CancellationTokenSource();

        Current = page;
        Navigated?.Invoke(this, page);
        await page.OnNavigatedToAsync(_loading.Token).ConfigureAwait(true);
    }

    public void Dispose() => _loading?.Dispose();
}
