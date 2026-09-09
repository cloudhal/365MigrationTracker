using _365MigrationTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace _365MigrationTracker.Services;

/// <summary>
/// SQLite implementation of IMetricSnapshotStore using Entity Framework Core.
///
/// Every query here filters on TenantId. That filter is the only thing keeping one
/// customer's figures away from another's, so it must not be removed from any query,
/// and new queries must include it.
/// </summary>
public class SqliteMetricSnapshotStore : IMetricSnapshotStore
{
    private readonly DashboardDbContext _context;
    private readonly ILogger<SqliteMetricSnapshotStore> _logger;

    public SqliteMetricSnapshotStore(DashboardDbContext context, ILogger<SqliteMetricSnapshotStore> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MetricSnapshot> SaveSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // A snapshot without a tenant would be invisible to every read and could never be
        // attributed later. Fail loudly rather than quietly write an orphan row.
        if (string.IsNullOrWhiteSpace(snapshot.TenantId))
        {
            throw new ArgumentException(
                "Snapshot has no TenantId. Snapshots must be attributed to the signed-in user's tenant.",
                nameof(snapshot));
        }

        _context.MetricSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Snapshot saved for tenant {TenantId}: Success={Success}, Users={Users}, Groups={Groups}, Duration={DurationMs}ms",
            snapshot.TenantId,
            snapshot.CollectionSucceeded,
            snapshot.SyncedUsers,
            snapshot.SyncedGroups,
            snapshot.CollectionDurationMilliseconds);

        return snapshot;
    }

    public async Task<MetricSnapshot?> GetLatestSuccessfulSnapshotAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return null;

        return await _context.MetricSnapshots
            .Where(s => s.TenantId == tenantId && s.CollectionSucceeded)
            .OrderByDescending(s => s.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<MetricSnapshot>> GetSuccessfulSnapshotsAsync(
        string tenantId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return Array.Empty<MetricSnapshot>();

        return await _context.MetricSnapshots
            .Where(s => s.TenantId == tenantId
                        && s.CollectionSucceeded
                        && s.CapturedAtUtc >= startUtc
                        && s.CapturedAtUtc <= endUtc)
            .OrderBy(s => s.CapturedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<MetricSnapshot>> GetRecentFailuresAsync(
        string tenantId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return Array.Empty<MetricSnapshot>();

        return await _context.MetricSnapshots
            .Where(s => s.TenantId == tenantId && !s.CollectionSucceeded)
            .OrderByDescending(s => s.CapturedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
