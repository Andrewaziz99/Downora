namespace Downora.App.Services;

/// <summary>
/// Lets only one Downora run per Windows session: a second launch asks the first to show its window and exits.
/// Two copies on one database would fight over the queue and the settings.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string Name = "Downora.SingleInstance.v1";

    /// <summary>Developers can run several copies side by side by setting this to 1.</summary>
    internal const string AllowMultipleVariable = "DOWNORA_ALLOW_MULTIPLE_INSTANCES";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showRequest;
    private readonly ManualResetEvent _stop = new(false);

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, $"Local\\{Name}", out var created);
        _showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{Name}.Show");
        IsFirst = created || Environment.GetEnvironmentVariable(AllowMultipleVariable) == "1";
    }

    /// <summary>True when no other Downora is running (or multiple copies were allowed).</summary>
    public bool IsFirst { get; }

    /// <summary>Asks the running Downora to show its window.</summary>
    public void SignalRunningInstance() => _showRequest.Set();

    /// <summary>Calls <paramref name="onShowRequested"/> (on a background thread) each time another launch asks for the window.</summary>
    public void ListenForShowRequests(Action onShowRequested)
    {
        var thread = new Thread(() =>
        {
            WaitHandle[] handles = [_showRequest, _stop];
            while (WaitHandle.WaitAny(handles) == 0)
            {
                onShowRequested();
            }
        })
        {
            IsBackground = true,
            Name = "Downora single-instance listener",
        };
        thread.Start();
    }

    public void Dispose()
    {
        _stop.Set();

        // The mutex is not released on purpose: it belongs to the process and Windows frees it when the process ends.
        _mutex.Dispose();
        _showRequest.Dispose();
        _stop.Dispose();
    }
}
