namespace _365MigrationTracker.Models;

/// <summary>
/// Result of a metrics collection attempt.
/// </summary>
public class CollectionResult
{
    /// <summary>
    /// True if collection succeeded; false otherwise.
    /// </summary>
    public required bool Succeeded { get; set; }

    /// <summary>
    /// The collected metrics, if successful.
    /// </summary>
    public MigrationMetrics? Metrics { get; set; }

    /// <summary>
    /// Error message if collection failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Duration of the collection attempt in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }
}

