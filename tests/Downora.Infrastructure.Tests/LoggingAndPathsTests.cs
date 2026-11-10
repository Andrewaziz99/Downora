using Downora.Infrastructure.Logging;
using Downora.Infrastructure.Services;
using Serilog.Events;

namespace Downora.Infrastructure.Tests;

public sealed class LoggingAndPathsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("downora-log-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A log file may still be held for a moment; the temp folder is cleaned up by Windows later.
        }
    }

    [Fact]
    public void Logs_go_to_a_daily_file_in_the_logs_folder_and_the_level_filters_noise()
    {
        var paths = new AppPaths(Path.Combine(_root, "data"), Path.Combine(_root, "downloads"));

        var logger = LoggingSetup.CreateLogger(paths, LogEventLevel.Information);
        logger.Information("hello {Thing}", "world");
        logger.Debug("quiet details");
        ((IDisposable)logger).Dispose();

        var file = Assert.Single(Directory.GetFiles(paths.LogsDirectory, "downora-*.log"));
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var text = new StreamReader(stream).ReadToEnd();
        Assert.Contains("hello world", text, StringComparison.Ordinal);
        Assert.DoesNotContain("quiet details", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Creating_a_logger_without_paths_is_refused() =>
        Assert.Throws<ArgumentNullException>(() => LoggingSetup.CreateLogger(null!));

    [Fact]
    public void The_app_folders_live_under_the_users_local_data_and_are_created_on_demand()
    {
        var current = AppPaths.ForCurrentUser();
        Assert.EndsWith(AppPaths.AppFolderName, current.DataDirectory, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(current.DataDirectory, "downora.db"), current.DatabasePath);
        Assert.EndsWith(Path.Combine("Downloads", AppPaths.AppFolderName), current.DefaultDownloadDirectory, StringComparison.Ordinal);

        var paths = new AppPaths(Path.Combine(_root, "d"), Path.Combine(_root, "dl"));
        paths.EnsureCreated();

        Assert.True(Directory.Exists(paths.LogsDirectory));
        Assert.True(Directory.Exists(paths.ToolsDirectory));
        Assert.True(Directory.Exists(paths.ThumbnailsDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_folders_are_refused(string folder)
    {
        Assert.Throws<ArgumentException>(() => new AppPaths(folder, _root));
        Assert.Throws<ArgumentException>(() => new AppPaths(_root, folder));
    }
}
