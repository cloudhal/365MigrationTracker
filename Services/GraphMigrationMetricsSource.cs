using _365MigrationTracker.Configuration;
using _365MigrationTracker.Models;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using System.Diagnostics;

namespace _365MigrationTracker.Services;

/// <summary>
/// Microsoft Graph implementation of IMigrationMetricsSource.
///
/// Calls Graph with the signed-in user's delegated token, so the counts are always for
/// that user's own tenant. There is no configured tenant and no app-only credential.
///
/// A failed query fails the whole collection. None of the individual count methods
/// substitutes a zero on error: a fabricated zero is indistinguishable from a real one
/// once stored, and would be saved as a successful snapshot that corrupts the trend.
/// Better a recorded failure with an error message than a plausible-looking lie.
/// </summary>
public class GraphMigrationMetricsSource : IMigrationMetricsSource
{
    /// <summary>
    /// Device counts are restricted to Windows.
    ///
    /// Hybrid join and Entra join are Windows concepts; phones and tablets are enrolled,
    /// not joined, and can never move between those states. Counting them in the
    /// denominator made the migration look far less advanced than it is - against this
    /// tenant it was 29 of 405 (7.2%) rather than 29 of 220 (13.2%), with the difference
    /// being iPhones and Android devices that were never in scope.
    ///
    /// This narrows the total only: every hybrid joined and Entra joined device in the
    /// tenant already had operatingSystem 'Windows', so those two counts are unchanged.
    /// </summary>
    private const string WindowsOnly = "operatingSystem eq 'Windows'";

    private readonly IGraphAccessProvider _graphAccess;
    private readonly GraphOptions _options;
    private readonly ILogger<GraphMigrationMetricsSource> _logger;

    public GraphMigrationMetricsSource(
        IGraphAccessProvider graphAccess,
        IOptions<GraphOptions> options,
        ILogger<GraphMigrationMetricsSource> logger)
    {
        _graphAccess = graphAccess;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets current migration metrics from Microsoft Graph API.
    /// </summary>
    public async Task<CollectionResult> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting Graph metrics collection for the signed-in user's tenant");

            // Acquire the token once, before any queries.
            //
            // If the user's token cache has gone (app restart with in-memory caches, an
            // expired refresh token, or a new consent requirement) this throws here rather
            // than failing eight separate queries, each retried three times with backoff -
            // which is what turned a re-authentication into a silent 21-second wait
            // followed by a dashboard full of zeros.
            await _graphAccess.GetAccessTokenAsync(cancellationToken);

            // Collect synced metrics
            var syncedUsers = await GetSyncedUsersCountAsync(cancellationToken);
            var migratedUsers = await GetMigratedUsersCountAsync(cancellationToken);
            var syncedGroups = await GetSyncedGroupsCountAsync(cancellationToken);
            var cloudOnlyGroups = await GetCloudOnlyGroupsCountAsync(cancellationToken);
            var hybridDevices = await GetHybridDevicesCountAsync(cancellationToken);
            var entraJoined = await GetEntraJoinedDevicesCountAsync(cancellationToken);

            // Collect total counts
            var totalUsers = await GetTotalUsersCountAsync(cancellationToken);
            var totalGroups = await GetTotalGroupsCountAsync(cancellationToken);
            var totalDevices = await GetTotalDevicesCountAsync(cancellationToken);

            stopwatch.Stop();

            // Calculate migrated groups: those with on-prem origin minus those still synced
            var onPremOriginGroups = totalGroups - cloudOnlyGroups;
            var migratedGroups = Math.Max(0, onPremOriginGroups - syncedGroups);

            var metrics = new MigrationMetrics
            {
                SyncedUsers = syncedUsers,
                MigratedUsers = migratedUsers,
                SyncedGroups = syncedGroups,
                MigratedGroups = migratedGroups,
                CloudOnlyGroups = cloudOnlyGroups,
                HybridDevices = hybridDevices,
                EntraJoinedDevices = entraJoined,
                TotalUsers = totalUsers,
                TotalGroups = totalGroups,
                TotalDevices = totalDevices
            };

            _logger.LogInformation(
                "✓ Users: {SyncedUsers} synced, {MigratedUsers} migrated | Groups: {Groups} | Hybrid: {Hybrid} | Entra: {Entra} ({Duration}ms)",
                metrics.SyncedUsers, metrics.MigratedUsers, metrics.SyncedGroups, metrics.HybridDevices,
                metrics.EntraJoinedDevices, stopwatch.ElapsedMilliseconds);

            return new CollectionResult
            {
                Succeeded = true,
                Metrics = metrics,
                DurationMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            // The user must sign in again or grant consent. This is not a collection
            // failure and must NOT be swallowed into a zeroed snapshot - the component
            // needs it so it can redirect them. See IGraphAccessProvider.
            stopwatch.Stop();
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMessage = $"Graph API collection failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);

            return new CollectionResult
            {
                Succeeded = false,
                ErrorMessage = errorMessage,
                DurationMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Gets count of users synchronized from on-premises AD.
    /// Filter: onPremisesSyncEnabled eq true
    /// </summary>
    private async Task<int> GetSyncedUsersCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    try
                    {
                        // Use REST API directly with ConsistencyLevel header for reliable count
                        var httpClient = new HttpClient();
                        var token = await GetGraphTokenAsync();
                        var filter = Uri.EscapeDataString("onPremisesSyncEnabled eq true");
                        var url = $"https://graph.microsoft.com/v1.0/users?$filter={filter}&$top=1&$count=true";

                        var request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        request.Headers.Add("ConsistencyLevel", "eventual");

                        var response = await httpClient.SendAsync(request, cancellationToken);
                        response.EnsureSuccessStatusCode();

                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        var json = System.Text.Json.JsonDocument.Parse(content);
                        var count = json.RootElement.GetProperty("@odata.count").GetInt32();
                        return count;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Inner exception in GetAsync for users");
                        throw;
                    }
                },
                "SyncedUsers",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting synced users count");
            // Rethrow rather than returning 0.
            //
            // A zero here would be indistinguishable from a genuine count and would be
            // written as a SUCCESSFUL snapshot, so the trend would show a cliff to zero
            // that never happened. Failing the whole collection records it as a failure,
            // which the dashboard excludes from trends and surfaces in Recent Failures.
            throw;
        }
    }

