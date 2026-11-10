using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Services;

/// <summary>Adds each download to the media library the moment it completes.</summary>
internal sealed partial class LibraryIndexer(
    IDownloadJobService downloads,
    IMediaLibraryService library,
    ILogger<LibraryIndexer> logger) : IHostedService
{
    /// <summary>Only subscribes: the database and settings are not ready when the host starts.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        downloads.JobUpdated += OnJobUpdated;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        downloads.JobUpdated -= OnJobUpdated;
        return Task.CompletedTask;
    }

    private void OnJobUpdated(object? sender, DownloadJobUpdate update)
    {
        if (update.Job.Status == DownloadStatus.Completed)
        {
            _ = IndexAsync(update.Job);
        }
    }

    private async Task IndexAsync(DownloadJob job)
    {
        try
        {
            await library.AddCompletedAsync(job).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A download that finished is not undone because its library entry couldn't be written; the next sync adds it.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogFailed(job.Id, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not add download {JobId} to the library")]
    private partial void LogFailed(Guid jobId, Exception exception);
}
