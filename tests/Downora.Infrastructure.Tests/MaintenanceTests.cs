using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Infrastructure.Database;
using Downora.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

public sealed class MaintenanceTests : IAsyncDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly IToolManager _tools = Substitute.For<IToolManager>();
    private TestEnvironment? _env;
    private ISettingsService _settings = null!;

    public async ValueTask DisposeAsync()
    {
        if (_env is not null)
        {
            await _env.DisposeAsync();
        }
    }

    private async Task<AppMaintenance> StartAsync(Action<AppSettings>? configure = null)
    {
        _env = await TestEnvironment.StartAsync();
        _settings = _env.Get<ISettingsService>();
        await _settings.LoadAsync(TestContext.Current.CancellationToken);
        var s = _settings.Current;
        s.YtDlpAutoUpdate = true;
        configure?.Invoke(s);
        await _settings.SaveAsync(s, TestContext.Current.CancellationToken);

        _tools.GetPath(ToolKind.YtDlp).Returns("C:/tools/yt-dlp.exe");
        _tools.UpdateYtDlpAsync(Arg.Any<CancellationToken>()).Returns("2026.11.10");
        var maintenance = new AppMaintenance(_env.Get<IDownloadJobQueries>(), _tools, _settings, _time, NullLogger<AppMaintenance>.Instance);
        await maintenance.StartAsync(TestContext.Current.CancellationToken);
        return maintenance;
    }

    private async Task AddJobAsync(DownloadStatus status, int daysAgo)
    {
        var when = _time.GetUtcNow().UtcDateTime.AddDays(-daysAgo);
        await using var db = await _env!.CreateDbAsync();
        db.DownloadJobs.Add(new DownloadJob
        {
            SourceUrl = "https://e.com/" + Guid.NewGuid(),
            NormalizedUrl = "https://e.com/" + Guid.NewGuid(),
            OutputDirectory = "x",
            Status = status,
            CreatedAtUtc = when,
            CompletedAtUtc = status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled ? when : null,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> JobCountAsync()
    {
        await using var db = await _env!.CreateDbAsync();
        return await db.DownloadJobs.CountAsync(TestContext.Current.CancellationToken);
    }

    // ---- History retention ----

    [Fact]
    public async Task Startup_removes_finished_jobs_older_than_the_retention_and_keeps_the_rest()
    {
        var maintenance = await StartAsync(s => { s.HistoryRetentionDays = 30; s.YtDlpAutoUpdate = false; });
        await AddJobAsync(DownloadStatus.Completed, daysAgo: 60);
        await AddJobAsync(DownloadStatus.Failed, daysAgo: 31);
        await AddJobAsync(DownloadStatus.Completed, daysAgo: 5);
        await AddJobAsync(DownloadStatus.Waiting, daysAgo: 90); // not finished: never pruned

        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, await JobCountAsync());
    }

    [Fact]
    public async Task Without_a_retention_nothing_is_removed()
    {
        var maintenance = await StartAsync(s => { s.HistoryRetentionDays = null; s.YtDlpAutoUpdate = false; });
        await AddJobAsync(DownloadStatus.Completed, daysAgo: 4000);

        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await JobCountAsync());
    }

    [Fact]
    public async Task Choosing_a_shorter_retention_prunes_at_once_but_unrelated_saves_do_not()
    {
        var maintenance = await StartAsync(s => { s.YtDlpAutoUpdate = false; });
        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);
        await AddJobAsync(DownloadStatus.Completed, daysAgo: 100);

        var s = _settings.Current;
        s.MaxConcurrentDownloads = 4; // unrelated
        await _settings.SaveAsync(s, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(1, await JobCountAsync());

        s = _settings.Current;
        s.HistoryRetentionDays = 90;
        await _settings.SaveAsync(s, TestContext.Current.CancellationToken);
        for (var i = 0; i < 300 && await JobCountAsync() != 0; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await JobCountAsync());
    }

    [Fact]
    public async Task Pruning_keeps_the_library_entry_and_the_file()
    {
        var maintenance = await StartAsync(s => { s.HistoryRetentionDays = 30; s.YtDlpAutoUpdate = false; });
        var jobId = Guid.NewGuid();
        await using (var db = await _env!.CreateDbAsync())
        {
            var old = _time.GetUtcNow().UtcDateTime.AddDays(-90);
            db.DownloadJobs.Add(new DownloadJob
            {
                Id = jobId, SourceUrl = "https://e.com/a", NormalizedUrl = "https://e.com/a", OutputDirectory = "x",
                Status = DownloadStatus.Completed, CreatedAtUtc = old, CompletedAtUtc = old,
            });
            db.MediaItems.Add(new MediaItem { FilePath = @"D:\Media\a.mp4", DownloadJobId = jobId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);

        await using var check = await _env.CreateDbAsync();
        Assert.Empty(await check.DownloadJobs.ToListAsync(TestContext.Current.CancellationToken));
        var item = Assert.Single(await check.MediaItems.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Null(item.DownloadJobId);
    }

    // ---- yt-dlp updates ----

    [Fact]
    public async Task yt_dlp_is_updated_at_startup_and_the_check_is_recorded()
    {
        var maintenance = await StartAsync();

        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);

        await _tools.Received(1).UpdateYtDlpAsync(Arg.Any<CancellationToken>());
        Assert.Equal(_time.GetUtcNow().UtcDateTime, _settings.Current.LastToolCheckUtc);
    }

    [Fact]
    public async Task yt_dlp_is_checked_at_most_once_a_day()
    {
        var maintenance = await StartAsync();
        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromHours(23));
        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);
        await _tools.Received(1).UpdateYtDlpAsync(Arg.Any<CancellationToken>());

        _time.Advance(TimeSpan.FromHours(2));
        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);
        await _tools.Received(2).UpdateYtDlpAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_update_check_when_the_setting_is_off_or_yt_dlp_is_missing()
    {
        var off = await StartAsync(s => s.YtDlpAutoUpdate = false);
        await off.RunStartupTasksAsync(TestContext.Current.CancellationToken);
        await _tools.DidNotReceive().UpdateYtDlpAsync(Arg.Any<CancellationToken>());
        await _env!.DisposeAsync();

        _tools.ClearReceivedCalls();
        var missing = await StartAsync();
        _tools.GetPath(ToolKind.YtDlp).Returns((string?)null);
        await missing.RunStartupTasksAsync(TestContext.Current.CancellationToken);
        await _tools.DidNotReceive().UpdateYtDlpAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DownloadStatus.Downloading)]
    [InlineData(DownloadStatus.Extracting)]
    [InlineData(DownloadStatus.Processing)]
    public async Task yt_dlp_is_not_replaced_while_a_download_is_running_and_is_retried_next_time(DownloadStatus running)
    {
        var maintenance = await StartAsync();
        await AddJobAsync(running, daysAgo: 0);

        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);

        await _tools.DidNotReceive().UpdateYtDlpAsync(Arg.Any<CancellationToken>());
        Assert.Null(_settings.Current.LastToolCheckUtc); // not recorded, so the next start checks again
    }

    [Fact]
    public async Task A_failed_update_is_logged_not_thrown_and_still_counts_as_a_check()
    {
        var maintenance = await StartAsync();
        _tools.UpdateYtDlpAsync(Arg.Any<CancellationToken>()).Returns<Task<string?>>(_ => throw new ToolInstallException("offline"));

        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(_settings.Current.LastToolCheckUtc);
    }

    [Fact]
    public async Task An_unexpected_failure_never_escapes_the_startup_tasks()
    {
        var maintenance = await StartAsync();
        _tools.UpdateYtDlpAsync(Arg.Any<CancellationToken>()).Returns<Task<string?>>(_ => throw new InvalidOperationException("boom"));

        await maintenance.RunStartupTasksAsync(TestContext.Current.CancellationToken);
    }
}

