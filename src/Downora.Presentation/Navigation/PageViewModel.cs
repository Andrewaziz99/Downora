using CommunityToolkit.Mvvm.ComponentModel;
using Downora.Presentation.Localization;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.Navigation;

/// <summary>Base for every screen in the sidebar. Pages reload their data each time they are shown.</summary>
public abstract partial class PageViewModel(Localizer localizer, ILogger logger) : ObservableObject
{
    protected Localizer L { get; } = localizer;

    [ObservableProperty]
    public partial bool IsLoading { get; protected set; }

    /// <summary>A user-facing message when loading or an action failed; null when everything is fine.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; protected set; }

    public virtual Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Runs page work, showing a friendly error and logging details instead of crashing the window.</summary>
    protected async Task RunSafeAsync(Func<Task> work, bool showLoading = true)
    {
        ArgumentNullException.ThrowIfNull(work);
        ErrorMessage = null;
        if (showLoading)
        {
            IsLoading = true;
        }

        try
        {
            await work().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request (navigation, typing): nothing to report.
        }
#pragma warning disable CA1031 // A page must survive any failure of its own work; the error is logged and shown.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogPageError(logger, GetType().Name, ex);
            ErrorMessage = L.Format("Error_Generic", ex.Message);
        }
        finally
        {
            if (showLoading)
            {
                IsLoading = false;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Page} failed")]
    private static partial void LogPageError(ILogger logger, string page, Exception exception);
}
