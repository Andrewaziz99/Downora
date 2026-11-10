using System.Windows;
using System.Windows.Threading;
using Downora.App.Services;
using Downora.App.Views;
using Downora.Core.Interfaces;
using Downora.Core.Services;
using Downora.Infrastructure;
using Downora.Infrastructure.Logging;
using Downora.Infrastructure.Services;
using Downora.Presentation;
using Downora.Presentation.Localization;
using Downora.Presentation.Services;
using Downora.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Downora.App;

/// <summary>Composition root: builds the host, prepares the database, applies theme and language, then shows the shell.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "An Application has no Dispose; the single-instance handle and tray icon are disposed in OnExit.")]
public partial class App : Application
{
    private IHost? _host;
    private SingleInstance? _instance;
    private TrayService? _tray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.ForCurrentUser();
        Log.Logger = LoggingSetup.CreateLogger(paths);
        RegisterGlobalExceptionHandlers();
        Log.Information("Downora {Version} starting", AppInfo.Version);

        // One Downora at a time: a second launch just brings the first one forward.
        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            Log.Information("Downora is already running; asking it to show its window");
            _instance.SignalRunningInstance();
            Shutdown(0);
            return;
        }

        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Services.AddSerilog();
            builder.Services.AddDownoraInfrastructure(paths);
            builder.Services.AddDownoraPresentation();
            builder.Services.AddSingleton<ThemeService>();
            builder.Services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IShellService, ShellService>();
            builder.Services.AddSingleton<IAppInfo, AppInfoProvider>();
            builder.Services.AddSingleton<IClipboardService, ClipboardService>();
            builder.Services.AddSingleton<IUiThread, UiThread>();
            builder.Services.AddSingleton<IStartupRegistry, StartupRegistry>();
            builder.Services.AddSingleton<WindowLifecycle>();
            builder.Services.AddSingleton<TrayService>();
            builder.Services.AddSingleton<MainWindow>();

            _host = builder.Build();
            await _host.StartAsync().ConfigureAwait(true);

            var services = _host.Services;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync().ConfigureAwait(true);
            var settingsService = services.GetRequiredService<ISettingsService>();
            await settingsService.LoadAsync().ConfigureAwait(true);
            await services.GetRequiredService<IJobRecoveryService>().RecoverInterruptedJobsAsync().ConfigureAwait(true);
            await services.GetRequiredService<IToolManager>().PrepareAsync().ConfigureAwait(true);
            await services.GetRequiredService<IDownloadJobService>().StartQueueAsync().ConfigureAwait(true);

            services.GetRequiredService<IStartupEntrySynchronizer>().Synchronize();

            // History retention and the daily yt-dlp update check; they never throw and nobody waits for them.
            _ = services.GetRequiredService<IAppMaintenance>().RunStartupTasksAsync();

            // Adds downloads from before the library existed and flags files that went missing; it never throws and nobody waits for it.
            _ = services.GetRequiredService<IMediaLibraryService>().SyncAsync();

            var settings = settingsService.Current;
            services.GetRequiredService<Localizer>().SetLanguage(settings.Language);

            var window = services.GetRequiredService<MainWindow>();
            var theme = services.GetRequiredService<ThemeService>();
            theme.Attach(window);
            theme.Apply(settings.Theme);

            MainWindow = window;
            var lifecycle = services.GetRequiredService<WindowLifecycle>();
            lifecycle.Attach(window);
            SessionEnding += (_, _) => lifecycle.MarkExiting();
            _tray = services.GetRequiredService<TrayService>();
            _instance.ListenForShowRequests(() => Dispatcher.BeginInvoke(lifecycle.Show));

            // Started by Windows at sign-in with "Start in the tray" on: stay in the tray. Opened by the user: always show.
            if (!(StartupCommand.IsStartupLaunch(e.Args) && settings.StartMinimized))
            {
                window.Show();
            }

            await services.GetRequiredService<ShellViewModel>().StartAsync().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Startup must never crash silently: any failure is logged and shown, then the app exits.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.Fatal(ex, "Downora failed to start");
            MessageBox.Show(
                $"Downora could not start.\n\n{ex.Message}\n\nDetails were saved to:\n{paths.LogsDirectory}",
                "Downora",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _instance?.Dispose();

        if (_host is not null)
        {
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
            finally
            {
                _host.Dispose();
            }
        }

        Log.Information("Downora exited with code {ExitCode}", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception (terminating: {IsTerminating})", args.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        var localizer = Localizer.Instance;
        MessageBox.Show(
            localizer.Format("Error_Generic", e.Exception.Message),
            "Downora",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
