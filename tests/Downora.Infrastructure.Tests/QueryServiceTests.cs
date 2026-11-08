using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;

namespace Downora.Infrastructure.Tests;

public class QueryServiceTests
{
    private static DownloadJob Job(TestEnvironment env, string title, DownloadStatus status, long bytes = 0, int order = 0, DateTime? completed = null) => new()
    {
        SourceUrl = $"https://example.com/{Uri.EscapeDataString(title)}",
        NormalizedUrl = $"https://example.com/{Uri.EscapeDataString(title)}",
        Title = title,
        OutputDirectory = env.Paths.DefaultDownloadDirectory,
        Status = status,
        TotalBytes = bytes,
        QueueOrder = order,
        CreatedAtUtc = DateTime.UtcNow.AddHours(-order),
        CompletedAtUtc = completed,
    };

    private static async Task SeedAsync(TestEnvironment env, params object[] entities)
    {
        await using var db = await env.CreateDbAsync();
        db.AddRange(entities);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Active_jobs_exclude_finished_ones_and_put_running_work_first()
    {
        await using var env = await TestEnvironment.StartAsync();
        await SeedAsync(env,
            Job(env, "paused", DownloadStatus.Paused, order: 0),
            Job(env, "waiting-2", DownloadStatus.Waiting, order: 2),
            Job(env, "waiting-1", DownloadStatus.Waiting, order: 1),
            Job(env, "downloading", DownloadStatus.Downloading, order: 5),
            Job(env, "done", DownloadStatus.Completed),
            Job(env, "failed", DownloadStatus.Failed));

        var active = await env.Get<IDownloadJobQueries>().GetActiveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["downloading", "waiting-1", "waiting-2", "paused"], active.Select(j => j.Title));
    }