public sealed class HistoryCleanupTests : IAsyncDisposable
{
    private TestEnvironment? _env;

    public async ValueTask DisposeAsync()
    {
        if (_env is not null)
        {
            await _env.DisposeAsync();
        }
    }

    [Fact]
    public async Task One_finished_entry_can_be_removed_but_a_running_one_cannot()
    {
        _env = await TestEnvironment.StartAsync();
        var done = new DownloadJob { SourceUrl = "https://e.com/a", NormalizedUrl = "https://e.com/a", OutputDirectory = "x", Status = DownloadStatus.Completed, CreatedAtUtc = DateTime.UtcNow };
        var running = new DownloadJob { SourceUrl = "https://e.com/b", NormalizedUrl = "https://e.com/b", OutputDirectory = "x", Status = DownloadStatus.Downloading, CreatedAtUtc = DateTime.UtcNow };
        await using (var db = await _env.CreateDbAsync())
        {
            db.DownloadJobs.AddRange(done, running);
            db.MediaItems.Add(new MediaItem { FilePath = @"D:\Media\a.mp4", DownloadJobId = done.Id });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var queries = _env.Get<IDownloadJobQueries>();

        Assert.True(await queries.RemoveFromHistoryAsync(done.Id, TestContext.Current.CancellationToken));
        Assert.False(await queries.RemoveFromHistoryAsync(done.Id, TestContext.Current.CancellationToken));
        Assert.False(await queries.RemoveFromHistoryAsync(running.Id, TestContext.Current.CancellationToken));

        await using var check = await _env.CreateDbAsync();
        Assert.Equal([running.Id], await check.DownloadJobs.Select(j => j.Id).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await check.MediaItems.ToListAsync(TestContext.Current.CancellationToken)); // the library entry stays
    }
}

public sealed class StartupSynchronizerTests
{
    private sealed class FakeRegistry : IStartupRegistry
    {
        public string? Command { get; set; }

