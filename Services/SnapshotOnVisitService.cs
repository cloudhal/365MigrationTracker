using _365MigrationTracker.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using System.Collections.Concurrent;

namespace _365MigrationTracker.Services;

/// <summary>
/// Records a snapshot when someone opens the dashboard, replacing the old background
/// collection service.
///
/// The background service could not survive the move to delegated Graph access: it ran
/// on a timer with nobody signed in, and there is no token to call Graph with in that
/// situation. History is therefore opportunistic - it accumulates while people use the
/// app, and there are gaps on days nobody visits.
///
/// Collection is throttled per tenant so that refreshing the page, or several people
/// opening it at once, does not write a burst of near-identical rows.
/// </summary>
public class SnapshotOnVisitService
{
    // Last successful collection per tenant. Static so it survives the per-circuit
    // lifetime of this scoped service. Fine for a single instance; if the app is ever
    // scaled out, each instance throttles independently and the worst case is one
    // extra row per instance, which is harmless.
    private static readonly ConcurrentDictionary<string, DateTime> LastCollectedUtc = new();

    private readonly MetricsCollectionService _collectionService;
    private readonly IGraphAccessProvider _graphAccess;
    private readonly CollectionOptions _options;
    private readonly ILogger<SnapshotOnVisitService> _logger;

    public SnapshotOnVisitService(
        MetricsCollectionService collectionService,
        IGraphAccessProvider graphAccess,
        IOptions<CollectionOptions> options,
        ILogger<SnapshotOnVisitService> logger)
    {
        _collectionService = collectionService;
        _graphAccess = graphAccess;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Collects a snapshot if this tenant's last one is older than the configured
    /// interval. Returns true if a snapshot was written.
    /// </summary>
    public async Task<bool> CollectIfDueAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return false;

        var tenantId = await _graphAccess.GetTenantIdAsync();
        if (string.IsNullOrWhiteSpace(tenantId)) return false;

        var interval = TimeSpan.FromHours(_options.IntervalHours);

        if (LastCollectedUtc.TryGetValue(tenantId, out var last)
            && DateTime.UtcNow - last < interval)
        {
            return false;
        }

        // Claim the slot before collecting, so two simultaneous visitors do not both
        // start a collection. MetricsCollectionService also refuses overlapping runs.
        LastCollectedUtc[tenantId] = DateTime.UtcNow;

        try
        {
            var snapshot = await _collectionService.CollectAsync(cancellationToken);

            if (snapshot is null)
            {
                // Another collection was already running; let the next visit try again.
                LastCollectedUtc.TryRemove(tenantId, out _);
                return false;
            }

            _logger.LogInformation("Visit-triggered snapshot recorded for tenant {TenantId}", tenantId);
            return true;
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            // Must reach the component, which redirects the user to re-authenticate.
            // Swallowing it here leaves them apparently signed in but unable to read
            // anything, with no way to recover short of clearing cookies.
            LastCollectedUtc.TryRemove(tenantId, out _);
            throw;
        }
        catch (Exception ex)
        {
            // Never let a failed background-ish collection break the page render.
            // Roll back the throttle so the next visit retries rather than waiting
            // out the full interval after a transient failure.
            LastCollectedUtc.TryRemove(tenantId, out _);
            _logger.LogError(ex, "Visit-triggered collection failed for tenant {TenantId}", tenantId);
            return false;
        }
    }
}
