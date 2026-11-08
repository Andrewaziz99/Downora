using Downora.Core.Enums;
using Downora.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Downora.Infrastructure.Tests;

public class DatabaseTests
{
    [Fact]
    public async Task Initialize_creates_folders_and_applies_all_migrations()
    {
        await using var env = await TestEnvironment.StartAsync();

        Assert.True(File.Exists(env.Paths.DatabasePath));
        Assert.True(Directory.Exists(env.Paths.LogsDirectory));
        Assert.True(Directory.Exists(env.Paths.ToolsDirectory));
        Assert.True(Directory.Exists(env.Paths.ThumbnailsDirectory));

        await using var db = await env.CreateDbAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Database_uses_write_ahead_logging()
    {
        await using var env = await TestEnvironment.StartAsync();
        await using var db = await env.CreateDbAsync();

        var mode = await db.Database.SqlQueryRaw<string>("SELECT journal_mode AS Value FROM pragma_journal_mode")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public async Task Initialize_is_safe_to_run_on_every_start()
    {
        await using var env = await TestEnvironment.StartAsync();
        await using var restarted = await env.RestartAsync();

        await using var db = await restarted.CreateDbAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Job_round_trips_enums_durations_and_utc_dates()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var created = new DateTime(2026, 11, 8, 18, 30, 0, DateTimeKind.Utc);
        var job = new DownloadJob
        {
            SourceUrl = "https://www.youtube.com/watch?v=abc",
            NormalizedUrl = "https://www.youtube.com/watch?v=abc",
            Platform = SourcePlatform.YouTube,
            MediaKind = MediaKind.Audio,
            QualityPreset = QualityPreset.AudioOnly,
            AudioFormat = AudioFormat.Flac,
            Duration = TimeSpan.FromMinutes(3.5),
            OutputDirectory = env.Paths.DefaultDownloadDirectory,
            Status = DownloadStatus.Downloading,
            CreatedAtUtc = created,
        };

        await using (var db = await env.CreateDbAsync())
        {
            db.DownloadJobs.Add(job);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = await env.CreateDbAsync())
        {
            var loaded = await db.DownloadJobs.SingleAsync(j => j.Id == job.Id, ct);
            Assert.Equal(SourcePlatform.YouTube, loaded.Platform);
            Assert.Equal(AudioFormat.Flac, loaded.AudioFormat);
            Assert.Null(loaded.Container);
            Assert.Equal(TimeSpan.FromMinutes(3.5), loaded.Duration);
            Assert.Equal(created, loaded.CreatedAtUtc);
            Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);

            var stored = await db.Database
                .SqlQueryRaw<string>("SELECT Status AS Value FROM DownloadJobs")
                .SingleAsync(ct);
            Assert.Equal("Downloading", stored);
        }
    }

    [Fact]
    public async Task Deleting_a_job_keeps_its_library_entry()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var job = new DownloadJob
        {
            SourceUrl = "https://soundcloud.com/a/b",
            NormalizedUrl = "https://soundcloud.com/a/b",
            OutputDirectory = env.Paths.DefaultDownloadDirectory,
            Status = DownloadStatus.Completed,
        };
        var item = new MediaItem { FilePath = Path.Combine(env.Root, "b.mp3"), DownloadJobId = job.Id, MediaKind = MediaKind.Audio };

        await using (var db = await env.CreateDbAsync())
        {
            db.DownloadJobs.Add(job);
            db.MediaItems.Add(item);
            await db.SaveChangesAsync(ct);

            db.DownloadJobs.Remove(job);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = await env.CreateDbAsync())
        {
            var kept = await db.MediaItems.SingleAsync(ct);
            Assert.Null(kept.DownloadJobId);
        }
    }

    [Fact]
    public async Task Library_rejects_the_same_file_twice()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var path = Path.Combine(env.Root, "same.mp4");

        await using var db = await env.CreateDbAsync();
        db.MediaItems.Add(new MediaItem { FilePath = path });
        db.MediaItems.Add(new MediaItem { FilePath = path });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Only_one_settings_row_can_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();

        await using var db = await env.CreateDbAsync();
        var second = AppSettings.CreateDefault(env.Paths.DefaultDownloadDirectory);
        second.Id = 2;
        db.Settings.Add(second);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
    }
}
