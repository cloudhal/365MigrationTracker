using _365MigrationTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace _365MigrationTracker.Services;

/// <summary>
/// SQLite implementation of IMetricSnapshotStore using Entity Framework Core.
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
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        _context.MetricSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Snapshot saved: Success={Success}, Synced Users={Users}, Synced Groups={Groups}, Duration={DurationMs}ms",
            snapshot.CollectionSucceeded,
            snapshot.SyncedUsers,
            snapshot.SyncedGroups,
            snapshot.CollectionDurationMilliseconds);

        return snapshot;
    }

    public async Task<MetricSnapshot?> GetLatestSuccessfulSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MetricSnapshots
            .Where(s => s.CollectionSucceeded)
            .OrderByDescending(s => s.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<MetricSnapshot>> GetSuccessfulSnapshotsAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.MetricSnapshots
            .Where(s => s.CollectionSucceeded && s.CapturedAtUtc >= startUtc && s.CapturedAtUtc <= endUtc)
            .OrderBy(s => s.CapturedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<MetricSnapshot>> GetRecentFailuresAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        return await _context.MetricSnapshots
            .Where(s => !s.CollectionSucceeded)
            .OrderByDescending(s => s.CapturedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

