namespace Downora.Core.Interfaces;

/// <summary>Brings the database to the latest schema and configures SQLite.</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>Repairs state left behind by an unexpected shutdown.</summary>
public interface IJobRecoveryService
{
    /// <summary>
    /// Moves jobs that were mid-flight (Extracting, Downloading, Processing) to Paused,
    /// keeping their partial files so Resume can continue them. Returns how many jobs were recovered.
    /// </summary>
    Task<int> RecoverInterruptedJobsAsync(CancellationToken cancellationToken = default);
}
