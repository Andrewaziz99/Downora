using System.Collections.Concurrent;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Services;

/// <summary>
/// Creates download jobs, runs each through <see cref="IDownloadEngine"/> and saves every state change, so the queue,
/// history, Home statistics and crash recovery all read real data. One job runs as one engine call; a queue that
/// limits how many run at once (Phase 5) replaces the "start immediately" step in <see cref="StartAsync"/> only.
/// </summary>
internal sealed partial class DownloadJobService(
    IDbContextFactory<DownoraDbContext> contextFactory,
    IDownloadEngine engine,
    ISettingsService settings,
    TimeProvider time,
    ILogger<DownloadJobService> logger) : IDownloadJobService, IHostedService
{
    /// <summary>Progress is saved this often; status changes are saved at once.</summary>
    internal static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(2);

    /// <summary>Screens are told about progress this often, which is as fast as anyone can read it.</summary>
    internal static readonly TimeSpan NotifyInterval = TimeSpan.FromMilliseconds(250);

    private readonly ConcurrentDictionary<Guid, Run> _runs = new();
    private volatile bool _stopping;

    public event EventHandler<DownloadJobUpdate>? JobUpdated;

    public async Task<DownloadStartResult> StartAsync(NewDownload download, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(download);
        var current = settings.Current;

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var duplicate = await FindActiveDuplicateAsync(db, download, cancellationToken).ConfigureAwait(false);
        if (duplicate is not null)
        {
            LogDuplicate(duplicate.Id);
            return new DownloadStartResult(duplicate, WasAlreadyActive: true);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var job = new DownloadJob
        {
            SourceUrl = download.SourceUrl,
            NormalizedUrl = download.NormalizedUrl,
            Platform = download.Platform,
            Title = download.Title,
            Uploader = download.Uploader,
            ThumbnailUrl = download.ThumbnailUrl,
            Duration = download.Duration,
            MediaKind = download.Kind,
            QualityPreset = download.Kind == MediaKind.Audio ? QualityPreset.AudioOnly : download.Quality,
            Container = download.Kind == MediaKind.Video ? download.Container : null,
            AudioFormat = download.Kind == MediaKind.Audio ? download.AudioFormat : null,
            AudioBitrateKbps = download.Kind == MediaKind.Audio ? download.AudioBitrateKbps : null,
            OutputDirectory = current.DownloadDirectory,
            TotalBytes = download.EstimatedBytes,
            Status = DownloadStatus.Waiting,
            QueueOrder = now.Ticks,
            CreatedAtUtc = now,
        };

        db.DownloadJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogCreated(job.Id, job.MediaKind, job.QualityPreset);

        var snapshot = job.Clone(); // taken before the run starts changing the job on another thread
        var run = new Run(new CancellationTokenSource());
        _runs[job.Id] = run;
        Publish(job, null);

        // The run outlives this call: the caller gets the job back straight away and follows it through JobUpdated.
        run.Task = Task.Run(() => RunAsync(job, run), CancellationToken.None);
        return new DownloadStartResult(snapshot, WasAlreadyActive: false);
    }

    public async Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_runs.TryGetValue(jobId, out var run))
        {
            try
            {
                await run.Cancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The run finished while we were asking it to stop; there is nothing left to cancel.
            }

            await run.Task.ConfigureAwait(false);
            return true;
        }

        // Not running: a job left Waiting or Paused (by a restart) is cancelled directly.
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.IsFinished)
        {
            return false;
        }

        DownloadJobTransitions.Cancel(job, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await engine.DiscardPartialFilesAsync(BuildRequest(job)).ConfigureAwait(false);
        Publish(job, null);
        return true;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// The app is closing: stop every download but keep its partial files, and leave the jobs Paused so they can be
    /// resumed. (If the app is killed instead, the next start moves interrupted jobs to Paused the same way.)
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        var running = _runs.Values.ToList();
        foreach (var run in running)
        {
            try
            {
                await run.Cancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Already finished.
            }
        }

        try
        {
            await Task.WhenAll(running.Select(r => r.Task)).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogStopTimedOut(_runs.Count);
        }
    }

    /// <summary>Completes when the job has ended (for tests and for callers that must wait).</summary>
    internal Task WaitForAsync(Guid jobId) => _runs.TryGetValue(jobId, out var run) ? run.Task : Task.CompletedTask;

    private async Task RunAsync(DownloadJob job, Run run)
    {
        var token = run.Cancellation.Token;
        var request = BuildRequest(job);
        var saveGate = new ThrottleGate(time, SaveInterval);
        var notifyGate = new ThrottleGate(time, NotifyInterval);
        DownloadProgress? latest = null;

        try
        {
            DownloadJobTransitions.Start(job, time.GetUtcNow().UtcDateTime);
            await SaveAsync(job).ConfigureAwait(false);
            Publish(job, null);

            var progress = new InlineProgress<DownloadProgress>(report =>
            {
                latest = report;
                var statusChanged = DownloadJobTransitions.ApplyProgress(job, report);
                if (statusChanged || saveGate.IsDue())
                {
                    SaveQuietly(job);
                }

                if (statusChanged || notifyGate.IsDue())
                {
                    Publish(job, report);
                }
            });

            var result = await engine.DownloadAsync(request, progress, token).ConfigureAwait(false);
            var now = time.GetUtcNow().UtcDateTime;
            if (result.Succeeded)
            {
                DownloadJobTransitions.Complete(job, result.FilePath!, result.FileSize ?? 0, now);
                LogCompleted(job.Id);
            }
            else
            {
                DownloadJobTransitions.Fail(job, result.ErrorKind, result.ErrorDetail, now);
                LogFailed(job.Id, job.ErrorKind);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (_stopping)
            {
                // Closing the app: keep the partial files so the download can continue later.
                DownloadJobTransitions.Pause(job);
                LogPaused(job.Id);
            }
            else
            {
                DownloadJobTransitions.Cancel(job, time.GetUtcNow().UtcDateTime);
                await engine.DiscardPartialFilesAsync(request).ConfigureAwait(false);
                LogCancelled(job.Id);
            }
        }
#pragma warning disable CA1031 // Whatever goes wrong with one download must end that job cleanly, never take the app down.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogCrashed(job.Id, ex);
            if (!job.IsFinished)
            {
                DownloadJobTransitions.Fail(job, DownloadErrorKind.Unknown, ex.Message, time.GetUtcNow().UtcDateTime);
            }
        }

        try
        {
            await SaveAsync(job).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The final state is still announced even if the database is unavailable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogSaveFailed(job.Id, ex);
        }

        // Announce the final state while the run still counts as running, so "wait for the job" really waits for it.
        Publish(job, job.Status == DownloadStatus.Completed ? null : latest);
        _runs.TryRemove(job.Id, out _);
        run.Cancellation.Dispose();
    }

    private DownloadRequest BuildRequest(DownloadJob job) => new()
    {
        JobId = job.Id,
        Url = job.SourceUrl,
        Kind = job.MediaKind,
        Quality = job.QualityPreset,
        Container = job.Container ?? VideoContainer.Mp4,
        AudioFormat = job.AudioFormat ?? AudioFormat.Mp3,
        AudioBitrateKbps = job.AudioBitrateKbps ?? 320,
        OutputDirectory = job.OutputDirectory,
        Title = job.Title,
        Uploader = job.Uploader,
        EstimatedBytes = job.TotalBytes,
        CookieBrowser = CookieBrowsers.For(settings.Current),
    };

    private static async Task<DownloadJob?> FindActiveDuplicateAsync(DownoraDbContext db, NewDownload download, CancellationToken cancellationToken)
    {
        var preset = download.Kind == MediaKind.Audio ? QualityPreset.AudioOnly : download.Quality;
        var audioFormat = download.Kind == MediaKind.Audio ? download.AudioFormat : (AudioFormat?)null;
        DownloadStatus[] finished = [DownloadStatus.Completed, DownloadStatus.Failed, DownloadStatus.Cancelled];

        return await db.DownloadJobs.AsNoTracking()
            .Where(j => j.NormalizedUrl == download.NormalizedUrl
                        && j.QualityPreset == preset
                        && j.MediaKind == download.Kind
                        && j.AudioFormat == audioFormat
                        && !finished.Contains(j.Status))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SaveAsync(DownloadJob job)
    {
        await using var db = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        db.DownloadJobs.Update(job);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>Saves from inside a progress callback (a background thread that must not throw); a missed save is repeated soon.</summary>
    private void SaveQuietly(DownloadJob job)
    {
        try
        {
            using var db = contextFactory.CreateDbContext();
            db.DownloadJobs.Update(job);
            db.SaveChanges();
        }
#pragma warning disable CA1031 // A failed progress save must not stop the download; the next one retries.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogSaveFailed(job.Id, ex);
        }
    }

    private void Publish(DownloadJob job, DownloadProgress? progress)
    {
        var handler = JobUpdated;
        if (handler is null)
        {
            return;
        }

        var update = new DownloadJobUpdate(job.Clone(), progress);
        foreach (var listener in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<DownloadJobUpdate>)listener)(this, update);
            }
