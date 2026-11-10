using System.Collections.Concurrent;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Database;
using Downora.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Downora.Infrastructure.Tests;

/// <summary>The queue: how many downloads run at once, in what order, and pause, resume, cancel and retry.</summary>
public sealed class DownloadQueueTests : IAsyncDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeEngine _engine = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DownloadResult>> _gates = new();
    private readonly ConcurrentQueue<DownloadJobUpdate> _updates = new();
    private TestEnvironment? _env;
    private DownloadJobService? _service;

    public async ValueTask DisposeAsync()
    {
        // Let anything still "downloading" end, so the database can be deleted.
        foreach (var gate in _gates.Values)
        {
            gate.TrySetResult(DownloadResult.Failure(DownloadErrorKind.Unknown, "test over"));
        }

        if (_service is not null)
        {
            await ((IHostedService)_service).StopAsync(CancellationToken.None);
            _service.Dispose();
        }

        if (_env is not null)
        {
            await _env.DisposeAsync();
        }
    }

    private async Task<DownloadJobService> StartAsync(int max = 2, bool autoStart = true)
    {
        _env = await TestEnvironment.StartAsync();
        var settings = _env.Get<ISettingsService>();
        await settings.LoadAsync(TestContext.Current.CancellationToken);
        var current = settings.Current;
        current.MaxConcurrentDownloads = max;
        current.AutoStartDownloads = autoStart;
        await settings.SaveAsync(current, TestContext.Current.CancellationToken);

        // Every download waits at its own gate until the test lets it finish.
        _engine.OnDownload = (request, progress, token) =>
        {
            progress?.Report(new DownloadProgress(DownloadStage.Downloading, 10, 100, 1000, 50, null));
            var gate = Gate(request.Url);
            token.Register(() => gate.TrySetCanceled(token));
            return gate.Task;
        };

        _service = new DownloadJobService(
            _env.Get<IDbContextFactory<DownoraDbContext>>(), _engine, settings, _time, NullLogger<DownloadJobService>.Instance);
        _service.JobUpdated += (_, update) => _updates.Enqueue(update);
        await ((IHostedService)_service).StartAsync(TestContext.Current.CancellationToken);
        await _service.StartQueueAsync(TestContext.Current.CancellationToken);
        return _service;
    }

    private TaskCompletionSource<DownloadResult> Gate(string url) =>
        _gates.GetOrAdd(url, _ => new TaskCompletionSource<DownloadResult>(TaskCreationOptions.RunContinuationsAsynchronously));

    private static string Url(string id) => $"https://www.youtube.com/watch?v={id}";

    private static NewDownload Video(string id) => new()
    {
        SourceUrl = Url(id),
        NormalizedUrl = Url(id),
        Platform = SourcePlatform.YouTube,
        Title = "Video " + id,
        Quality = QualityPreset.P720,
    };

    private void Finish(string id) => Gate(Url(id)).TrySetResult(DownloadResult.Success(@"C:\Downloads\" + id + ".mp4", 100));

    private void Fail(string id) => Gate(Url(id)).TrySetResult(DownloadResult.Failure(DownloadErrorKind.Network, "dropped"));

    private async Task<DownloadJob> LoadAsync(Guid id)
    {
        await using var db = await _env!.CreateDbAsync();
        return await db.DownloadJobs.AsNoTracking().SingleAsync(j => j.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<DownloadStatus> StatusAsync(Guid id) => (await LoadAsync(id)).Status;

    /// <summary>Waits (briefly) for a background condition, such as the queue starting the next download.</summary>
    private static async Task UntilAsync(Func<bool> condition, string what)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "Timed out waiting for: " + what);
    }

    private async Task<DownloadJob> AddAsync(DownloadJobService service, string id)
    {
        _time.Advance(TimeSpan.FromSeconds(1));
        return (await service.StartAsync(Video(id), TestContext.Current.CancellationToken)).Job;
    }

    /// <summary>Waits until the job has been saved and announced as downloading (the engine's first report).</summary>
    private Task DownloadingAsync(Guid id) =>
        UntilAsync(() => _updates.Any(u => u.Job.Id == id && u.Job.Status == DownloadStatus.Downloading && u.Progress is not null), "downloading " + id);

    private int Started(string id) => _engine.Requests.Count(r => r.Url == Url(id));

    // ---- Concurrency ----

    [Fact]
    public async Task No_more_downloads_run_at_once_than_the_setting_allows_and_the_rest_wait_in_order()
    {
        var service = await StartAsync(max: 2);

        var a = await AddAsync(service, "a");
        var b = await AddAsync(service, "b");
        var c = await AddAsync(service, "c");
        var d = await AddAsync(service, "d");
        await UntilAsync(() => _engine.Requests.Count == 2, "two downloads started");

        await DownloadingAsync(a.Id);
        await DownloadingAsync(b.Id);
        Assert.Equal(2, Started("a") + Started("b"));
        Assert.Equal(0, Started("c") + Started("d"));
        Assert.Equal(DownloadStatus.Downloading, await StatusAsync(a.Id));
        Assert.Equal(DownloadStatus.Downloading, await StatusAsync(b.Id));
        Assert.Equal(DownloadStatus.Waiting, await StatusAsync(c.Id));
        Assert.Equal(DownloadStatus.Waiting, await StatusAsync(d.Id));

        // A finishing download frees a slot for the oldest waiting one.
        Finish("a");
        await UntilAsync(() => Started("c") == 1, "c started");
        Assert.Equal(0, Started("d"));

        Finish("b");
        await UntilAsync(() => Started("d") == 1, "d started");
        Finish("c");
        Finish("d");
        await UntilAsync(() => _service!.WaitForAsync(d.Id).IsCompleted, "queue drained");
        // a and b start side by side, so their order is not fixed; c always starts before d.
        var started = _engine.Requests.Select(r => r.Title![^1..]).ToList();
        Assert.Equal(["a", "b"], started.Take(2).Order());
        Assert.Equal(["c", "d"], started.Skip(2));
        Assert.Equal(DownloadStatus.Completed, await StatusAsync(d.Id));
    }

    [Fact]
    public async Task A_limit_of_one_runs_downloads_strictly_one_after_another()
    {
        var service = await StartAsync(max: 1);
        await AddAsync(service, "a");
        await AddAsync(service, "b");
        await UntilAsync(() => Started("a") == 1, "a started");
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, Started("b"));

        Fail("a");
        await UntilAsync(() => Started("b") == 1, "b started after a ended, even though a failed");
    }

    [Fact]
    public async Task Raising_the_limit_starts_waiting_downloads_at_once_and_lowering_it_never_stops_running_ones()
    {
        var service = await StartAsync(max: 1);
        var a = await AddAsync(service, "a");
        var b = await AddAsync(service, "b");
        var c = await AddAsync(service, "c");
        await UntilAsync(() => Started("a") == 1, "a started");

        var settings = _env!.Get<ISettingsService>();
        var raised = settings.Current;
        raised.MaxConcurrentDownloads = 3;
        await settings.SaveAsync(raised, TestContext.Current.CancellationToken);
        await UntilAsync(() => Started("b") == 1 && Started("c") == 1, "b and c started");
        await DownloadingAsync(a.Id);
        await DownloadingAsync(b.Id);
        await DownloadingAsync(c.Id);

        var lowered = settings.Current;
        lowered.MaxConcurrentDownloads = 1;
        await settings.SaveAsync(lowered, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(DownloadStatus.Downloading, await StatusAsync(a.Id));
        Assert.Equal(DownloadStatus.Downloading, await StatusAsync(b.Id));
        Assert.Equal(DownloadStatus.Downloading, await StatusAsync(c.Id));
    }

    [Fact]
    public async Task Jobs_left_waiting_by_the_last_session_start_when_the_app_does()
    {
        _env = await TestEnvironment.StartAsync();
        Guid id;
        await using (var db = await _env.CreateDbAsync())
        {
            var job = new DownloadJob
            {
                SourceUrl = Url("old"), NormalizedUrl = Url("old"), OutputDirectory = @"D:\Media", Title = "old",
                Status = DownloadStatus.Waiting, CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
            };
            db.DownloadJobs.Add(job);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            id = job.Id;
        }

        var settings = _env.Get<ISettingsService>();
        await settings.LoadAsync(TestContext.Current.CancellationToken);
        _engine.OnDownload = (_, _, _) => Task.FromResult(DownloadResult.Success(@"C:\old.mp4", 5));
        _service = new DownloadJobService(_env.Get<IDbContextFactory<DownoraDbContext>>(), _engine, settings, _time, NullLogger<DownloadJobService>.Instance);

        await ((IHostedService)_service).StartAsync(TestContext.Current.CancellationToken);
        Assert.Empty(_engine.Requests); // starting the host alone runs nothing: settings may not be loaded yet
        await _service.StartQueueAsync(TestContext.Current.CancellationToken);
        await UntilAsync(() => _engine.Requests.Count == 1, "the waiting job started");
        await _service.WaitForAsync(id);

        Assert.Equal(DownloadStatus.Completed, await StatusAsync(id));
    }

    [Fact]
    public async Task Starting_the_host_before_settings_and_the_database_are_ready_does_not_touch_either()
    {
        // The app starts its host first and loads settings afterwards; this order once crashed startup.
        _env = await TestEnvironment.StartAsync();
        var settings = _env.Get<ISettingsService>(); // deliberately not loaded
        _service = new DownloadJobService(_env.Get<IDbContextFactory<DownoraDbContext>>(), _engine, settings, _time, NullLogger<DownloadJobService>.Instance);

        await ((IHostedService)_service).StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_engine.Requests);
    }

    [Fact]
    public async Task The_engine_is_told_the_tag_settings_and_the_length_to_check_against()
    {
        var service = await StartAsync();
        var settings = _env!.Get<ISettingsService>();
        var current = settings.Current;
        current.EmbedMetadata = false;
        current.EmbedCoverArt = true;
        await settings.SaveAsync(current, TestContext.Current.CancellationToken);

        await service.StartAsync(Video("tags") with { Duration = TimeSpan.FromSeconds(42) }, TestContext.Current.CancellationToken);
        await UntilAsync(() => _engine.Requests.Count == 1, "the download started");

        var request = _engine.Requests.Single();
        Assert.False(request.EmbedMetadata);
        Assert.True(request.EmbedCoverArt);
        Assert.Equal(TimeSpan.FromSeconds(42), request.ExpectedDuration);
    }

    // ---- Pause and resume ----

    [Fact]
    public async Task Pausing_a_running_download_keeps_its_files_frees_the_slot_and_resuming_continues_it()
    {
        var service = await StartAsync(max: 1);
        var a = await AddAsync(service, "a");
        var b = await AddAsync(service, "b");
        await UntilAsync(() => Started("a") == 1, "a started");

        Assert.True(await service.PauseAsync(a.Id, TestContext.Current.CancellationToken));

        var paused = await LoadAsync(a.Id);
        Assert.Equal(DownloadStatus.Paused, paused.Status);
        Assert.Equal(10, paused.ProgressPercent);
        Assert.Null(paused.CompletedAtUtc);
        Assert.Empty(_engine.Discarded); // paused, not cancelled: nothing is deleted
        await UntilAsync(() => Started("b") == 1, "b took the free slot");

        // Resuming puts it back in line behind b (limit 1), and it continues with the same job and folder.
        Assert.True(await service.ResumeAsync(a.Id, TestContext.Current.CancellationToken));
        Assert.Equal(DownloadStatus.Waiting, await StatusAsync(a.Id));
        _gates.TryRemove(Url("a"), out _); // the old gate is spent; the new run needs a fresh one
        Finish("b");
        await UntilAsync(() => Started("a") == 2, "a ran again");
        Assert.Equal(2, _engine.Requests.Count(r => r.JobId == a.Id));
        Finish("a");
        await service.WaitForAsync(a.Id);
        Assert.Equal(DownloadStatus.Completed, await StatusAsync(a.Id));
        Assert.Equal(DownloadStatus.Completed, await StatusAsync(b.Id));
    }

    [Fact]
    public async Task A_waiting_job_can_be_paused_and_is_then_skipped_by_the_queue()
    {
        var service = await StartAsync(max: 1);
        await AddAsync(service, "a");
        var b = await AddAsync(service, "b");
        var c = await AddAsync(service, "c");
        await UntilAsync(() => Started("a") == 1, "a started");

        Assert.True(await service.PauseAsync(b.Id, TestContext.Current.CancellationToken));
        Assert.Equal(DownloadStatus.Paused, await StatusAsync(b.Id));

        Finish("a");
        await UntilAsync(() => Started("c") == 1, "c started");
        Assert.Equal(0, Started("b"));
        Assert.Equal(DownloadStatus.Paused, await StatusAsync(b.Id));
        Assert.NotEqual(Guid.Empty, c.Id);
    }

    [Fact]
    public async Task Pausing_stops_working_for_a_job_that_is_not_pausable()
    {
        var service = await StartAsync(max: 1);
        var done = await AddAsync(service, "done");
        await UntilAsync(() => Started("done") == 1, "started");
        Finish("done");
        await service.WaitForAsync(done.Id);

        Assert.False(await service.PauseAsync(done.Id, TestContext.Current.CancellationToken));
        Assert.False(await service.PauseAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal(DownloadStatus.Completed, await StatusAsync(done.Id));
    }

    [Fact]
    public async Task A_download_that_is_being_joined_by_ffmpeg_is_left_to_finish()
    {
        var service = await StartAsync(max: 1);
        _engine.OnDownload = (_, progress, token) =>
        {
            progress!.Report(new DownloadProgress(DownloadStage.Merging, 99, 1000, 1000, null, null));
            var gate = Gate("merge");
            token.Register(() => gate.TrySetCanceled(token));
            return gate.Task;
        };
        var job = await AddAsync(service, "m");
        await UntilAsync(() => _updates.Any(u => u.Job.Id == job.Id && u.Job.Status == DownloadStatus.Processing), "joining started");

        Assert.False(await service.PauseAsync(job.Id, TestContext.Current.CancellationToken));
        Assert.Equal(DownloadStatus.Processing, await StatusAsync(job.Id));
    }

    [Fact]
    public async Task Resume_and_retry_only_apply_to_jobs_in_the_right_state()
    {
        var service = await StartAsync(max: 1);
        var running = await AddAsync(service, "r");
        await UntilAsync(() => Started("r") == 1, "started");

        Assert.False(await service.ResumeAsync(running.Id, TestContext.Current.CancellationToken)); // not paused
        Assert.False(await service.RetryAsync(running.Id, TestContext.Current.CancellationToken)); // not failed
        Assert.False(await service.ResumeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.False(await service.RetryAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Finish("r");
        await service.WaitForAsync(running.Id);
        Assert.False(await service.RetryAsync(running.Id, TestContext.Current.CancellationToken)); // completed jobs are final
    }

    [Fact]
    public async Task Pause_all_holds_everything_and_resume_all_restarts_it_in_the_original_order()
    {
        var service = await StartAsync(max: 2);
        var a = await AddAsync(service, "a");
        var b = await AddAsync(service, "b");
        var c = await AddAsync(service, "c");
        await UntilAsync(() => _engine.Requests.Count == 2, "two started");

        await service.PauseAllAsync(TestContext.Current.CancellationToken);

        foreach (var job in new[] { a, b, c })
        {
            Assert.Equal(DownloadStatus.Paused, await StatusAsync(job.Id));
        }

        Assert.Equal(2, _engine.Requests.Count); // c never got a slot while the others were being held
        Assert.Empty(_engine.Discarded);

        _gates.Clear();
        await service.ResumeAllAsync(TestContext.Current.CancellationToken);
        await UntilAsync(() => _engine.Requests.Count == 4, "a and b running again");
        // Both start at once on separate threads, so only the pair (not the order of the two calls) is fixed.
        Assert.Equal(new HashSet<Guid> { a.Id, b.Id }, _engine.Requests.Skip(2).Select(r => r.JobId).ToHashSet());
        Assert.Equal(DownloadStatus.Waiting, await StatusAsync(c.Id));
    }

    [Fact]
    public async Task With_automatic_start_off_new_downloads_are_held_until_started()
    {
        var service = await StartAsync(max: 2, autoStart: false);

        var job = await AddAsync(service, "a");
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(DownloadStatus.Paused, await StatusAsync(job.Id));
        Assert.Empty(_engine.Requests);

        Assert.True(await service.ResumeAsync(job.Id, TestContext.Current.CancellationToken));
        await UntilAsync(() => Started("a") == 1, "started once the user asked");
        await DownloadingAsync(job.Id);
        Assert.Equal(DownloadStatus.Downloading, await StatusAsync(job.Id));
    }

    // ---- Cancel ----

    [Fact]
    public async Task Cancelling_a_running_download_starts_the_next_one()
    {
        var service = await StartAsync(max: 1);
        var a = await AddAsync(service, "a");
        await AddAsync(service, "b");
        await UntilAsync(() => Started("a") == 1, "a started");

        await service.CancelAsync(a.Id, TestContext.Current.CancellationToken);

        Assert.Equal(DownloadStatus.Cancelled, await StatusAsync(a.Id));
        Assert.Equal(a.Id, Assert.Single(_engine.Discarded).JobId);
        await UntilAsync(() => Started("b") == 1, "b started");
    }

    [Fact]
    public async Task A_waiting_job_can_be_cancelled_without_ever_running()
    {
        var service = await StartAsync(max: 1);
        await AddAsync(service, "a");
        var b = await AddAsync(service, "b");
        await UntilAsync(() => Started("a") == 1, "a started");

        Assert.True(await service.CancelAsync(b.Id, TestContext.Current.CancellationToken));

        Finish("a");
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, Started("b"));
        Assert.Equal(DownloadStatus.Cancelled, await StatusAsync(b.Id));
    }

    // ---- Retry ----

    [Fact]
    public async Task A_failed_download_can_be_retried_and_continues_from_its_partial_files()
    {
        var service = await StartAsync(max: 1);
        var job = await AddAsync(service, "a");
        await UntilAsync(() => Started("a") == 1, "started");
        Fail("a");
        await service.WaitForAsync(job.Id);
        var failed = await LoadAsync(job.Id);
        Assert.Equal(DownloadStatus.Failed, failed.Status);
        Assert.Equal(10, failed.ProgressPercent);

        _gates.Clear();
        Assert.True(await service.RetryAsync(job.Id, TestContext.Current.CancellationToken));
        await UntilAsync(() => Started("a") == 2, "retry started");
        await UntilAsync(() => _updates.Count(u => u.Job.Id == job.Id && u.Job.Status == DownloadStatus.Downloading) >= 2, "retry downloading");

        var retrying = await LoadAsync(job.Id);
        Assert.Equal(DownloadStatus.Downloading, retrying.Status);
        Assert.Equal(1, retrying.RetryCount);
        Assert.Equal(DownloadErrorKind.None, retrying.ErrorKind);
        Assert.Null(retrying.ErrorMessage);
        Assert.Empty(_engine.Discarded); // the same job and folder, so yt-dlp picks up its partial files
        Assert.All(_engine.Requests, r => Assert.Equal(job.Id, r.JobId));

        Finish("a");
        await service.WaitForAsync(job.Id);
        Assert.Equal(DownloadStatus.Completed, await StatusAsync(job.Id));
    }

    [Fact]
    public async Task A_cancelled_download_can_be_retried_from_the_start()
    {
        var service = await StartAsync(max: 1);
        var job = await AddAsync(service, "a");
        await UntilAsync(() => Started("a") == 1, "started");
        await service.CancelAsync(job.Id, TestContext.Current.CancellationToken);

        _gates.Clear();
        Assert.True(await service.RetryAsync(job.Id, TestContext.Current.CancellationToken));
        await UntilAsync(() => Started("a") == 2, "retry started");
        await UntilAsync(() => _updates.Count(u => u.Job.Id == job.Id && u.Job.Status == DownloadStatus.Downloading) >= 2, "retry downloading");

        var retrying = await LoadAsync(job.Id);
        Assert.Equal(1, retrying.RetryCount);
        Assert.Null(retrying.CompletedAtUtc);
        Assert.Equal(10, retrying.ProgressPercent); // fresh run's own first report; it was reset to 0 before starting
    }

    [Fact]
    public async Task A_retried_job_takes_its_original_place_in_line()
    {
        var service = await StartAsync(max: 1);
        var a = await AddAsync(service, "a");
        await UntilAsync(() => Started("a") == 1, "a started");
        Fail("a");
        await service.WaitForAsync(a.Id);
        var b = await AddAsync(service, "b");
        await UntilAsync(() => Started("b") == 1, "b started");

        _gates.TryRemove(Url("a"), out _); // a's first attempt is over; its retry needs a fresh gate (b is still running)
        await service.RetryAsync(a.Id, TestContext.Current.CancellationToken);
        var c = await AddAsync(service, "c");
        Finish("b");

        // a was added first, so after b it runs before the later c.
        await UntilAsync(() => Started("a") == 2, "a retried before c");
        Assert.Equal(0, Started("c"));
        Assert.NotEqual(Guid.Empty, c.Id);
    }

    // ---- Closing the app ----

    [Fact]
    public async Task Closing_the_app_pauses_what_runs_and_starts_nothing_new()
    {
        var service = await StartAsync(max: 1);
        var a = await AddAsync(service, "a");
        var b = await AddAsync(service, "b");
        await UntilAsync(() => Started("a") == 1, "a started");

        await ((IHostedService)service).StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DownloadStatus.Paused, await StatusAsync(a.Id));
        Assert.Equal(DownloadStatus.Waiting, await StatusAsync(b.Id));
        Assert.Equal(0, Started("b"));
        Assert.Empty(_engine.Discarded);
    }

    // ---- Screens ----

    [Fact]
    public async Task Every_change_of_state_is_announced()
    {
        var service = await StartAsync(max: 1);
        var a = await AddAsync(service, "a");
        await UntilAsync(() => Started("a") == 1, "started");
        await service.PauseAsync(a.Id, TestContext.Current.CancellationToken);
        await service.ResumeAsync(a.Id, TestContext.Current.CancellationToken);

        var states = _updates.Where(u => u.Job.Id == a.Id).Select(u => u.Job.Status).ToList();
        Assert.Contains(DownloadStatus.Waiting, states);
        Assert.Contains(DownloadStatus.Downloading, states);
        Assert.Contains(DownloadStatus.Paused, states);
        Assert.Equal(DownloadStatus.Paused, states[states.LastIndexOf(DownloadStatus.Paused)]);
        Assert.True(states.LastIndexOf(DownloadStatus.Waiting) > states.LastIndexOf(DownloadStatus.Paused));
    }
}
