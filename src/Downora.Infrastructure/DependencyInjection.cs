using Downora.Core.Interfaces;
using Downora.Infrastructure.Database;
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

        return services;
    }
}
