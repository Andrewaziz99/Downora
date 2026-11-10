using Downora.Core.Interfaces;
using Downora.Core.Services;
using Downora.Infrastructure.Database;
using Downora.Infrastructure.Downloads;
using Downora.Infrastructure.Extraction;
using Downora.Infrastructure.Processes;
using Downora.Infrastructure.Tools;
using Downora.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Downora.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers data access, settings and startup services.</summary>
    public static IServiceCollection AddDownoraInfrastructure(this IServiceCollection services, IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();

        // A factory gives each operation its own short-lived context, which is safe across background threads.
        services.AddDbContextFactory<DownoraDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IJobRecoveryService, JobRecoveryService>();
        services.AddSingleton<IDownloadJobQueries, DownloadJobQueries>();
        services.AddSingleton<IMediaLibraryService, MediaLibraryService>();
        services.AddSingleton<IDiskSpaceService, DiskSpaceService>();

        // Analysis and external tools.
        services.AddSingleton<ISourceDetector, SourceDetector>();
        services.AddSingleton<IUrlValidator, UrlValidator>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IToolManager, ToolManager>();
        services.AddSingleton<IMediaExtractor, YtDlpMediaExtractor>();

        // Downloading: the engine drives yt-dlp; the job service saves state and stops with the host so no tool outlives the app.
        services.AddSingleton<IMediaFileVerifier, FfprobeFileVerifier>();
        services.AddSingleton<IDownloadEngine, YtDlpDownloadEngine>();
        services.AddSingleton<DownloadJobService>();
        services.AddSingleton<IDownloadJobService>(sp => sp.GetRequiredService<DownloadJobService>());
        services.AddHostedService(sp => sp.GetRequiredService<DownloadJobService>());
        services.AddSingleton(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Downora/0.3 (+https://github.com/Andrewaziz99/Downora)");
            return http;
        });

        return services;
    }
}
