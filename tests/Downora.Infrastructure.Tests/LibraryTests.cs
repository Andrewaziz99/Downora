using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Database;
using Downora.Infrastructure.Downloads;
using Downora.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

public sealed class LibraryTests : IAsyncDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly IMediaProbe _probe = Substitute.For<IMediaProbe>();
    private readonly string _folder = Directory.CreateTempSubdirectory("downora-library-").FullName;
    private TestEnvironment? _env;

    public async ValueTask DisposeAsync()
    {
        if (_env is not null)
        {
            await _env.DisposeAsync();
        }

        Directory.Delete(_folder, recursive: true);
    }

    private async Task<MediaLibraryService> ServiceAsync()
    {
        _env = await TestEnvironment.StartAsync();
        var settings = _env.Get<ISettingsService>();
        await settings.LoadAsync(TestContext.Current.CancellationToken);
        return new MediaLibraryService(_env.Get<IDbContextFactory<DownoraDbContext>>(), _probe, settings, _time, NullLogger<MediaLibraryService>.Instance);
    }

    private string File(string name, int bytes = 2048)
    {
        var path = Path.Combine(_folder, name);
        System.IO.File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private static DownloadJob Job(string? path, MediaKind kind = MediaKind.Video, DownloadStatus status = DownloadStatus.Completed) => new()
    {
        SourceUrl = "https://www.youtube.com/watch?v=x",
        NormalizedUrl = "https://www.youtube.com/watch?v=x",
        Platform = SourcePlatform.YouTube,
        Title = "Me at the zoo",
        Uploader = "jawed",
        ThumbnailUrl = "https://i.ytimg.com/vi/x/default.jpg",
        Duration = TimeSpan.FromSeconds(19),
        MediaKind = kind,
        OutputDirectory = @"D:\Media",
        OutputPath = path,
        Status = status,
        CompletedAtUtc = new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc),
        CreatedAtUtc = new DateTime(2026, 11, 9, 7, 0, 0, DateTimeKind.Utc),
    };

    private async Task AddJobsAsync(params DownloadJob[] jobs)
    {
        await using var db = await _env!.CreateDbAsync();
        db.DownloadJobs.AddRange(jobs);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<MediaItem>> ItemsAsync()
    {
        await using var db = await _env!.CreateDbAsync();
        return await db.MediaItems.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_completed_download_becomes_an_entry_with_what_ffprobe_found()
    {
        var library = await ServiceAsync();
        var path = File("zoo.mp4", 4096);
        var job = Job(path);
        await AddJobsAsync(job);
        _probe.ProbeAsync(path, Arg.Any<CancellationToken>()).Returns(new MediaProbe(TimeSpan.FromSeconds(19.2), true, true, 1280, 720, "h264", "aac", 900));

        await library.AddCompletedAsync(job, TestContext.Current.CancellationToken);

        var item = Assert.Single(await ItemsAsync());
        Assert.Equal(path, item.FilePath);
        Assert.Equal(job.Id, item.DownloadJobId);
        Assert.Equal("Me at the zoo", item.Title);
        Assert.Equal("jawed", item.Artist);
        Assert.Equal(MediaKind.Video, item.MediaKind);
        Assert.Equal(TimeSpan.FromSeconds(19.2), item.Duration);
        Assert.Equal(4096, item.FileSizeBytes);
        Assert.Equal((1280, 720, "h264", "aac", 900), (item.Width, item.Height, item.VideoCodec, item.AudioCodec, item.BitrateKbps));
        Assert.Equal("https://i.ytimg.com/vi/x/default.jpg", item.ThumbnailPath);
        Assert.Equal(job.CompletedAtUtc, item.AddedAtUtc);
        Assert.False(item.IsMissing);
    }

    [Fact]
    public async Task An_audio_entry_ignores_the_cover_picture_that_ffprobe_reports_as_video()
    {
        var library = await ServiceAsync();
        var path = File("song.mp3");
        var job = Job(path, MediaKind.Audio);
        await AddJobsAsync(job);
        _probe.ProbeAsync(path, Arg.Any<CancellationToken>()).Returns(new MediaProbe(TimeSpan.FromSeconds(200), true, true, 500, 500, "mjpeg", "mp3", 320));

        await library.AddCompletedAsync(job, TestContext.Current.CancellationToken);

        var item = Assert.Single(await ItemsAsync());
        Assert.Null(item.Width);
        Assert.Null(item.Height);
        Assert.Null(item.VideoCodec);
        Assert.Equal("mp3", item.AudioCodec);
        Assert.Equal(320, item.BitrateKbps);
    }

    [Fact]
    public async Task Without_ffprobe_the_entry_still_has_the_download_s_own_details()
    {
        var library = await ServiceAsync();
        var path = File("zoo.mp4");
        var job = Job(path);
        await AddJobsAsync(job);
        _probe.ProbeAsync(path, Arg.Any<CancellationToken>()).Returns((MediaProbe?)null);

        await library.AddCompletedAsync(job, TestContext.Current.CancellationToken);

        var item = Assert.Single(await ItemsAsync());
        Assert.Equal(TimeSpan.FromSeconds(19), item.Duration);
        Assert.Null(item.Height);
    }

    [Fact]
    public async Task The_same_file_is_never_listed_twice()
    {
        var library = await ServiceAsync();
        var path = File("zoo.mp4");
        var job = Job(path);
        await AddJobsAsync(job);

        await library.AddCompletedAsync(job, TestContext.Current.CancellationToken);
        job.Title = "Renamed";
        await library.AddCompletedAsync(job, TestContext.Current.CancellationToken);

        var item = Assert.Single(await ItemsAsync());
        Assert.Equal("Renamed", item.Title);
    }

    [Fact]
    public async Task Unfinished_jobs_and_jobs_without_a_file_are_not_added()
    {
        var library = await ServiceAsync();
        var path = File("partial.mp4");

        await library.AddCompletedAsync(Job(path, status: DownloadStatus.Downloading), TestContext.Current.CancellationToken);
        await library.AddCompletedAsync(Job(null), TestContext.Current.CancellationToken);
        await library.AddCompletedAsync(Job(Path.Combine(_folder, "gone.mp4")), TestContext.Current.CancellationToken);

        Assert.Empty(await ItemsAsync());
    }

    [Fact]
    public async Task Sync_adds_old_completed_downloads_and_leaves_the_rest_alone()
    {
        var library = await ServiceAsync();
        var old = Job(File("old.mp4"));
        var deleted = Job(Path.Combine(_folder, "deleted.mp4"));
        var failed = Job(null, status: DownloadStatus.Failed);
        await AddJobsAsync(old, deleted, failed);

        var added = await library.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, added);
        Assert.Equal(old.OutputPath, Assert.Single(await ItemsAsync()).FilePath);

        // Running it again adds nothing new.
        Assert.Equal(0, await library.SyncAsync(TestContext.Current.CancellationToken));
        Assert.Single(await ItemsAsync());
    }

    [Fact]
    public async Task A_removed_entry_is_not_brought_back_by_a_later_sync()
    {
        var library = await ServiceAsync();
        var job = Job(File("old.mp4"));
        await AddJobsAsync(job);
        await library.SyncAsync(TestContext.Current.CancellationToken);
        await library.RemoveAsync((await ItemsAsync()).Single().Id, TestContext.Current.CancellationToken);

        await library.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await ItemsAsync());
        Assert.True(_env!.Get<ISettingsService>().Current.LibraryBackfilled);
    }

    [Fact]
    public async Task Sync_marks_files_that_went_missing_and_clears_the_mark_when_they_return()
    {
        var library = await ServiceAsync();
        var path = File("zoo.mp4");
        var job = Job(path);
        await AddJobsAsync(job);
        await library.AddCompletedAsync(job, TestContext.Current.CancellationToken);

        System.IO.File.Delete(path);
        await library.SyncAsync(TestContext.Current.CancellationToken);
        Assert.True(Assert.Single(await ItemsAsync()).IsMissing);

        File("zoo.mp4");
        await library.SyncAsync(TestContext.Current.CancellationToken);
        Assert.False(Assert.Single(await ItemsAsync()).IsMissing);
    }

    [Fact]
    public async Task Sync_never_throws_even_when_ffprobe_does()
    {
        var library = await ServiceAsync();
        await AddJobsAsync(Job(File("zoo.mp4")));
        _probe.ProbeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<Task<MediaProbe?>>(_ => throw new InvalidOperationException("boom"));

        var added = await library.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, added);
    }

    [Fact]
    public async Task Removing_an_entry_keeps_the_file_and_the_download_history()
    {
        var library = await ServiceAsync();
        var path = File("zoo.mp4");
        var job = Job(path);
        await AddJobsAsync(job);
        await library.AddCompletedAsync(job, TestContext.Current.CancellationToken);
        var id = (await ItemsAsync()).Single().Id;

        Assert.True(await library.RemoveAsync(id, TestContext.Current.CancellationToken));
        Assert.False(await library.RemoveAsync(id, TestContext.Current.CancellationToken));

        Assert.Empty(await ItemsAsync());
        Assert.True(System.IO.File.Exists(path));
        await using var db = await _env!.CreateDbAsync();
        Assert.Single(await db.DownloadJobs.ToListAsync(TestContext.Current.CancellationToken));
    }
}