        public int Writes { get; private set; }

        public bool Throw { get; set; }

        public string? GetCommand() => Command;

        public void SetCommand(string? command)
        {
            if (Throw)
            {
                throw new UnauthorizedAccessException("locked down");
            }

            Command = command;
            Writes++;
        }
    }

    private readonly FakeRegistry _registry = new();
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();

    private StartupSynchronizer Create() => new(_registry, _settings, NullLogger<StartupSynchronizer>.Instance);

    private static AppSettings Settings(bool launch) => new() { DownloadDirectory = "x", LaunchAtStartup = launch };

    [Fact]
    public void Turning_it_on_writes_a_quoted_command_that_marks_a_startup_launch()
    {
        Create().Apply(Settings(launch: true));

        Assert.NotNull(_registry.Command);
        Assert.StartsWith("\"", _registry.Command, StringComparison.Ordinal);
        Assert.EndsWith("\" " + StartupCommand.Argument, _registry.Command, StringComparison.Ordinal);
    }

    [Fact]
    public void Turning_it_off_removes_the_entry_and_an_unchanged_state_writes_nothing()
    {
        var sync = Create();
        sync.Apply(Settings(launch: true));
        sync.Apply(Settings(launch: true));
        Assert.Equal(1, _registry.Writes);

        sync.Apply(Settings(launch: false));
        Assert.Null(_registry.Command);
        sync.Apply(Settings(launch: false));
        Assert.Equal(2, _registry.Writes);
    }

    [Fact]
    public void A_stale_entry_from_another_location_is_corrected_at_startup()
    {
        _registry.Command = "\"C:\\Old\\Downora.exe\" --startup";
        _settings.Current.Returns(Settings(launch: true));

        Create().Synchronize();

        Assert.DoesNotContain("Old", _registry.Command, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_the_setting_updates_the_entry_and_a_locked_registry_is_survived()
    {
        var sync = Create();
        await sync.StartAsync(TestContext.Current.CancellationToken);

        _settings.Changed += NSubstitute.Raise.Event<EventHandler<AppSettings>>(_settings, Settings(launch: true));
        Assert.NotNull(_registry.Command);

        _registry.Throw = true;
        _settings.Changed += NSubstitute.Raise.Event<EventHandler<AppSettings>>(_settings, Settings(launch: false)); // must not throw
    }
}
