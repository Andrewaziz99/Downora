using Downora.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Downora.Infrastructure.Database;

internal sealed class DownloadJobConfiguration : IEntityTypeConfiguration<DownloadJob>
{
    public void Configure(EntityTypeBuilder<DownloadJob> builder)
    {
        builder.ToTable("DownloadJobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.SourceUrl).HasMaxLength(4096).IsRequired();
        builder.Property(j => j.NormalizedUrl).HasMaxLength(4096).IsRequired();
        builder.Property(j => j.Title).HasMaxLength(1024);
        builder.Property(j => j.Uploader).HasMaxLength(512);
        builder.Property(j => j.ThumbnailUrl).HasMaxLength(4096);
        builder.Property(j => j.FormatSelector).HasMaxLength(1024);
        builder.Property(j => j.OutputDirectory).HasMaxLength(1024).IsRequired();
        builder.Property(j => j.OutputPath).HasMaxLength(1024);
        builder.Property(j => j.ErrorMessage).HasMaxLength(2048);

        builder.Ignore(j => j.IsFinished);

        // Duplicate check: same URL + same preset while the job is not finished.
        builder.HasIndex(j => new { j.NormalizedUrl, j.QualityPreset, j.Status });
        // Queue worker: next waiting job in order.
        builder.HasIndex(j => new { j.Status, j.QueueOrder });
        builder.HasIndex(j => j.CreatedAtUtc);

        builder.HasOne(j => j.PlaylistBatch)
            .WithMany(b => b.Jobs)
            .HasForeignKey(j => j.PlaylistBatchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PlaylistBatchConfiguration : IEntityTypeConfiguration<PlaylistBatch>
{
    public void Configure(EntityTypeBuilder<PlaylistBatch> builder)
    {
        builder.ToTable("PlaylistBatches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.SourceUrl).HasMaxLength(4096).IsRequired();
        builder.Property(b => b.Title).HasMaxLength(1024);
        builder.Property(b => b.Uploader).HasMaxLength(512);
    }
}

internal sealed class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("MediaItems");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.FilePath).HasMaxLength(1024).IsRequired();
        builder.HasIndex(m => m.FilePath).IsUnique();

        builder.Property(m => m.Title).HasMaxLength(1024);
        builder.Property(m => m.Artist).HasMaxLength(512);
        builder.Property(m => m.Album).HasMaxLength(512);
        builder.Property(m => m.VideoCodec).HasMaxLength(64);
        builder.Property(m => m.AudioCodec).HasMaxLength(64);
        builder.Property(m => m.ThumbnailPath).HasMaxLength(1024);
        builder.Property(m => m.SourceUrl).HasMaxLength(4096);

        builder.HasIndex(m => m.AddedAtUtc);
        builder.HasIndex(m => m.Title);

        // The library outlives history: deleting a job only unlinks its file entry.
        builder.HasOne<DownloadJob>()
            .WithMany()
            .HasForeignKey(m => m.DownloadJobId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.DownloadDirectory).HasMaxLength(1024).IsRequired();
        builder.Property(s => s.FfmpegPathOverride).HasMaxLength(1024);
        builder.Property(s => s.BrowserForCookies).HasMaxLength(32);
        builder.ToTable(t => t.HasCheckConstraint("CK_AppSettings_Singleton", $"\"Id\" = {AppSettings.SingletonId}"));
    }
}
