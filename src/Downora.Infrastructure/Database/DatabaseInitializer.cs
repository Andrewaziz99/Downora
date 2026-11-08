using Downora.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Database;

internal sealed partial class DatabaseInitializer(
    IAppPaths paths,
    IDbContextFactory<DownoraDbContext> contextFactory,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        if (pending.Count > 0)
        {
            var names = string.Join(", ", pending);
            LogApplyingMigrations(pending.Count, names);
        }

        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        // WAL lets the UI read history while the queue worker writes progress.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);

        LogDatabaseReady(paths.DatabasePath);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} database migration(s): {Migrations}")]
    private partial void LogApplyingMigrations(int count, string migrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database ready at {DatabasePath}")]
    private partial void LogDatabaseReady(string databasePath);
}
