using Downora.Core.Interfaces;
using Serilog;
using Serilog.Events;

namespace Downora.Infrastructure.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// Structured logs to a daily rolling file (14 days kept) plus the debugger output.
    /// Raw tool output (stderr) is logged here only; users see mapped, friendly messages.
    /// </summary>
    public static Serilog.ILogger CreateLogger(IAppPaths paths, LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Directory.CreateDirectory(paths.LogsDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Debug(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "downora-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
    }
}
