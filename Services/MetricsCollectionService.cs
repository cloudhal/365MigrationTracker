using _365MigrationTracker.Configuration;
using _365MigrationTracker.Data;
using _365MigrationTracker.Models;
using Microsoft.Extensions.Options;

namespace _365MigrationTracker.Services;

/// <summary>
/// Orchestrates metrics collection from a source and persistence to storage.
/// Provides both automated and manual collection with concurrency protection.
/// </summary>
public class MetricsCollectionService
{
    private readonly IMigrationMetricsSource _metricsSource;
    private readonly IMetricSnapshotStore _snapshotStore;
    private readonly IGraphAccessProvider _graphAccess;
    private readonly ILogger<MetricsCollectionService> _logger;
    private readonly string _sourceName;
    private SemaphoreSlim _collectionLock = new(1, 1);
    private bool _collectionInProgress = false;

    public MetricsCollectionService(
        IMigrationMetricsSource metricsSource,
        IMetricSnapshotStore snapshotStore,
        IGraphAccessProvider graphAccess,
        IOptions<CollectionOptions> options,
        ILogger<MetricsCollectionService> logger)
    {
        _metricsSource = metricsSource ?? throw new ArgumentNullException(nameof(metricsSource));
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _graphAccess = graphAccess ?? throw new ArgumentNullException(nameof(graphAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Stamped onto every row so a later trend can tell real collections from simulated ones
        _sourceName = options?.Value?.Source ?? "Unknown";
    }

    /// <summary>
    /// Indicates whether a collection is currently in progress.
    /// </summary>
    public bool IsCollectionInProgress => _collectionInProgress;

    /// <summary>
    /// Performs a collection cycle: retrieve metrics, measure duration, store result, and log.
    /// Prevents overlapping collection runs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved snapshot, or null if collection was already in progress.</returns>
    public async Task<MetricSnapshot?> CollectAsync(CancellationToken cancellationToken = default)
    {
        // Prevent overlapping collections
        if (!await _collectionLock.WaitAsync(0))
        {
            _logger.LogWarning("Collection requested but another collection is already in progress");
            return null;
        }

        try
        {
            _collectionInProgress = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Collection only makes sense in the context of a signed-in user: the Graph
            // call uses their delegated token and the row is attributed to their tenant.
            var tenantId = await _graphAccess.GetTenantIdAsync();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                _logger.LogWarning("Collection requested with no signed-in user; nothing collected");
                return null;
            }

            _logger.LogInformation("Starting metrics collection for tenant {TenantId}", tenantId);

            // Get metrics from source
            var result = await _metricsSource.GetMetricsAsync(cancellationToken);

            sw.Stop();

            // Create snapshot
            var snapshot = new MetricSnapshot
            {
                CapturedAtUtc = DateTime.UtcNow,
                TenantId = tenantId,
                Source = _sourceName,
                CollectionSucceeded = result.Succeeded,
                CollectionDurationMilliseconds = result.DurationMilliseconds,
                ErrorMessage = result.ErrorMessage,
                SyncedUsers = result.Metrics?.SyncedUsers ?? 0,
                MigratedUsers = result.Metrics?.MigratedUsers ?? 0,
                SyncedGroups = result.Metrics?.SyncedGroups ?? 0,
                MigratedGroups = result.Metrics?.MigratedGroups ?? 0,
                CloudOnlyGroups = result.Metrics?.CloudOnlyGroups ?? 0,
                HybridDevices = result.Metrics?.HybridDevices ?? 0,
                PendingHybridDevices = result.Metrics?.PendingHybridDevices ?? 0,
                EntraJoinedDevices = result.Metrics?.EntraJoinedDevices ?? 0,
                TotalUsers = result.Metrics?.TotalUsers ?? 0,
                TotalGroups = result.Metrics?.TotalGroups ?? 0,
                TotalDevices = result.Metrics?.TotalDevices ?? 0
            };

            // Store snapshot
            var saved = await _snapshotStore.SaveSnapshotAsync(snapshot, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Collection succeeded: Users={Users}/{TotalUsers}, Groups={Groups}/{TotalGroups}, Hybrid={Hybrid}, Entra={Entra}/{TotalDevices}, Duration={DurationMs}ms",
                    saved.SyncedUsers, saved.TotalUsers, saved.SyncedGroups, saved.TotalGroups,
                    saved.HybridDevices, saved.EntraJoinedDevices, saved.TotalDevices, saved.CollectionDurationMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Collection failed: {Error}, Duration={DurationMs}ms",
                    result.ErrorMessage, result.DurationMilliseconds);
            }

            return saved;
        }
        finally
        {
            _collectionInProgress = false;
            _collectionLock.Release();
        }
    }
}
