namespace _365MigrationTracker.Models;

/// <summary>
/// Represents the current migration metrics snapshot data.
/// </summary>
public class MigrationMetrics
{
    /// <summary>
    /// Number of users still synchronized from on-premises AD.
    /// </summary>
    public int SyncedUsers { get; set; }

    /// <summary>
    /// Number of groups still synchronized from on-premises AD.
    /// </summary>
    public int SyncedGroups { get; set; }

    /// <summary>
    /// Number of groups that have been migrated from on-premises AD.
    /// These groups have OnPremisesSecurityIdentifier set but are no longer synced.
    /// </summary>
    public int MigratedGroups { get; set; }

    /// <summary>
    /// Number of groups that were never from on-premises AD (cloud-only).
    /// Filter: onPremisesSecurityIdentifier eq null
    /// </summary>
    public int CloudOnlyGroups { get; set; }

    /// <summary>
    /// Number of computers with Microsoft Entra hybrid join (trustType == "ServerAd" and registered).
    /// </summary>
    public int HybridDevices { get; set; }

    /// <summary>
    /// Number of hybrid devices whose registration is still pending (trustType == "ServerAd" and not registered).
    /// </summary>
    public int PendingHybridDevices { get; set; }

    /// <summary>
    /// Number of devices with Microsoft Entra join (trustType == "AzureAd").
    /// </summary>
    public int EntraJoinedDevices { get; set; }

    /// <summary>
    /// Total number of users in the tenant (synced + Entra-only).
    /// Used to calculate migration progress.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Total number of groups in the tenant (synced + Entra-only).
    /// Used to calculate migration progress.
    /// </summary>
    public int TotalGroups { get; set; }

    /// <summary>
    /// Total number of devices in the tenant.
    /// Used to calculate device migration progress.
    /// </summary>
    public int TotalDevices { get; set; }

    /// <summary>
    /// Number of users who have been migrated from on-premises AD.
    /// These users have OnPremisesUserPrincipalName set but are no longer synced (onPremisesSyncEnabled is null/false).
    /// </summary>
    public int MigratedUsers { get; set; }

    /// <summary>
    /// Calculated: Entra-only users (TotalUsers - SyncedUsers).
    /// </summary>
    public int EntraOnlyUsers => TotalUsers - SyncedUsers;

    /// <summary>
    /// Calculated: Entra-only groups (TotalGroups - SyncedGroups).
    /// </summary>
    public int EntraOnlyGroups => TotalGroups - SyncedGroups;

    /// <summary>
    /// Calculated: Entra-only devices (TotalDevices - HybridDevices).
    /// </summary>
    public int EntraOnlyDevices => TotalDevices - HybridDevices;
}