#pragma warning disable CA1031 // One misbehaving screen must not stop the download or the other screens.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogListenerFailed(job.Id, ex);
            }
        }
    }

    private sealed class Run(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

        public Task Task { get; set; } = Task.CompletedTask;
    }

    /// <summary>Reports on the calling thread at once, unlike <see cref="Progress{T}"/> which queues to the thread pool and can reorder.</summary>
    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    /// <summary>Says "yes" at most once per interval.</summary>
    private sealed class ThrottleGate(TimeProvider time, TimeSpan interval)
    {
        private long _last = time.GetTimestamp();

        public bool IsDue()
        {
            if (time.GetElapsedTime(_last) < interval)
            {
                return false;
            }

            _last = time.GetTimestamp();
            return true;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Download job {JobId} created ({Kind}, {Quality})")]
    private partial void LogCreated(Guid jobId, MediaKind kind, QualityPreset quality);

    [LoggerMessage(Level = LogLevel.Information, Message = "Download job {JobId} is already queued or running; not starting another")]
    private partial void LogDuplicate(Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Download job {JobId} completed")]
    private partial void LogCompleted(Guid jobId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Download job {JobId} failed ({Kind})")]
    private partial void LogFailed(Guid jobId, DownloadErrorKind kind);

    [LoggerMessage(Level = LogLevel.Information, Message = "Download job {JobId} cancelled by the user")]
    private partial void LogCancelled(Guid jobId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Download job {JobId} paused because the app is closing")]
    private partial void LogPaused(Guid jobId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Download job {JobId} stopped unexpectedly")]
    private partial void LogCrashed(Guid jobId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save download job {JobId}")]
    private partial void LogSaveFailed(Guid jobId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A screen failed while handling an update of download job {JobId}")]
    private partial void LogListenerFailed(Guid jobId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} download(s) did not stop in time while closing")]
    private partial void LogStopTimedOut(int count);
}
