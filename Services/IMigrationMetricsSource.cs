using _365MigrationTracker.Models;

namespace _365MigrationTracker.Services;

/// <summary>
/// Abstraction for retrieving migration metrics from a source.
/// This allows swapping between simulated and real Graph API implementations.
/// </summary>
public interface IMigrationMetricsSource
{
    /// <summary>
    /// Retrieves the current migration metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A CollectionResult indicating success or failure.</returns>
    Task<CollectionResult> GetMetricsAsync(CancellationToken cancellationToken = default);
}
