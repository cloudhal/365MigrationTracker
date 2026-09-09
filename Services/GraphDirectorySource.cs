using _365MigrationTracker.Configuration;
using _365MigrationTracker.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace _365MigrationTracker.Services;

/// <summary>
/// Reads individual directory objects from Microsoft Graph for the drill-down grids.
///
/// Uses the REST endpoints directly for the same reason the metrics source does: counts
/// and paging behave predictably, and the ConsistencyLevel header is required for the
/// advanced queries. Results are returned to the caller and never persisted.
/// </summary>
public class GraphDirectorySource : IDirectorySource
{
    private readonly IGraphAccessProvider _graphAccess;
    private readonly GraphOptions _options;
    private readonly ILogger<GraphDirectorySource> _logger;

    /// <summary>Graph's maximum page size for directory collections.</summary>
    private const int PageSize = 999;

    /// <summary>Stops a malformed nextLink chain from looping forever.</summary>
    private const int MaxPages = 50;

    public GraphDirectorySource(
        IGraphAccessProvider graphAccess,
        IOptions<GraphOptions> options,
        ILogger<GraphDirectorySource> logger)
    {
        _graphAccess = graphAccess;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DirectoryUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://graph.microsoft.com/v1.0/users" +
                  $"?$select=displayName,userPrincipalName,onPremisesSyncEnabled,accountEnabled" +
                  $"&$top={PageSize}";

        return await FetchAllPagesAsync(url, "users", item => new DirectoryUser
        {
            DisplayName = GetString(item, "displayName"),
            UserPrincipalName = GetString(item, "userPrincipalName"),
            OnPremisesSyncEnabled = GetBool(item, "onPremisesSyncEnabled"),
            AccountEnabled = GetBool(item, "accountEnabled")
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DirectoryGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://graph.microsoft.com/v1.0/groups" +
                  $"?$select=displayName,mail,onPremisesSyncEnabled,securityEnabled" +
                  $"&$top={PageSize}";

        return await FetchAllPagesAsync(url, "groups", item => new DirectoryGroup
        {
            DisplayName = GetString(item, "displayName"),
            Mail = GetNullableString(item, "mail"),
            OnPremisesSyncEnabled = GetBool(item, "onPremisesSyncEnabled"),
            SecurityEnabled = GetBool(item, "securityEnabled")
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DirectoryDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        // Windows only, matching the dashboard's device counts. If this listed every
        // device, the detail page's own chart - which is built from this list - would
        // disagree with the donut the user clicked through from.
        var filter = Uri.EscapeDataString("operatingSystem eq 'Windows'");

        var url = $"https://graph.microsoft.com/v1.0/devices" +
                  $"?$filter={filter}" +
                  $"&$select=displayName,operatingSystem,operatingSystemVersion,trustType," +
                  $"registrationDateTime,approximateLastSignInDateTime" +
                  $"&$top={PageSize}";

        return await FetchAllPagesAsync(url, "devices", item => new DirectoryDevice
        {
            DisplayName = GetString(item, "displayName"),
            OperatingSystem = GetNullableString(item, "operatingSystem"),
            OperatingSystemVersion = GetNullableString(item, "operatingSystemVersion"),
            TrustType = GetNullableString(item, "trustType"),
            RegistrationDateTime = GetDate(item, "registrationDateTime"),
            ApproximateLastSignInDateTime = GetDate(item, "approximateLastSignInDateTime")
        }, cancellationToken);
    }

    /// <summary>
    /// Walks the @odata.nextLink chain, projecting each item through <paramref name="map"/>.
    /// </summary>
    private async Task<IReadOnlyList<T>> FetchAllPagesAsync<T>(
        string url,
        string resourceName,
        Func<JsonElement, T> map,
        CancellationToken cancellationToken)
    {
        var results = new List<T>();

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        var token = await GetGraphTokenAsync();
        var nextUrl = url;
        var pages = 0;

        while (!string.IsNullOrEmpty(nextUrl) && pages < MaxPages)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("ConsistencyLevel", "eventual");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(content);

            if (json.RootElement.TryGetProperty("value", out var value))
            {
                foreach (var item in value.EnumerateArray())
                {
                    results.Add(map(item));
                }
            }

            nextUrl = json.RootElement.TryGetProperty("@odata.nextLink", out var link)
                ? link.GetString()
                : null;

            pages++;
        }

        if (pages >= MaxPages && !string.IsNullOrEmpty(nextUrl))
        {
            _logger.LogWarning(
                "Stopped reading {Resource} at the {MaxPages}-page cap; the grid is showing a partial list of {Count}.",
                resourceName, MaxPages, results.Count);
        }
        else
        {
            _logger.LogInformation("Read {Count} {Resource} from Graph across {Pages} page(s)",
                results.Count, resourceName, pages);
        }

        return results;
    }

    private Task<string> GetGraphTokenAsync() => _graphAccess.GetAccessTokenAsync();

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? GetNullableString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // Graph omits or nulls onPremisesSyncEnabled for cloud-only objects rather than
    // returning false, so anything that is not explicitly true counts as false.
    private static bool GetBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? GetDate(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
}
