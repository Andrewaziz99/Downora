using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Downora.Infrastructure.Services;

internal sealed class DownloadJobQueries(
    IDbContextFactory<DownoraDbContext> contextFactory,
    TimeProvider timeProvider) : IDownloadJobQueries
{
    private static readonly DownloadStatus[] Finished =
        [DownloadStatus.Completed, DownloadStatus.Failed, DownloadStatus.Cancelled];

    public async Task<IReadOnlyList<DownloadJob>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var jobs = await db.DownloadJobs.AsNoTracking()
            .Where(j => !Finished.Contains(j.Status))
            .OrderBy(j => j.QueueOrder)
            .ThenBy(j => j.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Running work first, then what is waiting, then what the user paused.
        return [.. jobs.OrderBy(j => ActiveRank(j.Status))];
    }

    public async Task<IReadOnlyDictionary<DownloadStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.DownloadJobs.AsNoTracking()
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DownloadJob>> GetHistoryAsync(HistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var jobs = db.DownloadJobs.AsNoTracking().Where(j => Finished.Contains(j.Status));

        if (query.Status is { } status)
        {
            jobs = jobs.Where(j => j.Status == status);
        }

        if (SearchPattern.FromText(query.Search) is { } pattern)
        {
            jobs = jobs.Where(j =>
                EF.Functions.Like(j.Title ?? string.Empty, pattern, SearchPattern.Escape) ||
                EF.Functions.Like(j.SourceUrl, pattern, SearchPattern.Escape));
        }

        return await jobs
            .OrderByDescending(j => j.CompletedAtUtc ?? j.CreatedAtUtc)
            .Take(Math.Max(1, query.Take))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DownloadStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var weekAgo = timeProvider.GetUtcNow().UtcDateTime.AddDays(-7);

        var completed = db.DownloadJobs.AsNoTracking().Where(j => j.Status == DownloadStatus.Completed);
        var totalCompleted = await completed.CountAsync(cancellationToken).ConfigureAwait(false);
        var lastWeek = await completed.CountAsync(j => j.CompletedAtUtc >= weekAgo, cancellationToken).ConfigureAwait(false);
        var bytes = await completed.SumAsync(j => (long?)(j.TotalBytes ?? j.DownloadedBytes), cancellationToken).ConfigureAwait(false) ?? 0;
        var failed = await db.DownloadJobs.CountAsync(j => j.Status == DownloadStatus.Failed, cancellationToken).ConfigureAwait(false);
        var active = await db.DownloadJobs.CountAsync(
            j => j.Status == DownloadStatus.Extracting || j.Status == DownloadStatus.Downloading || j.Status == DownloadStatus.Processing,
            cancellationToken).ConfigureAwait(false);

        double? successRate = totalCompleted + failed == 0 ? null : (double)totalCompleted / (totalCompleted + failed);
        return new DownloadStatistics(totalCompleted, lastWeek, bytes, active, successRate);
    }

    public async Task<int> ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await DeleteAsync(db, db.DownloadJobs.Where(j => Finished.Contains(j.Status)), cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> PruneHistoryAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await DeleteAsync(
            db,
            db.DownloadJobs.Where(j => Finished.Contains(j.Status) && (j.CompletedAtUtc ?? j.CreatedAtUtc) < cutoffUtc),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveFromHistoryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await DeleteAsync(db, db.DownloadJobs.Where(j => j.Id == jobId && Finished.Contains(j.Status)), cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>Deletes the selected (finished) jobs. Their library entries are unlinked first, so the files stay in the library.</summary>
    private static async Task<int> DeleteAsync(DownoraDbContext db, IQueryable<DownloadJob> selected, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await db.MediaItems
            .Where(m => m.DownloadJobId != null && selected.Any(j => j.Id == m.DownloadJobId))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.DownloadJobId, (Guid?)null), cancellationToken)
            .ConfigureAwait(false);

        var removed = await selected.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return removed;
    }

    private static int ActiveRank(DownloadStatus status) => status switch
    {
        DownloadStatus.Downloading => 0,
        DownloadStatus.Processing => 1,
        DownloadStatus.Extracting => 2,
        DownloadStatus.Waiting => 3,
        DownloadStatus.Paused => 4,
        _ => 5,
    };
}
