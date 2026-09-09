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
    /// Calculate progress for synced users.
    /// Migrated means no longer synced from on-premises AD, measured against the
    /// tenant's current user total.
    /// </summary>
    public ProgressMetrics CalculateSyncedUsersProgress(
        MetricSnapshot? current,
        MetricSnapshot? previous)
    {
        var synced = current?.SyncedUsers ?? 0;
        var total = current?.TotalUsers ?? 0;

        return CalculateProgress(
            synced,
            previous?.SyncedUsers,
            total,
            migratedCount: Math.Max(0, total - synced));
    }

    /// <summary>
    /// Calculate progress for synced groups.
    /// Migrated means no longer synced from on-premises AD, measured against the
    /// tenant's current group total.
    /// </summary>
    public ProgressMetrics CalculateSyncedGroupsProgress(
        MetricSnapshot? current,
        MetricSnapshot? previous)
    {
        var synced = current?.SyncedGroups ?? 0;
        var total = current?.TotalGroups ?? 0;

        return CalculateProgress(
            synced,
            previous?.SyncedGroups,
            total,
            migratedCount: Math.Max(0, total - synced));
    }

    /// <summary>
    /// Calculate progress for hybrid devices.
    /// Devices differ from users and groups: a device that has left hybrid join has not
    /// necessarily been migrated, because the tenant also holds devices that are neither
    /// hybrid nor Entra joined (workplace/registered). Only Entra joined counts as migrated.
    /// </summary>
    public ProgressMetrics CalculateHybridDevicesProgress(
        MetricSnapshot? current,
        MetricSnapshot? previous)
    {
        return CalculateProgress(
            current?.HybridDevices ?? 0,
            previous?.HybridDevices,
            current?.TotalDevices ?? 0,
            migratedCount: current?.EntraJoinedDevices ?? 0);
    }

    /// <summary>
    /// Generic progress calculation.
    /// </summary>
    /// <param name="current">Current count of the metric being tracked.</param>
    /// <param name="previous">Previous count, for change-over-time; null if unavailable.</param>
    /// <param name="total">Denominator for percentage migrated. Zero suppresses the percentage.</param>
    /// <param name="migratedCount">How many objects count as migrated.</param>
    private ProgressMetrics CalculateProgress(int current, int? previous, int total, int migratedCount)
    {
        var progress = new ProgressMetrics
        {
            CurrentCount = current,
            BaselineCount = total
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

        // Percentage migrated. Snapshots collected before totals were recorded have a
        // total of 0; leaving this null makes the dashboard hide the figure rather than
        // show a fabricated one.
        if (total > 0)
        {
            progress.PercentageMigrated = ((decimal)migratedCount / total) * 100;
            progress.PercentageMigrated = Math.Max(0, Math.Min(100, progress.PercentageMigrated.Value));
        }

        return progress;
    }
}
