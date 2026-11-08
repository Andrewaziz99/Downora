using Downora.Core.Enums;
using Downora.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Downora.Infrastructure.Database;

public sealed class DownoraDbContext(DbContextOptions<DownoraDbContext> options) : DbContext(options)
{
    public DbSet<DownloadJob> DownloadJobs => Set<DownloadJob>();
    public DbSet<PlaylistBatch> PlaylistBatches => Set<PlaylistBatch>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Enums are stored by name: readable in the DB and safe if members are reordered.
        configurationBuilder.Properties<DownloadStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<MediaKind>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<SourcePlatform>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<QualityPreset>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<VideoContainer>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<AudioFormat>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<AppTheme>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<AppLanguage>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<DownloadErrorKind>().HaveConversion<string>().HaveMaxLength(32);

        // SQLite has no native time types: ticks keep TimeSpan sortable, and DateTimes are always UTC.
        configurationBuilder.Properties<TimeSpan>().HaveConversion<long>();
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DownoraDbContext).Assembly);
    }

    /// <summary>Reads stored values back as <see cref="DateTimeKind.Utc"/> and normalizes writes to UTC.</summary>
    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
