using _365MigrationTracker.Models;
using Microsoft.Extensions.Options;
using _365MigrationTracker.Configuration;

namespace _365MigrationTracker.Services;

/// <summary>
/// Simulated implementation of IMigrationMetricsSource.
/// Produces realistic migration data that gradually transitions away from on-premises.
/// </summary>
public class SimulatedMetricsSource : IMigrationMetricsSource
{
    private readonly ILogger<SimulatedMetricsSource> _logger;
    private readonly Random _random;
    private static DateTime _lastCollectionTime = DateTime.UtcNow;
    private static MigrationMetrics _lastMetrics = new()
    {
        SyncedUsers = 5000,
        MigratedUsers = 0,
        SyncedGroups = 250,
        MigratedGroups = 0,
        CloudOnlyGroups = 50,
        HybridDevices = 3500,
        PendingHybridDevices = 150,
        EntraJoinedDevices = 1500
    };

    // Configuration for baseline and target values
    private const int BaselineSyncedUsers = 5000;
    private const int BaselineMigratedUsers = 0;
    private const int BaselineSyncedGroups = 250;
    private const int BaselineMigratedGroups = 0;
    private const int BaselineCloudOnlyGroups = 50;
    private const int BaselineHybridDevices = 3500;
    private const int BaselinePendingHybridDevices = 150;
    private const int BaselineEntraJoinedDevices = 1500;

    private const int TargetSyncedUsers = 0;
    private const int TargetMigratedUsers = 5000;
    private const int TargetSyncedGroups = 0;
    private const int TargetMigratedGroups = 250;
    private const int TargetCloudOnlyGroups = 50;
    private const int TargetHybridDevices = 500;
    private const int TargetPendingHybridDevices = 0;
    private const int TargetEntraJoinedDevices = 4000;

    // Tenant totals stay flat: objects move from on-premises-synced to cloud-only,
    // they aren't created or deleted.
    private const int TotalUsers = BaselineSyncedUsers + TargetMigratedUsers;
    private const int TotalGroups = BaselineSyncedGroups + BaselineCloudOnlyGroups;
    private const int TotalDevices = BaselineHybridDevices + BaselineEntraJoinedDevices;

    public SimulatedMetricsSource(ILogger<SimulatedMetricsSource> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    public async Task<CollectionResult> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Simulate some async work
            await Task.Delay(100, cancellationToken);

            // Generate realistic progression
            var metrics = GenerateMetrics();

            sw.Stop();

            _logger.LogInformation(
                "Simulated metrics generated: Users={SyncedUsers} synced, {MigratedUsers} migrated, Groups={Groups}, Hybrid={Hybrid}, Entra={Entra}, Duration={DurationMs}ms",
                metrics.SyncedUsers, metrics.MigratedUsers, metrics.SyncedGroups, metrics.HybridDevices, metrics.EntraJoinedDevices, sw.ElapsedMilliseconds);

            return new CollectionResult
            {
                Succeeded = true,
                Metrics = metrics,
                DurationMilliseconds = sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning("Simulated metrics collection was cancelled after {DurationMs}ms", sw.ElapsedMilliseconds);
            return new CollectionResult
            {
                Succeeded = false,
                ErrorMessage = "Collection was cancelled.",
                DurationMilliseconds = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Simulated metrics collection failed after {DurationMs}ms", sw.ElapsedMilliseconds);
            return new CollectionResult
            {
                Succeeded = false,
                ErrorMessage = $"Collection error: {ex.Message}",
                DurationMilliseconds = sw.ElapsedMilliseconds
            };
        }
    }

    private MigrationMetrics GenerateMetrics()
    {
        var now = DateTime.UtcNow;
        var hoursSinceLastCollection = (now - _lastCollectionTime).TotalHours;
        _lastCollectionTime = now;

        // Small random variations to make it realistic
        var changeRatePerHour = 0.005; // 0.5% change per hour
        var changeMultiplier = 1.0 - (changeRatePerHour * hoursSinceLastCollection);
        var variance = 0.98 + (_random.NextDouble() * 0.04); // ±2% variance

        var newSyncedUsers = Math.Max(TargetSyncedUsers, (int)(_lastMetrics.SyncedUsers * changeMultiplier * variance));
        var newMigratedUsers = Math.Min(TargetMigratedUsers, (int)(_lastMetrics.MigratedUsers / changeMultiplier / variance));
        var newSyncedGroups = Math.Max(TargetSyncedGroups, (int)(_lastMetrics.SyncedGroups * changeMultiplier * variance));
        var newMigratedGroups = Math.Min(TargetMigratedGroups, (int)(_lastMetrics.MigratedGroups / changeMultiplier / variance));

        var metrics = new MigrationMetrics
        {
            // Gradually decrease synced users and groups
            SyncedUsers = newSyncedUsers,
            MigratedUsers = newMigratedUsers,
            SyncedGroups = newSyncedGroups,
            MigratedGroups = newMigratedGroups,
            CloudOnlyGroups = TargetCloudOnlyGroups,

            // Gradually decrease hybrid devices
            HybridDevices = Math.Max(TargetHybridDevices, (int)(_lastMetrics.HybridDevices * changeMultiplier * variance)),

            // Gradually decrease pending hybrid devices
            PendingHybridDevices = Math.Max(TargetPendingHybridDevices, (int)(_lastMetrics.PendingHybridDevices * changeMultiplier * variance)),

            // Gradually increase Entra-joined devices
            EntraJoinedDevices = Math.Min(TargetEntraJoinedDevices, (int)(_lastMetrics.EntraJoinedDevices / changeMultiplier / variance)),

            TotalUsers = TotalUsers,
            TotalGroups = TotalGroups,
            TotalDevices = TotalDevices
        };

        // Ensure no negative values
        metrics = EnsureNonNegative(metrics);

        _lastMetrics = metrics;
        return metrics;
    }

    private MigrationMetrics EnsureNonNegative(MigrationMetrics metrics)
    {
        return new MigrationMetrics
        {
            SyncedUsers = Math.Max(0, metrics.SyncedUsers),
            MigratedUsers = Math.Max(0, metrics.MigratedUsers),
            SyncedGroups = Math.Max(0, metrics.SyncedGroups),
            MigratedGroups = Math.Max(0, metrics.MigratedGroups),
            CloudOnlyGroups = Math.Max(0, metrics.CloudOnlyGroups),
            HybridDevices = Math.Max(0, metrics.HybridDevices),
            PendingHybridDevices = Math.Max(0, metrics.PendingHybridDevices),
            EntraJoinedDevices = Math.Max(0, metrics.EntraJoinedDevices),
            TotalUsers = Math.Max(0, metrics.TotalUsers),
            TotalGroups = Math.Max(0, metrics.TotalGroups),
            TotalDevices = Math.Max(0, metrics.TotalDevices)
        };
    }
}
