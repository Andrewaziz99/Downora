using System.Collections.Concurrent;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Database;
using Downora.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Downora.Infrastructure.Tests;

/// <summary>An engine the test controls: it decides what each download reports and how it ends.</summary>
internal sealed class FakeEngine : IDownloadEngine
{
    public Func<DownloadRequest, IProgress<DownloadProgress>?, CancellationToken, Task<DownloadResult>> OnDownload { get; set; } =
        (_, _, _) => Task.FromResult(DownloadResult.Success(@"C:\Downloads\a.mp4", 1000));

    public ConcurrentQueue<DownloadRequest> Requests { get; } = new();

    public ConcurrentQueue<DownloadRequest> Discarded { get; } = new();

    /// <summary>Completes once a download has started.</summary>
    public TaskCompletionSource Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);

        // Signal after the download's first synchronous steps (its progress reports) have run, so a test that
        // waits for "started" sees their effects instead of racing them.
        var download = OnDownload(request, progress, cancellationToken);
        Started.TrySetResult();
        return download;
    }

    public Task DiscardPartialFilesAsync(DownloadRequest request)
    {
        Discarded.Enqueue(request);
        return Task.CompletedTask;
    }

    /// <summary>A download that runs until it is cancelled.</summary>
    public void BlockUntilCancelled() =>
        OnDownload = async (_, progress, token) =>
        {
            progress?.Report(new DownloadProgress(DownloadStage.Downloading, 10, 100, 1000, 50, TimeSpan.FromSeconds(5)));
            await Task.Delay(Timeout.Infinite, token);
            return DownloadResult.Success("unreachable", 0);
        };
}

