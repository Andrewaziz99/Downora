using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Services;

internal sealed partial class SettingsService(
    IDbContextFactory<DownoraDbContext> contextFactory,
    IAppPaths paths,
    ILogger<SettingsService> logger) : ISettingsService
{
    private readonly Lock _gate = new();
    private AppSettings? _current;

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return (_current ?? throw new InvalidOperationException("Settings have not been loaded yet. Call LoadAsync at startup."))
                    .Clone();
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var settings = await db.Settings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == AppSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = AppSettings.CreateDefault(paths.DefaultDownloadDirectory);
            db.Settings.Add(settings);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogCreatedDefaults(settings.DownloadDirectory);
        }

        lock (_gate)
        {
            _current = settings.Clone();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = AppSettingsValidator.Validate(settings);
        if (errors.Count > 0)
        {
            throw new SettingsValidationException(errors);
        }

        var toSave = settings.Clone();
        toSave.Id = AppSettings.SingletonId;

        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            var exists = await db.Settings.AnyAsync(s => s.Id == AppSettings.SingletonId, cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                db.Settings.Update(toSave);
            }
            else
            {
                db.Settings.Add(toSave);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _current = toSave.Clone();
        }

        LogSaved();
        Changed?.Invoke(this, toSave.Clone());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created default settings; downloads go to {DownloadDirectory}")]
    private partial void LogCreatedDefaults(string downloadDirectory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Settings saved")]
    private partial void LogSaved();
}
