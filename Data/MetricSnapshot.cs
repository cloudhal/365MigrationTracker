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
    /// The UTC timestamp when this snapshot was captured.
    /// </summary>
    public DateTime CapturedAtUtc { get; set; }

    /// <summary>
    /// Number of users still synchronized from on-premises AD.
    /// Must be non-negative.
    /// </summary>
    public int SyncedUsers { get; set; }

    /// <summary>
    /// Number of groups still synchronized from on-premises AD.
    /// Must be non-negative.
    /// </summary>
    public int SyncedGroups { get; set; }

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