    [Fact]
    public async Task Counts_are_grouped_by_status()
    {
        await using var env = await TestEnvironment.StartAsync();
        await SeedAsync(env,
            Job(env, "a", DownloadStatus.Waiting),
            Job(env, "b", DownloadStatus.Waiting),
            Job(env, "c", DownloadStatus.Failed));

        var counts = await env.Get<IDownloadJobQueries>().CountByStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, counts[DownloadStatus.Waiting]);
        Assert.Equal(1, counts[DownloadStatus.Failed]);
        Assert.False(counts.ContainsKey(DownloadStatus.Completed));
    }

    [Fact]
    public async Task History_is_newest_first_and_searchable_by_title()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var now = DateTime.UtcNow;
        await SeedAsync(env,
            Job(env, "Ocean documentary", DownloadStatus.Completed, completed: now.AddDays(-2)),
            Job(env, "Jazz album", DownloadStatus.Completed, completed: now.AddDays(-1)),
            Job(env, "Ocean sounds", DownloadStatus.Failed, completed: now),
            Job(env, "Ocean live", DownloadStatus.Downloading));

        var queries = env.Get<IDownloadJobQueries>();

        var all = await queries.GetHistoryAsync(new HistoryQuery(), ct);
        Assert.Equal(["Ocean sounds", "Jazz album", "Ocean documentary"], all.Select(j => j.Title));

        var ocean = await queries.GetHistoryAsync(new HistoryQuery(Search: "ocean"), ct);
        Assert.Equal(["Ocean sounds", "Ocean documentary"], ocean.Select(j => j.Title));

        var failedOnly = await queries.GetHistoryAsync(new HistoryQuery(Status: DownloadStatus.Failed), ct);
        Assert.Equal("Ocean sounds", Assert.Single(failedOnly).Title);
    }

    [Theory]
    [InlineData("100%")]
    [InlineData("a_b")]
    public async Task Search_treats_like_wildcards_as_plain_text(string search)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        await SeedAsync(env,
            Job(env, "100% pure", DownloadStatus.Completed),
            Job(env, "a_b mix", DownloadStatus.Completed),
            Job(env, "1000 tracks", DownloadStatus.Completed),
            Job(env, "axb", DownloadStatus.Completed));

        var hits = await env.Get<IDownloadJobQueries>().GetHistoryAsync(new HistoryQuery(Search: search), ct);

        Assert.Single(hits);
    }

    [Fact]
    public async Task Statistics_count_completed_bytes_active_and_success_rate()
    {
        await using var env = await TestEnvironment.StartAsync();
        var now = DateTime.UtcNow;
        await SeedAsync(env,
            Job(env, "old", DownloadStatus.Completed, bytes: 1000, completed: now.AddDays(-30)),
            Job(env, "new", DownloadStatus.Completed, bytes: 500, completed: now.AddDays(-1)),
            Job(env, "newer", DownloadStatus.Completed, bytes: 250, completed: now),
            Job(env, "bad", DownloadStatus.Failed),
            Job(env, "running", DownloadStatus.Downloading),
            Job(env, "merging", DownloadStatus.Processing),
            Job(env, "queued", DownloadStatus.Waiting));

        var stats = await env.Get<IDownloadJobQueries>().GetStatisticsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, stats.TotalCompleted);
        Assert.Equal(2, stats.CompletedLast7Days);
        Assert.Equal(1750, stats.BytesDownloaded);
        Assert.Equal(2, stats.ActiveCount);
        Assert.Equal(0.75, stats.SuccessRate);
    }

    [Fact]
    public async Task Statistics_have_no_success_rate_before_anything_finished()
    {
        await using var env = await TestEnvironment.StartAsync();

        var stats = await env.Get<IDownloadJobQueries>().GetStatisticsAsync(TestContext.Current.CancellationToken);

        Assert.Null(stats.SuccessRate);
        Assert.Equal(0, stats.BytesDownloaded);
    }

    [Fact]
    public async Task Clearing_history_removes_finished_jobs_but_keeps_active_ones_and_library_files()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var done = Job(env, "done", DownloadStatus.Completed);
        await SeedAsync(env,
            done,
            Job(env, "failed", DownloadStatus.Failed),
            Job(env, "running", DownloadStatus.Downloading),
            new MediaItem { FilePath = Path.Combine(env.Root, "done.mp4"), DownloadJobId = done.Id });

        var removed = await env.Get<IDownloadJobQueries>().ClearHistoryAsync(ct);

        Assert.Equal(2, removed);
        var active = await env.Get<IDownloadJobQueries>().GetActiveAsync(ct);
        Assert.Equal("running", Assert.Single(active).Title);
        var library = await env.Get<IMediaLibraryService>().QueryAsync(new LibraryQuery(), ct);
        Assert.Null(Assert.Single(library).DownloadJobId);
    }

    [Fact]
    public async Task Library_filters_by_kind_searches_and_sorts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var now = DateTime.UtcNow;
        await SeedAsync(env,
            new MediaItem { FilePath = Path.Combine(env.Root, "a.mp4"), Title = "Alpha", MediaKind = MediaKind.Video, FileSizeBytes = 300, AddedAtUtc = now.AddDays(-3) },
            new MediaItem { FilePath = Path.Combine(env.Root, "b.mp3"), Title = "Bravo", Artist = "Nightcall", MediaKind = MediaKind.Audio, FileSizeBytes = 100, AddedAtUtc = now.AddDays(-1) },
            new MediaItem { FilePath = Path.Combine(env.Root, "c.mp3"), Title = "Charlie", MediaKind = MediaKind.Audio, FileSizeBytes = 200, AddedAtUtc = now.AddDays(-2) });

        var library = env.Get<IMediaLibraryService>();

        var newest = await library.QueryAsync(new LibraryQuery(), ct);
        Assert.Equal(["Bravo", "Charlie", "Alpha"], newest.Select(m => m.Title));

        var audioBySize = await library.QueryAsync(new LibraryQuery(Kind: MediaKind.Audio, Sort: LibrarySort.FileSize, Descending: false), ct);
        Assert.Equal(["Bravo", "Charlie"], audioBySize.Select(m => m.Title));

        var byArtist = await library.QueryAsync(new LibraryQuery(Search: "night"), ct);
        Assert.Equal("Bravo", Assert.Single(byArtist).Title);

        var byTitle = await library.QueryAsync(new LibraryQuery(Sort: LibrarySort.Title, Descending: false), ct);
        Assert.Equal(["Alpha", "Bravo", "Charlie"], byTitle.Select(m => m.Title));
    }

    [Fact]
    public void Disk_usage_is_read_for_an_existing_folder_and_null_for_nonsense()
    {
        var service = new Services.DiskSpaceService();

        var usage = service.GetUsage(Path.GetTempPath());
        Assert.NotNull(usage);
        Assert.True(usage.TotalBytes > 0);
        Assert.InRange(usage.UsedFraction, 0, 1);

        Assert.Null(service.GetUsage(""));
        Assert.Null(service.GetUsage("relative/path"));
    }
}
