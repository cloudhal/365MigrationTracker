using _365MigrationTracker.Data;

namespace _365MigrationTracker.Services;

/// <summary>
/// Abstraction for storing and retrieving metric snapshots.
/// This interface allows future implementations using different storage backends (e.g., Azure Table Storage).
/// </summary>
public interface IMetricSnapshotStore
{
    /// <summary>
    /// Saves a metric snapshot to storage.
    /// </summary>
    /// <param name="snapshot">The snapshot to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved snapshot with its Id populated.</returns>
    Task<MetricSnapshot> SaveSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent successful snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest successful snapshot, or null if none exist.</returns>
    Task<MetricSnapshot?> GetLatestSuccessfulSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves successful snapshots within a date range.
    /// </summary>
    /// <param name="startUtc">Start of date range (inclusive).</param>
    /// <param name="endUtc">End of date range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Successful snapshots ordered by CapturedAtUtc ascending.</returns>
    Task<IEnumerable<MetricSnapshot>> GetSuccessfulSnapshotsAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent failed collection attempts.
    /// </summary>
    /// <param name="limit">Maximum number of failures to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Failed snapshots ordered by CapturedAtUtc descending.</returns>
    Task<IEnumerable<MetricSnapshot>> GetRecentFailuresAsync(int limit = 10, CancellationToken cancellationToken = default);
}

