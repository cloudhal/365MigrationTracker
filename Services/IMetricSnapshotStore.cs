using _365MigrationTracker.Data;

namespace _365MigrationTracker.Services;

/// <summary>
/// Abstraction for storing and retrieving metric snapshots.
///
/// Every read takes a tenantId and is filtered by it. That parameter is mandatory
/// rather than optional on purpose: the app is multi-tenant with one shared table, so
/// an unfiltered read would show one customer another customer's figures. Making it a
/// required argument means a caller cannot omit it by accident.
/// </summary>
public interface IMetricSnapshotStore
{
    /// <summary>
    /// Saves a snapshot. The snapshot's TenantId must already be set.
    /// </summary>
    Task<MetricSnapshot> SaveSnapshotAsync(
        MetricSnapshot snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Most recent successful snapshot for the given tenant, or null if there is none.
    /// </summary>
    Task<MetricSnapshot?> GetLatestSuccessfulSnapshotAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Successful snapshots for the given tenant within the date range, oldest first.
    /// </summary>
    Task<IEnumerable<MetricSnapshot>> GetSuccessfulSnapshotsAsync(
        string tenantId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recent failed collection attempts for the given tenant, newest first.
    /// </summary>
    Task<IEnumerable<MetricSnapshot>> GetRecentFailuresAsync(
        string tenantId,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
