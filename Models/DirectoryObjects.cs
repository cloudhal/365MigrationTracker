namespace _365MigrationTracker.Models;

/// <summary>
/// Which side of the migration a directory object currently sits on.
/// </summary>
public enum DirectorySource
{
    /// <summary>Still synchronised from on-premises Active Directory.</summary>
    OnPremisesSynced,

    /// <summary>Exists only in Microsoft Entra ID.</summary>
    CloudOnly
}

/// <summary>
/// A user as shown on the drill-down grid.
/// Live view of Microsoft Graph - deliberately not persisted, so no directory
/// data about individuals is written to the local database.
/// </summary>
public class DirectoryUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public bool OnPremisesSyncEnabled { get; set; }
    public bool AccountEnabled { get; set; }

    public DirectorySource Source =>
        OnPremisesSyncEnabled ? DirectorySource.OnPremisesSynced : DirectorySource.CloudOnly;

    public string SourceLabel =>
        OnPremisesSyncEnabled ? "Synced from AD" : "Entra only";
}

/// <summary>
/// A group as shown on the drill-down grid.
/// </summary>
public class DirectoryGroup
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Mail { get; set; }
    public bool OnPremisesSyncEnabled { get; set; }
    public bool SecurityEnabled { get; set; }

    public DirectorySource Source =>
        OnPremisesSyncEnabled ? DirectorySource.OnPremisesSynced : DirectorySource.CloudOnly;

    public string SourceLabel =>
        OnPremisesSyncEnabled ? "Synced from AD" : "Entra only";
}

/// <summary>
/// A device as shown on the drill-down grid.
/// </summary>
public class DirectoryDevice
{
    public string DisplayName { get; set; } = string.Empty;
    public string? OperatingSystem { get; set; }
    public string? OperatingSystemVersion { get; set; }

    /// <summary>Raw Graph trustType: ServerAd, AzureAd, Workplace, or null.</summary>
    public string? TrustType { get; set; }

    public DateTimeOffset? RegistrationDateTime { get; set; }
    public DateTimeOffset? ApproximateLastSignInDateTime { get; set; }

    /// <summary>
    /// The join type in the same words the dashboard's device donut uses, so the
    /// grid and the chart can be reconciled by eye.
    /// </summary>
    public string JoinTypeLabel => TrustType switch
    {
        "ServerAd" => "Hybrid joined",
        "AzureAd" => "Entra joined",
        "Workplace" => "Workplace registered",
        _ => "Other"
    };
}