    /// <summary>
    /// Gets count of users who have been migrated from on-premises AD.
    /// Filter: onPremisesSyncEnabled eq false
    /// These users have been desynced after migration and retain OnPremisesImmutableId.
    /// </summary>
    private async Task<int> GetMigratedUsersCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var filter = Uri.EscapeDataString("onPremisesSyncEnabled eq false");
                    var url = $"https://graph.microsoft.com/v1.0/users?$filter={filter}&$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();
                    return count;
                },
                "MigratedUsers",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migrated users count");
            throw;
        }
    }

    /// <summary>
    /// Gets count of groups synchronized from on-premises AD.
    /// Filter: onPremisesSyncEnabled eq true
    /// </summary>
    private async Task<int> GetSyncedGroupsCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var filter = Uri.EscapeDataString("onPremisesSyncEnabled eq true");
                    var url = $"https://graph.microsoft.com/v1.0/groups?$filter={filter}&$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();

                    return count;
                },
                "SyncedGroups",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting synced groups count");
            // Rethrow rather than returning 0.
            //
            // A zero here would be indistinguishable from a genuine count and would be
            // written as a SUCCESSFUL snapshot, so the trend would show a cliff to zero
            // that never happened. Failing the whole collection records it as a failure,
            // which the dashboard excludes from trends and surfaces in Recent Failures.
            throw;
        }
    }

    /// <summary>
    /// Gets count of groups that were never synced from on-premises AD (cloud-only).
    /// Filter: onPremisesSecurityIdentifier eq null
    /// </summary>
    private async Task<int> GetCloudOnlyGroupsCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var filter = Uri.EscapeDataString("onPremisesSecurityIdentifier eq null");
                    var url = $"https://graph.microsoft.com/v1.0/groups?$filter={filter}&$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();

                    return count;
                },
                "CloudOnlyGroups",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cloud-only groups count");
            throw;
        }
    }

    /// <summary>
    /// Gets count of hybrid Azure AD joined devices.
    /// Filter: trustType eq 'ServerAd'
    /// </summary>
    private async Task<int> GetHybridDevicesCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var filter = Uri.EscapeDataString($"trustType eq 'ServerAd' and {WindowsOnly}");
                    var url = $"https://graph.microsoft.com/v1.0/devices?$filter={filter}&$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();

                    return count;
                },
                "HybridDevices",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hybrid devices count");
            // Rethrow rather than returning 0.
            //
            // A zero here would be indistinguishable from a genuine count and would be
            // written as a SUCCESSFUL snapshot, so the trend would show a cliff to zero
            // that never happened. Failing the whole collection records it as a failure,
            // which the dashboard excludes from trends and surfaces in Recent Failures.
            throw;
        }
    }

    /// <summary>
    /// Gets count of Entra-joined (cloud-only) devices.
    /// Filter: trustType eq 'AzureAd'
    /// </summary>
    private async Task<int> GetEntraJoinedDevicesCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var filter = Uri.EscapeDataString($"trustType eq 'AzureAd' and {WindowsOnly}");
                    var url = $"https://graph.microsoft.com/v1.0/devices?$filter={filter}&$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();

                    return count;
                },
                "EntraJoinedDevices",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Entra joined devices count");
            // Rethrow rather than returning 0.
            //
            // A zero here would be indistinguishable from a genuine count and would be
            // written as a SUCCESSFUL snapshot, so the trend would show a cliff to zero
            // that never happened. Failing the whole collection records it as a failure,
            // which the dashboard excludes from trends and surfaces in Recent Failures.
            throw;
        }
    }

    /// <summary>
    /// Gets total count of all users in the tenant (synced + Entra-only).
    /// </summary>
    private async Task<int> GetTotalUsersCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var url = "https://graph.microsoft.com/v1.0/users?$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();

                    return count;
                },
                "TotalUsers",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total users count");
            // Rethrow rather than returning 0.
            //
            // A zero here would be indistinguishable from a genuine count and would be
            // written as a SUCCESSFUL snapshot, so the trend would show a cliff to zero
            // that never happened. Failing the whole collection records it as a failure,
            // which the dashboard excludes from trends and surfaces in Recent Failures.
            throw;
        }
    }

    /// <summary>
    /// Gets total count of all groups in the tenant (synced + Entra-only).
    /// </summary>
    private async Task<int> GetTotalGroupsCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var url = "https://graph.microsoft.com/v1.0/groups?$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();

                    return count;
                },
                "TotalGroups",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total groups count");
            // Rethrow rather than returning 0.
            //
            // A zero here would be indistinguishable from a genuine count and would be
            // written as a SUCCESSFUL snapshot, so the trend would show a cliff to zero
            // that never happened. Failing the whole collection records it as a failure,
            // which the dashboard excludes from trends and surfaces in Recent Failures.
            throw;
        }
    }

    /// <summary>
    /// Gets total count of all devices in the tenant (hybrid + Entra-only).
    /// </summary>
    private async Task<int> GetTotalDevicesCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteGraphQueryWithRetryAsync(
                async () =>
                {
                    var httpClient = new HttpClient();
                    var token = await GetGraphTokenAsync();
                    var filter = Uri.EscapeDataString(WindowsOnly);
                    var url = $"https://graph.microsoft.com/v1.0/devices?$filter={filter}&$top=1&$count=true";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Headers.Add("ConsistencyLevel", "eventual");

                    var response = await httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var count = json.RootElement.GetProperty("@odata.count").GetInt32();

                    return count;
                },
                "TotalDevices",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total devices count");
            // Rethrow rather than returning 0.
            //
            // A zero here would be indistinguishable from a genuine count and would be
            // written as a SUCCESSFUL snapshot, so the trend would show a cliff to zero
            // that never happened. Failing the whole collection records it as a failure,
            // which the dashboard excludes from trends and surfaces in Recent Failures.
            throw;
        }
    }

    /// <summary>
    /// Gets a fresh access token for Graph API using client credentials.
    /// </summary>
    private Task<string> GetGraphTokenAsync() => _graphAccess.GetAccessTokenAsync();

    /// <summary>
    /// Executes a Graph API query with exponential backoff retry logic.
    /// Handles 429 (throttled) and transient errors.
    /// </summary>
    private async Task<int> ExecuteGraphQueryWithRetryAsync(
        Func<Task<int>> query,
        string metricName,
        CancellationToken cancellationToken)
    {
        int delay = _options.RetryDelayMilliseconds;

        for (int attempt = 1; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                return await query();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Query for {MetricName} was cancelled", metricName);
                throw;
            }
            catch (MicrosoftIdentityWebChallengeUserException)
            {
                // Needs the user to act. Retrying with backoff cannot help and only
                // delays telling them.
                throw;
            }
            catch (Exception ex) when (attempt < _options.MaxRetries)
            {
                // Log the error and retry
                _logger.LogWarning(
                    ex,
                    "Error querying {MetricName} (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms",
                    metricName, attempt, _options.MaxRetries, delay);

                await Task.Delay(delay, cancellationToken);
                delay = (int)Math.Min(delay * 2, 30000); // Cap at 30 seconds
            }
        }

        // All retries exhausted
        throw new InvalidOperationException(
            $"Failed to query {metricName} after {_options.MaxRetries} attempts");
    }
}
