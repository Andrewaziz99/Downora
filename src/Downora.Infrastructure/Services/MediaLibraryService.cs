using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Services;

internal sealed partial class MediaLibraryService(
    IDbContextFactory<DownoraDbContext> contextFactory,
    IMediaProbe probe,
    ISettingsService settings,
    TimeProvider time,
    ILogger<MediaLibraryService> logger) : IMediaLibraryService
{
    public async Task<IReadOnlyList<MediaItem>> QueryAsync(LibraryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var items = db.MediaItems.AsNoTracking();

        if (query.Kind is { } kind)
        {
            items = items.Where(m => m.MediaKind == kind);
        }

        if (SearchPattern.FromText(query.Search) is { } pattern)
        {
            items = items.Where(m =>
                EF.Functions.Like(m.Title ?? string.Empty, pattern, SearchPattern.Escape) ||
                EF.Functions.Like(m.Artist ?? string.Empty, pattern, SearchPattern.Escape) ||
                EF.Functions.Like(m.FilePath, pattern, SearchPattern.Escape));
        }

        items = (query.Sort, query.Descending) switch
        {
            (LibrarySort.Title, false) => items.OrderBy(m => m.Title ?? m.FilePath),
            (LibrarySort.Title, true) => items.OrderByDescending(m => m.Title ?? m.FilePath),
            (LibrarySort.Duration, false) => items.OrderBy(m => m.Duration),
            (LibrarySort.Duration, true) => items.OrderByDescending(m => m.Duration),
            (LibrarySort.FileSize, false) => items.OrderBy(m => m.FileSizeBytes),
            (LibrarySort.FileSize, true) => items.OrderByDescending(m => m.FileSizeBytes),
            (_, false) => items.OrderBy(m => m.AddedAtUtc),
            _ => items.OrderByDescending(m => m.AddedAtUtc),
        };

        return await items.Take(Math.Max(1, query.Take)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddCompletedAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Status != DownloadStatus.Completed || string.IsNullOrWhiteSpace(job.OutputPath) || !File.Exists(job.OutputPath))
        {
            return;
        }

        var details = await probe.ProbeAsync(job.OutputPath, cancellationToken).ConfigureAwait(false);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // One entry per file: a re-download over the same path refreshes the entry instead of adding a second.
        var item = await db.MediaItems.FirstOrDefaultAsync(m => m.FilePath == job.OutputPath, cancellationToken).ConfigureAwait(false);
        var isNew = item is null;
        item ??= new MediaItem { FilePath = job.OutputPath, AddedAtUtc = job.CompletedAtUtc ?? time.GetUtcNow().UtcDateTime };
        Fill(item, job, details);

        if (isNew)
        {
            db.MediaItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogAdded(job.Id, isNew);
    }

    public async Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Old downloads are added once. Afterwards a removed entry must stay removed, so nothing is re-added.
            var changed = 0;
            if (!settings.Current.LibraryBackfilled)
            {
                changed = await AddMissingEntriesAsync(cancellationToken).ConfigureAwait(false);
                var current = settings.Current;
                current.LibraryBackfilled = true;
                await settings.SaveAsync(current, cancellationToken).ConfigureAwait(false);
            }

            changed += await RefreshAvailabilityAsync(cancellationToken).ConfigureAwait(false);
            return changed;
        }
#pragma warning disable CA1031 // Keeping the library tidy is best effort; it must never stop the app or a screen from loading.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogSyncFailed(ex);
            return 0;
        }
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var removed = await db.MediaItems.Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return removed > 0;
    }

    /// <summary>Completed downloads from before the library existed get an entry.</summary>
    private async Task<int> AddMissingEntriesAsync(CancellationToken cancellationToken)
    {
        List<DownloadJob> candidates;
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            var known = db.MediaItems.Select(m => m.FilePath);
            candidates = await db.DownloadJobs.AsNoTracking()
                .Where(j => j.Status == DownloadStatus.Completed && j.OutputPath != null && !known.Contains(j.OutputPath))
                .OrderBy(j => j.CompletedAtUtc)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var added = 0;
        foreach (var job in candidates.Where(j => File.Exists(j.OutputPath)))
        {
            await AddCompletedAsync(job, cancellationToken).ConfigureAwait(false);
            added++;
        }

        return added;
    }

    private async Task<int> RefreshAvailabilityAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var items = await db.MediaItems.ToListAsync(cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var item in items)
        {
            var missing = !File.Exists(item.FilePath);
            if (item.IsMissing != missing)
            {
                item.IsMissing = missing;
                changed++;
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    private static void Fill(MediaItem item, DownloadJob job, MediaProbe? details)
    {
        item.DownloadJobId = job.Id;
        item.MediaKind = job.MediaKind;
        item.Title = job.Title;
        item.Artist = job.Uploader;
        item.Duration = details?.Duration ?? job.Duration;
        item.FileSizeBytes = new FileInfo(item.FilePath).Length;
        item.Width = job.MediaKind == MediaKind.Video ? details?.Width : null;
        item.Height = job.MediaKind == MediaKind.Video ? details?.Height : null;
        item.VideoCodec = job.MediaKind == MediaKind.Video ? details?.VideoCodec : null;
        item.AudioCodec = details?.AudioCodec;
        item.BitrateKbps = details?.BitrateKbps;
        item.ThumbnailPath = job.ThumbnailUrl;
        item.SourceUrl = job.SourceUrl;
        item.Platform = job.Platform;
        item.IsMissing = false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Library: {Action} the entry for download {JobId}")]
    private partial void LogAdded(Guid jobId, bool action);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Library sync failed")]
    private partial void LogSyncFailed(Exception exception);
}
