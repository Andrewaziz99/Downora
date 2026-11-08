using System.Windows;
using System.Windows.Threading;
using Downora.App.Services;
using Downora.App.ViewModels;
using Downora.App.Views;
using Downora.Core.Interfaces;
using Downora.Infrastructure;
using Downora.Infrastructure.Logging;
using Downora.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Downora.App;

/// <summary>Composition root: builds the host, prepares the database, then shows the main window.</summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.ForCurrentUser();
        Log.Logger = LoggingSetup.CreateLogger(paths);
        RegisterGlobalExceptionHandlers();
        Log.Information("Downora {Version} starting", AppInfo.Version);

        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            builder.Services.AddSerilog();
            builder.Services.AddDownoraInfrastructure(paths);
            builder.Services.AddSingleton<IShellService, ShellService>();
            builder.Services.AddSingleton<MainWindowViewModel>();
            builder.Services.AddSingleton<MainWindow>();

            _host = builder.Build();
            await _host.StartAsync().ConfigureAwait(true);

            var services = _host.Services;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync().ConfigureAwait(true);
            await services.GetRequiredService<ISettingsService>().LoadAsync().ConfigureAwait(true);
            var recovered = await services.GetRequiredService<IJobRecoveryService>().RecoverInterruptedJobsAsync().ConfigureAwait(true);

            var viewModel = services.GetRequiredService<MainWindowViewModel>();
            viewModel.Load(recovered);

            var window = services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
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
        MessageBox.Show(
            $"Something went wrong: {e.Exception.Message}\n\nDetails were saved to the log. You can keep using Downora.",
            "Downora",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
