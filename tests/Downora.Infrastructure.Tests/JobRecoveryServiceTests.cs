using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Downora.Infrastructure.Tests;

public class JobRecoveryServiceTests
{
    [Fact]
    public async Task Interrupted_jobs_are_paused_after_a_restart_and_others_are_untouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = await TestEnvironment.StartAsync();

        var statuses = Enum.GetValues<DownloadStatus>();
        await using (var db = await env.CreateDbAsync())
        {
            foreach (var status in statuses)
            {
                db.DownloadJobs.Add(new DownloadJob
                {
                    SourceUrl = $"https://example.com/{status}",
                    NormalizedUrl = $"https://example.com/{status}",
                    OutputDirectory = env.Paths.DefaultDownloadDirectory,
                    Status = status,
                    ProgressPercent = 42,
                });
            }

            await db.SaveChangesAsync(ct);
        }

        // Simulate the app being killed and opened again.
        await using var restarted = await env.RestartAsync();
        var recovered = await restarted.Get<IJobRecoveryService>().RecoverInterruptedJobsAsync(ct);

        Assert.Equal(3, recovered);

        await using var check = await restarted.CreateDbAsync();
        var byUrl = await check.DownloadJobs.ToDictionaryAsync(j => j.SourceUrl, ct);

        Assert.Equal(DownloadStatus.Paused, byUrl["https://example.com/Extracting"].Status);
        Assert.Equal(DownloadStatus.Paused, byUrl["https://example.com/Downloading"].Status);
        Assert.Equal(DownloadStatus.Paused, byUrl["https://example.com/Processing"].Status);
        Assert.Equal(DownloadStatus.Waiting, byUrl["https://example.com/Waiting"].Status);
        Assert.Equal(DownloadStatus.Completed, byUrl["https://example.com/Completed"].Status);
        Assert.Equal(DownloadStatus.Failed, byUrl["https://example.com/Failed"].Status);
        Assert.Equal(DownloadStatus.Cancelled, byUrl["https://example.com/Cancelled"].Status);
        Assert.Equal(DownloadStatus.Paused, byUrl["https://example.com/Paused"].Status);

        // Progress is kept so the UI can show where a resumed job stands.
        Assert.All(byUrl.Values, j => Assert.Equal(42, j.ProgressPercent));
    }

    [Fact]
    public async Task Nothing_to_recover_returns_zero()
    {
        await using var env = await TestEnvironment.StartAsync();

        var recovered = await env.Get<IJobRecoveryService>().RecoverInterruptedJobsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, recovered);
    }
}
