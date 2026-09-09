using _365MigrationTracker.Configuration;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace _365MigrationTracker.Services;

/// <summary>
/// Delegated implementation of <see cref="IGraphAccessProvider"/>: acquires Graph tokens
/// for the signed-in user via Microsoft.Identity.Web, and reads their tenant from the
/// authentication state.
/// </summary>
public class DelegatedGraphAccessProvider : IGraphAccessProvider
{
    // Entra puts the tenant id in this claim; "tid" is the short form seen on raw tokens.
    private const string TenantIdClaim = "http://schemas.microsoft.com/identity/claims/tenantid";
    private const string ShortTenantIdClaim = "tid";

    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly GraphOptions _options;
    private readonly ILogger<DelegatedGraphAccessProvider> _logger;

    public DelegatedGraphAccessProvider(
        ITokenAcquisition tokenAcquisition,
        AuthenticationStateProvider authenticationStateProvider,
        IOptions<GraphOptions> options,
        ILogger<DelegatedGraphAccessProvider> logger)
    {
        _tokenAcquisition = tokenAcquisition;
        _authenticationStateProvider = authenticationStateProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Deliberately not wrapped in try/catch. MicrosoftIdentityWebChallengeUserException
        // must reach the component so the consent handler can redirect the user.
        return await _tokenAcquisition.GetAccessTokenForUserAsync(
            _options.Scopes,
            user: await GetUserAsync());
    }

    public async Task<string?> GetTenantIdAsync()
    {
        var user = await GetUserAsync();

        return user?.FindFirst(TenantIdClaim)?.Value
            ?? user?.FindFirst(ShortTenantIdClaim)?.Value;
    }

    public async Task<string?> GetUserDisplayNameAsync()
    {
        var user = await GetUserAsync();

        return user?.FindFirst("name")?.Value
            ?? user?.FindFirst(ClaimTypes.Name)?.Value
            ?? user?.Identity?.Name;
    }

    public async Task<string?> GetUserPhotoDataUriAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(cancellationToken);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/photo/$value");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // 404 is the common, expected case: the account has no photo.
                // Anything else is logged so a permissions problem is visible rather
                // than silently presenting as "nobody has a photo".
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation(
                        "Profile photo unavailable ({StatusCode}); falling back to an icon",
                        (int)response.StatusCode);
                }

                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0) return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex)
        {
            // Deliberately swallowed, including a re-authentication challenge. This runs
            // from the layout on every page; letting it redirect or throw would turn a
            // missing avatar into a broken app.
            _logger.LogInformation(ex, "Could not load profile photo; falling back to an icon");
            return null;
        }
    }

    private async Task<ClaimsPrincipal?> GetUserAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User?.Identity?.IsAuthenticated == true ? state.User : null;
    }
}
