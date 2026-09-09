namespace _365MigrationTracker.Configuration;

/// <summary>
/// Configuration options for Microsoft Graph calls.
///
/// Credentials are no longer here. Graph is called with delegated permissions as the
/// signed-in user, so the tenant, client id and secret all belong to the "AzureAd"
/// section consumed by Microsoft.Identity.Web. Nothing in this app pins a tenant -
/// the tenant is whichever one the signed-in user belongs to.
/// </summary>
public class GraphOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "GraphApi";

    /// <summary>
    /// Delegated scopes requested for Graph.
    ///
    /// Note these are delegated, not application, permissions: the effective access is
    /// the intersection of what the app was granted and what the signed-in user is
    /// allowed to see. A user without directory-read rights may get fewer results than
    /// an administrator, particularly for devices.
    /// </summary>
    public string[] Scopes { get; set; } =
    {
        "User.Read.All",
        "Group.Read.All",
        "Device.Read.All"
    };

    /// <summary>
    /// Maximum number of items to request per page from Graph.
    /// </summary>
    public int PageSize { get; set; } = 999;

    /// <summary>
    /// Maximum number of retry attempts for throttled or transient requests.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before the first retry; subsequent retries back off.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