public sealed class DownloadJobServiceTests : IAsyncDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeEngine _engine = new();
    private readonly ConcurrentQueue<DownloadJobUpdate> _updates = new();
    private TestEnvironment? _env;
    private DownloadJobService? _service;

    public async ValueTask DisposeAsync()
    {
        if (_env is not null)
        {
            await _env.DisposeAsync();
        }
    }

    private async Task<DownloadJobService> StartAsync(Action<AppSettings>? settings = null)
    {
        _env = await TestEnvironment.StartAsync();
        var settingsService = _env.Get<ISettingsService>();
        await settingsService.LoadAsync(TestContext.Current.CancellationToken);
        if (settings is not null)
        {
            var current = settingsService.Current;
            settings(current);
            await settingsService.SaveAsync(current, TestContext.Current.CancellationToken);
        }

        _service = new DownloadJobService(
            _env.Get<IDbContextFactory<DownoraDbContext>>(), _engine, settingsService, _time, NullLogger<DownloadJobService>.Instance);
        _service.JobUpdated += (_, update) => _updates.Enqueue(update);
        return _service;
    }

    private static NewDownload Video(string id = "abc", QualityPreset quality = QualityPreset.P1080) => new()
    {
        SourceUrl = $"https://www.youtube.com/watch?v={id}",
        NormalizedUrl = $"https://youtube.com/watch?v={id}",
        Platform = SourcePlatform.YouTube,
        Title = "A video",
        Uploader = "Someone",
        ThumbnailUrl = "https://i.ytimg.com/vi/x/hq.jpg",
        Duration = TimeSpan.FromMinutes(3),
        Kind = MediaKind.Video,
        Quality = quality,
        Container = VideoContainer.Mp4,
        EstimatedBytes = 5000,
    };

    private static NewDownload Audio(string id = "abc", AudioFormat format = AudioFormat.Mp3) =>
        Video(id) with { Kind = MediaKind.Audio, AudioFormat = format, AudioBitrateKbps = 192 };

    private async Task<DownloadJob> LoadAsync(Guid id)
    {
        await using var db = await _env!.CreateDbAsync();
        return await db.DownloadJobs.AsNoTracking().SingleAsync(j => j.Id == id, TestContext.Current.CancellationToken);
    }

    // ---- Creating and completing ----

    [Fact]
    public async Task Starting_a_download_saves_a_queued_job_with_the_chosen_options()
    {
        var service = await StartAsync(s => s.DownloadDirectory = @"D:\Media");
        _engine.BlockUntilCancelled();

        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await _engine.Started.Task;

        Assert.False(started.WasAlreadyActive);
        var job = await LoadAsync(started.Job.Id);
        Assert.Equal(DownloadStatus.Downloading, job.Status);
        Assert.Equal(10, job.ProgressPercent);
        Assert.Equal("A video", job.Title);
        Assert.Equal(QualityPreset.P1080, job.QualityPreset);
        Assert.Equal(MediaKind.Video, job.MediaKind);
        Assert.Equal(VideoContainer.Mp4, job.Container);
        Assert.Null(job.AudioFormat);
        Assert.Equal(@"D:\Media", job.OutputDirectory);
        Assert.Equal(1000, job.TotalBytes); // the real size reported by the download replaces the analysis' estimate (5000)
        Assert.Equal(_time.GetUtcNow().UtcDateTime, job.CreatedAtUtc);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, job.StartedAtUtc);

        await service.CancelAsync(job.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_engine_gets_the_job_the_output_folder_and_the_chosen_format()
    {
        var service = await StartAsync(s => s.DownloadDirectory = @"D:\Media");

        var started = await service.StartAsync(Audio(format: AudioFormat.Opus), TestContext.Current.CancellationToken);
        await service.WaitForAsync(started.Job.Id);

        var request = Assert.Single(_engine.Requests);
        Assert.Equal(started.Job.Id, request.JobId);
        Assert.Equal("https://www.youtube.com/watch?v=abc", request.Url);
        Assert.Equal(MediaKind.Audio, request.Kind);
        Assert.Equal(AudioFormat.Opus, request.AudioFormat);
        Assert.Equal(192, request.AudioBitrateKbps);
        Assert.Equal(@"D:\Media", request.OutputDirectory);
        Assert.Equal("A video", request.Title);
        Assert.Null(request.CookieBrowser);
    }

    [Fact]
    public async Task A_finished_download_is_completed_with_its_file_and_counts_in_history_and_statistics()
    {
        var service = await StartAsync();
        _engine.OnDownload = (_, progress, _) =>
        {
            progress!.Report(new DownloadProgress(DownloadStage.Starting, 0, 0, null, null, null));
            progress.Report(new DownloadProgress(DownloadStage.Downloading, 40, 2000, 5000, 100, TimeSpan.FromSeconds(30)));
            progress.Report(new DownloadProgress(DownloadStage.Merging, 99, 5000, 5000, null, null));
            _time.Advance(TimeSpan.FromSeconds(42));
            return Task.FromResult(DownloadResult.Success(@"C:\Downloads\A video.mp4", 4800));
        };

        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(started.Job.Id);

        var job = await LoadAsync(started.Job.Id);
        Assert.Equal(DownloadStatus.Completed, job.Status);
        Assert.Equal(@"C:\Downloads\A video.mp4", job.OutputPath);
        Assert.Equal(100, job.ProgressPercent);
        Assert.Equal(4800, job.DownloadedBytes);
        Assert.Equal(4800, job.TotalBytes);
        Assert.Equal(DownloadErrorKind.None, job.ErrorKind);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, job.CompletedAtUtc);
        Assert.True(job.IsFinished);

        var queries = _env!.Get<IDownloadJobQueries>();
        Assert.Empty(await queries.GetActiveAsync(TestContext.Current.CancellationToken));
        var history = Assert.Single(await queries.GetHistoryAsync(new HistoryQuery(), TestContext.Current.CancellationToken));
        Assert.Equal(job.Id, history.Id);
        var stats = await queries.GetStatisticsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, stats.TotalCompleted);
        Assert.Equal(4800, stats.BytesDownloaded);
        Assert.Equal(0, stats.ActiveCount);
        Assert.Equal(1.0, stats.SuccessRate);
    }

    [Fact]
    public async Task Screens_see_every_state_in_order_ending_with_the_final_one()
    {
        var service = await StartAsync();
        _engine.OnDownload = (_, progress, _) =>
        {
            progress!.Report(new DownloadProgress(DownloadStage.Starting, 0, 0, null, null, null));
            progress.Report(new DownloadProgress(DownloadStage.Downloading, 40, 2000, 5000, 100, TimeSpan.FromSeconds(30)));
            progress.Report(new DownloadProgress(DownloadStage.Merging, 99, 5000, 5000, null, null));
            return Task.FromResult(DownloadResult.Success(@"C:\Downloads\a.mp4", 4800));
        };

        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(started.Job.Id);

        var statuses = _updates.Select(u => u.Job.Status).Aggregate(new List<DownloadStatus>(), (list, s) =>
        {
            if (list.Count == 0 || list[^1] != s)
            {
                list.Add(s);
            }

            return list;
        });
        Assert.Equal(
            [DownloadStatus.Waiting, DownloadStatus.Extracting, DownloadStatus.Downloading, DownloadStatus.Processing, DownloadStatus.Completed],
            statuses);
        Assert.Equal(DownloadStatus.Completed, _updates.Last().Job.Status);
        Assert.Contains(_updates, u => u.Progress is { Stage: DownloadStage.Downloading, BytesPerSecond: 100 });
    }

    [Fact]
    public async Task Progress_is_saved_and_announced_no_more_often_than_needed()
    {
        var service = await StartAsync();
        var release = new TaskCompletionSource();
        _engine.OnDownload = async (_, progress, _) =>
        {
            // 100 reports at the same instant: only the first and the status change should get through.
            for (var i = 1; i <= 100; i++)
            {
                progress!.Report(new DownloadProgress(DownloadStage.Downloading, i / 2.0, i * 10, 5000, 100, null));
            }

            await release.Task;
            return DownloadResult.Success(@"C:\a.mp4", 1);
        };

        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await _engine.Started.Task;

        var downloading = _updates.Where(u => u.Job.Status == DownloadStatus.Downloading).ToList();
        Assert.InRange(downloading.Count, 1, 3);

        // Once enough time has passed, the next report is saved and shown.
        _time.Advance(DownloadJobService.SaveInterval + TimeSpan.FromMilliseconds(1));
        release.SetResult();
        await service.WaitForAsync(started.Job.Id);
        Assert.Equal(DownloadStatus.Completed, (await LoadAsync(started.Job.Id)).Status);
    }

    [Fact]
    public async Task The_saved_percentage_follows_the_download_and_never_goes_back()
    {
        var service = await StartAsync();
        var gate = new TaskCompletionSource();
        _engine.OnDownload = async (_, progress, _) =>
        {
            progress!.Report(new DownloadProgress(DownloadStage.Downloading, 30, 1500, 5000, 100, null));
            _time.Advance(TimeSpan.FromSeconds(3));
            progress.Report(new DownloadProgress(DownloadStage.Downloading, 60, 3000, 5000, 100, null));
            _time.Advance(TimeSpan.FromSeconds(3));
            progress.Report(new DownloadProgress(DownloadStage.Downloading, 20, 1000, 5000, 100, null)); // out of order
            await gate.Task;
            return DownloadResult.Failure(DownloadErrorKind.Network, "dropped");
        };

        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await _engine.Started.Task;

        var saved = await LoadAsync(started.Job.Id);
        Assert.Equal(60, saved.ProgressPercent);
        Assert.Equal(3000, saved.DownloadedBytes);
        gate.SetResult();
        await service.WaitForAsync(started.Job.Id);
    }

    // ---- Failing ----

    [Fact]
    public async Task A_classified_failure_is_saved_with_its_kind_and_detail()
    {
        var service = await StartAsync();
        _engine.OnDownload = (_, _, _) => Task.FromResult(DownloadResult.Failure(DownloadErrorKind.RequiresSignIn, "Sign in to confirm you're not a bot"));

        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(started.Job.Id);

        var job = await LoadAsync(started.Job.Id);
        Assert.Equal(DownloadStatus.Failed, job.Status);
        Assert.Equal(DownloadErrorKind.RequiresSignIn, job.ErrorKind);
        Assert.Equal("Sign in to confirm you're not a bot", job.ErrorMessage);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Empty(_engine.Discarded); // a failure keeps its partial files so a retry can resume

        var stats = await _env!.Get<IDownloadJobQueries>().GetStatisticsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0.0, stats.SuccessRate);
    }

    [Fact]
    public async Task An_unexpected_exception_fails_that_job_and_leaves_the_service_working()
    {
        var service = await StartAsync();
        _engine.OnDownload = (_, _, _) => throw new InvalidOperationException("engine exploded");

        var broken = await service.StartAsync(Video("one"), TestContext.Current.CancellationToken);
        await service.WaitForAsync(broken.Job.Id);

        var job = await LoadAsync(broken.Job.Id);
        Assert.Equal(DownloadStatus.Failed, job.Status);
        Assert.Equal(DownloadErrorKind.Unknown, job.ErrorKind);
        Assert.Equal("engine exploded", job.ErrorMessage);

        _engine.OnDownload = (_, _, _) => Task.FromResult(DownloadResult.Success(@"C:\b.mp4", 5));
        var next = await service.StartAsync(Video("two"), TestContext.Current.CancellationToken);
        await service.WaitForAsync(next.Job.Id);
        Assert.Equal(DownloadStatus.Completed, (await LoadAsync(next.Job.Id)).Status);
    }

    [Fact]
    public async Task A_screen_that_throws_does_not_stop_the_download()
    {
        var service = await StartAsync();
        service.JobUpdated += (_, _) => throw new InvalidOperationException("bad screen");

        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(started.Job.Id);

        Assert.Equal(DownloadStatus.Completed, (await LoadAsync(started.Job.Id)).Status);
    }

    // ---- Cancelling ----

    [Fact]
    public async Task Cancelling_a_running_download_ends_it_as_cancelled_and_discards_its_partial_files()
    {
        var service = await StartAsync();
        _engine.BlockUntilCancelled();
        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await _engine.Started.Task;

        var cancelled = await service.CancelAsync(started.Job.Id, TestContext.Current.CancellationToken);

        Assert.True(cancelled);
        var job = await LoadAsync(started.Job.Id);
        Assert.Equal(DownloadStatus.Cancelled, job.Status);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Equal(started.Job.Id, Assert.Single(_engine.Discarded).JobId);
        Assert.Equal(DownloadStatus.Cancelled, _updates.Last().Job.Status);
    }

    [Fact]
    public async Task A_paused_job_left_by_a_restart_can_be_cancelled_too()
    {
        var service = await StartAsync();
        Guid id;
        await using (var db = await _env!.CreateDbAsync())
        {
            var job = new DownloadJob
            {
                SourceUrl = "https://example.com/v",
                NormalizedUrl = "https://example.com/v",
                OutputDirectory = @"D:\Media",
                Status = DownloadStatus.Paused,
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
            };
            db.DownloadJobs.Add(job);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            id = job.Id;
        }

        Assert.True(await service.CancelAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(DownloadStatus.Cancelled, (await LoadAsync(id)).Status);
        Assert.Equal(id, Assert.Single(_engine.Discarded).JobId);
    }

    [Fact]
    public async Task Cancelling_something_that_is_not_active_reports_false()
    {
        var service = await StartAsync();
        var done = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(done.Job.Id);

        Assert.False(await service.CancelAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.False(await service.CancelAsync(done.Job.Id, TestContext.Current.CancellationToken));
        Assert.Equal(DownloadStatus.Completed, (await LoadAsync(done.Job.Id)).Status);
    }

    // ---- Closing the app ----

    [Fact]
    public async Task Closing_the_app_pauses_running_downloads_and_keeps_their_files()
    {
        var service = await StartAsync();
        _engine.BlockUntilCancelled();
        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await _engine.Started.Task;

        await service.StopAsync(TestContext.Current.CancellationToken);

        var job = await LoadAsync(started.Job.Id);
        Assert.Equal(DownloadStatus.Paused, job.Status);
        Assert.Null(job.CompletedAtUtc);
        Assert.Empty(_engine.Discarded);
    }

    // ---- Duplicates ----

    [Fact]
    public async Task The_same_item_at_the_same_quality_is_not_started_twice()
    {
        var service = await StartAsync();
        _engine.BlockUntilCancelled();
        var first = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await _engine.Started.Task;

        var again = await service.StartAsync(Video(), TestContext.Current.CancellationToken);

        Assert.True(again.WasAlreadyActive);
        Assert.Equal(first.Job.Id, again.Job.Id);
        Assert.Single(_engine.Requests);

        await service.CancelAsync(first.Job.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_different_quality_or_kind_is_a_separate_download()
    {
        var service = await StartAsync();
        _engine.BlockUntilCancelled();

        var a = await service.StartAsync(Video(quality: QualityPreset.P1080), TestContext.Current.CancellationToken);
        var b = await service.StartAsync(Video(quality: QualityPreset.P720), TestContext.Current.CancellationToken);
        var c = await service.StartAsync(Audio(format: AudioFormat.Mp3), TestContext.Current.CancellationToken);
        var d = await service.StartAsync(Audio(format: AudioFormat.M4a), TestContext.Current.CancellationToken);

        Assert.Equal(4, new[] { a.Job.Id, b.Job.Id, c.Job.Id, d.Job.Id }.Distinct().Count());
        Assert.All(new[] { a, b, c, d }, r => Assert.False(r.WasAlreadyActive));

        foreach (var id in new[] { a.Job.Id, b.Job.Id, c.Job.Id, d.Job.Id })
        {
            await service.CancelAsync(id, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_finished_download_can_be_downloaded_again()
    {
        var service = await StartAsync();
        var first = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(first.Job.Id);

        var second = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(second.Job.Id);

        Assert.False(second.WasAlreadyActive);
        Assert.NotEqual(first.Job.Id, second.Job.Id);
    }

    // ---- Settings ----

    [Fact]
    public async Task Browser_sign_in_reaches_the_engine_only_when_the_user_turned_it_on()
    {
        var off = await StartAsync();
        var first = await off.StartAsync(Video("one"), TestContext.Current.CancellationToken);
        await off.WaitForAsync(first.Job.Id);
        Assert.Null(_engine.Requests.Single().CookieBrowser);
        await _env!.DisposeAsync();
        _env = null;

        var on = await StartAsync(s =>
        {
            s.UseBrowserSession = true;
            s.BrowserForCookies = "firefox";
        });
        var second = await on.StartAsync(Video("two"), TestContext.Current.CancellationToken);
        await on.WaitForAsync(second.Job.Id);

        Assert.Equal("firefox", _engine.Requests.Last().CookieBrowser);
    }

    [Fact]
    public async Task A_job_survives_a_restart_in_its_last_saved_state()
    {
        var service = await StartAsync();
        _engine.OnDownload = (_, _, _) => Task.FromResult(DownloadResult.Failure(DownloadErrorKind.GeoRestricted, "not in your country"));
        var started = await service.StartAsync(Video(), TestContext.Current.CancellationToken);
        await service.WaitForAsync(started.Job.Id);

        _env = await _env!.RestartAsync();

        var job = await LoadAsync(started.Job.Id);
        Assert.Equal(DownloadStatus.Failed, job.Status);
        Assert.Equal(DownloadErrorKind.GeoRestricted, job.ErrorKind);
    }
}
