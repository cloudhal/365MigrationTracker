using _365MigrationTracker.Data;
using _365MigrationTracker.Models;

namespace _365MigrationTracker.Services;

/// <summary>
/// Service for calculating progress metrics from snapshots.
/// Handles edge cases like missing data, zero baselines, and temporary increases.
/// </summary>
public class ProgressCalculationService
{
    /// <summary>
    /// Baseline counts used as the starting point for migration progress.
    /// These represent the state at the beginning of migration.
    /// </summary>
    public class Baselines
    {
        public int SyncedUsers { get; set; } = 5000;
        public int SyncedGroups { get; set; } = 250;
        public int HybridDevices { get; set; } = 3500;
    }

    /// <summary>
    /// Calculate progress for synced users.
    /// </summary>
    public ProgressMetrics CalculateSyncedUsersProgress(
        MetricSnapshot? current,
        MetricSnapshot? previous,
        Baselines? baselines = null)
    {
        baselines ??= new Baselines();
        return CalculateProgress(
            current?.SyncedUsers ?? 0,
            previous?.SyncedUsers,
            baselines.SyncedUsers);
    }

    /// <summary>
    /// Calculate progress for synced groups.
    /// </summary>
    public ProgressMetrics CalculateSyncedGroupsProgress(
        MetricSnapshot? current,
        MetricSnapshot? previous,
        Baselines? baselines = null)
    {
        baselines ??= new Baselines();
        return CalculateProgress(
            current?.SyncedGroups ?? 0,
            previous?.SyncedGroups,
            baselines.SyncedGroups);
    }

    /// <summary>
    /// Calculate progress for hybrid devices.
    /// </summary>
    public ProgressMetrics CalculateHybridDevicesProgress(
        MetricSnapshot? current,
        MetricSnapshot? previous,
        Baselines? baselines = null)
    {
        baselines ??= new Baselines();
        return CalculateProgress(
            current?.HybridDevices ?? 0,
            previous?.HybridDevices,
            baselines.HybridDevices);
    }

    /// <summary>
    /// Generic progress calculation.
    /// </summary>
    private ProgressMetrics CalculateProgress(int current, int? previous, int baseline)
    {
        var progress = new ProgressMetrics
        {
            CurrentCount = current,
            BaselineCount = baseline
        };

        // Calculate absolute change
        if (previous.HasValue)
        {
            progress.PreviousCount = previous.Value;
            progress.AbsoluteChange = current - previous.Value;

            // Calculate percentage change
            if (previous.Value != 0)
            {
                progress.PercentageChange = ((decimal)(current - previous.Value) / previous.Value) * 100;
            }
        }

        // Calculate percentage migrated
        if (baseline > 0)
        {
            progress.PercentageMigrated = ((decimal)(baseline - current) / baseline) * 100;
            // Ensure it doesn't exceed 100% or go below 0%
            progress.PercentageMigrated = Math.Max(0, Math.Min(100, progress.PercentageMigrated.Value));
        }

        return progress;
    }
}