public sealed class LibraryIndexerTests
{
    private readonly IDownloadJobService _downloads = Substitute.For<IDownloadJobService>();
    private readonly IMediaLibraryService _library = Substitute.For<IMediaLibraryService>();

    private async Task<LibraryIndexer> StartedAsync()
    {
        var indexer = new LibraryIndexer(_downloads, _library, NullLogger<LibraryIndexer>.Instance);
        await indexer.StartAsync(TestContext.Current.CancellationToken);
        return indexer;
    }

    private void Raise(DownloadStatus status) =>
        _downloads.JobUpdated += NSubstitute.Raise.Event<EventHandler<DownloadJobUpdate>>(
            _downloads,
            new DownloadJobUpdate(new DownloadJob { SourceUrl = "https://e.com/a", NormalizedUrl = "https://e.com/a", OutputDirectory = "x", Status = status }, null));

    [Fact]
    public async Task A_job_that_completes_is_added_to_the_library()
    {
        await StartedAsync();

        Raise(DownloadStatus.Completed);

        await _library.Received(1).AddCompletedAsync(Arg.Is<DownloadJob>(j => j.Status == DownloadStatus.Completed), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DownloadStatus.Downloading)]
    [InlineData(DownloadStatus.Failed)]
    [InlineData(DownloadStatus.Cancelled)]
    [InlineData(DownloadStatus.Paused)]
    public async Task Other_updates_do_not_touch_the_library(DownloadStatus status)
    {
        await StartedAsync();

        Raise(status);

        await _library.DidNotReceiveWithAnyArgs().AddCompletedAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_library_failure_is_absorbed_and_stopping_stops_listening()
    {
        var indexer = await StartedAsync();
        _library.AddCompletedAsync(Arg.Any<DownloadJob>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new IOException("locked"));

        Raise(DownloadStatus.Completed); // must not throw

        await indexer.StopAsync(TestContext.Current.CancellationToken);
        _library.ClearReceivedCalls();
        Raise(DownloadStatus.Completed);
        await _library.DidNotReceiveWithAnyArgs().AddCompletedAsync(default!, TestContext.Current.CancellationToken);
    }
}

public class FfprobeDetailsTests
{
    [Fact]
    public void Size_codecs_and_bitrate_come_from_the_first_real_streams()
    {
        const string json = """
            {"streams":[
              {"codec_name":"h264","codec_type":"video","width":1920,"height":1080,"disposition":{"attached_pic":0}},
              {"codec_name":"aac","codec_type":"audio"}],
             "format":{"duration":"61.5","bit_rate":"2500000"}}
            """;

        var probe = FfprobeFileVerifier.Parse(json)!;

        Assert.Equal((1920, 1080, "h264", "aac", 2500), (probe.Width, probe.Height, probe.VideoCodec, probe.AudioCodec, probe.BitrateKbps));
        Assert.Equal(TimeSpan.FromSeconds(61.5), probe.Duration);
    }

    [Fact]
    public void An_attached_cover_is_a_video_stream_for_the_check_but_not_for_the_details()
    {
        const string json = """
            {"streams":[
              {"codec_name":"mp3","codec_type":"audio"},
              {"codec_name":"mjpeg","codec_type":"video","width":500,"height":500,"disposition":{"attached_pic":1}}],
             "format":{"duration":"200.0","bit_rate":"320000"}}
            """;

        var probe = FfprobeFileVerifier.Parse(json)!;

        Assert.True(probe.HasVideo);
        Assert.Null(probe.VideoCodec);
        Assert.Null(probe.Width);
        Assert.Equal("mp3", probe.AudioCodec);
        Assert.Equal(320, probe.BitrateKbps);
    }

    [Fact]
    public void Missing_details_are_simply_unknown()
    {
        var probe = FfprobeFileVerifier.Parse("""{"streams":[{"codec_type":"audio"}],"format":{"duration":"3.0"}}""")!;

        Assert.Null(probe.BitrateKbps);
        Assert.Null(probe.AudioCodec);
        Assert.Null(probe.Height);
    }
}
