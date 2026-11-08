using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Services;

internal sealed partial class JobRecoveryService(
    IDbContextFactory<DownoraDbContext> contextFactory,
    ILogger<JobRecoveryService> logger) : IJobRecoveryService
{
    private static readonly DownloadStatus[] InFlight =
        [DownloadStatus.Extracting, DownloadStatus.Downloading, DownloadStatus.Processing];

    public async Task<int> RecoverInterruptedJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var recovered = await db.DownloadJobs
            .Where(j => InFlight.Contains(j.Status))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(j => j.Status, DownloadStatus.Paused),
                cancellationToken)
            .ConfigureAwait(false);

        if (recovered > 0)
        {
            LogRecovered(recovered);
        }

        return recovered;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recovered {Count} download(s) interrupted by the last shutdown; they are now paused")]
    private partial void LogRecovered(int count);
}
