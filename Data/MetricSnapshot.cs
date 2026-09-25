namespace _365MigrationTracker.Data;

/// <summary>
/// Represents a recorded snapshot of migration metrics at a point in time.
/// </summary>
public class MetricSnapshot
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Entra tenant this snapshot belongs to, taken from the signed-in user's token.
    ///
    /// This is a security boundary, not a label. The app is multi-tenant and every
    /// customer's rows share one table, so any query that reads snapshots MUST filter on
    /// this column or one customer will see another's figures.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The UTC timestamp when this snapshot was captured.
    /// </summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>
    /// Number of users still synchronized from on-premises AD.
    /// Must be non-negative.
    /// </summary>
    public int SyncedUsers { get; set; }

    /// <summary>
    /// Number of users who have been migrated from on-premises AD.
    /// These users have OnPremisesUserPrincipalName set but are no longer synced (onPremisesSyncEnabled is null/false).
    /// Must be non-negative.
    /// </summary>
    public int MigratedUsers { get; set; }

    /// <summary>
    /// Number of groups still synchronized from on-premises AD.
    /// Must be non-negative.
    /// </summary>
    public int SyncedGroups { get; set; }

    /// <summary>
    /// Number of groups that have been migrated from on-premises AD.
    /// These groups have OnPremisesSecurityIdentifier set but are no longer synced.
    /// Must be non-negative.
    /// </summary>
    public int MigratedGroups { get; set; }

    /// <summary>
    /// Number of groups that were never from on-premises AD (cloud-only).
    /// Filter: onPremisesSecurityIdentifier eq null
    /// Must be non-negative.
    /// </summary>
    public int CloudOnlyGroups { get; set; }

    /// <summary>
    /// Number of computers with Microsoft Entra hybrid join.
    /// Must be non-negative.
    /// </summary>
    public int HybridDevices { get; set; }

    /// <summary>
    /// Number of hybrid devices whose registration is still pending.
    /// Must be non-negative.
    /// </summary>
    public int PendingHybridDevices { get; set; }

    /// <summary>
    /// Number of devices with Microsoft Entra join.
    /// Must be non-negative.
    /// </summary>
    public int EntraJoinedDevices { get; set; }

    /// <summary>
    /// Total number of users in the tenant (synced + Entra-only).
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Total number of groups in the tenant (synced + Entra-only).
    /// </summary>
    public int TotalGroups { get; set; }

    /// <summary>
    /// Total number of devices in the tenant.
    /// </summary>
    public int TotalDevices { get; set; }

    /// <summary>
    /// Which metrics source produced this row - "Graph" or "Simulated".
    ///
    /// Recorded because the two are not comparable: mixing them in one trend produces a
    /// chart that looks like migration progress but is really the switch between sources.
    /// Rows written before this column existed carry "Unknown".
    /// </summary>
    public string Source { get; set; } = "Unknown";

    /// <summary>
    /// True if this snapshot represents a successful collection; false if it failed.
    /// </summary>
    public bool CollectionSucceeded { get; set; }

    /// <summary>
    /// Duration of the collection in milliseconds.
    /// </summary>
    public long CollectionDurationMilliseconds { get; set; }

    /// <summary>
    /// Error message if collection failed; null if successful.
    /// Must not contain any personal directory data.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

