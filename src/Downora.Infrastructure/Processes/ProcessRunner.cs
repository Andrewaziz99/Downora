using System.Diagnostics;
using System.Text;
using Downora.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Downora.Infrastructure.Processes;

/// <summary>
/// Starts external tools safely: no shell, arguments passed one by one, UTF-8 output,
/// and the whole process tree killed on cancel or timeout.
/// </summary>
internal sealed partial class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    /// <summary>Only the end of stderr matters for errors; keep it bounded for long downloads.</summary>
    private const int MaxErrorLines = 200;

    public async Task<ProcessResult> RunAsync(
        ProcessSpec spec,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var startInfo = new ProcessStartInfo(spec.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (spec.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = spec.WorkingDirectory;
        }

        if (spec.Environment is not null)
        {
            foreach (var (key, value) in spec.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        var output = new StringBuilder();
        var errors = new Queue<string>();
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            // A long download prints a progress line every moment; callers that follow it line by line don't need them all kept.
            if (spec.CaptureStandardOutput)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            }

            onOutputLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            lock (errors)
            {
                errors.Enqueue(e.Data);
                while (errors.Count > MaxErrorLines)
                {
                    errors.Dequeue();
                }
            }

            onErrorLine?.Invoke(e.Data);
        };

        var toolName = Path.GetFileName(spec.FileName);
        LogStarting(toolName, spec.Arguments.Count);
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {spec.FileName}.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = spec.Timeout is { } limit ? new CancellationTokenSource(limit) : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var timedOut = false;

        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);

            // Make sure the last output lines have been delivered to the handlers.
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            timedOut = true;
            LogTimedOut(toolName, spec.Timeout ?? TimeSpan.Zero);
        }

        string stdout, stderr;
        lock (output)
        {
            stdout = output.ToString();
        }

        lock (errors)
        {
            stderr = string.Join(Environment.NewLine, errors);
        }

        return new ProcessResult(timedOut ? -1 : process.ExitCode, stdout, stderr, timedOut);
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting {Tool} with {ArgumentCount} argument(s)")]
    private partial void LogStarting(string tool, int argumentCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Tool} timed out after {Timeout} and was stopped")]
    private partial void LogTimedOut(string tool, TimeSpan timeout);
}
