using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Downora.Presentation.Services;
using Downora.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Downora.Presentation;

public static class DependencyInjection
{
    /// <summary>
    /// Registers navigation and view models. The host app must also register
    /// <see cref="Services.IThemeService"/>, <see cref="Services.IDialogService"/>,
    /// <see cref="Services.IShellService"/>, <see cref="Services.IAppInfo"/>, <see cref="Services.IClipboardService"/> and <see cref="Services.IUiThread"/>.
    /// </summary>
    public static IServiceCollection AddDownoraPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(Localizer.Instance);
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();

        // Live downloads: one feed brings job updates to the UI thread for every screen that shows them.
        services.AddSingleton<DownloadFeed>();
        services.AddSingleton<IDownloadItemActions, DownloadItemActions>();

        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<DownloaderViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();

        return services;
    }
}
