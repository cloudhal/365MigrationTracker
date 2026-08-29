namespace _365MigrationTracker.Configuration;

/// <summary>
/// Configuration options for metrics collection.
/// </summary>
public class CollectionOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "MetricsCollection";

    /// <summary>
    /// Whether metrics collection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interval in hours between automated collection runs.
    /// Supports decimals for testing (e.g., 0.083 ≈ 5 minutes).
    /// </summary>
    public double IntervalHours { get; set; } = 6;

    /// <summary>
    /// Metrics source to use: "Simulated" or "Graph".
    /// </summary>
    public string Source { get; set; } = "Simulated";

    /// <summary>
    /// Threshold in hours for warning about stale data on the dashboard.
    /// </summary>
    public double StaleDataThresholdHours { get; set; } = 12;
}
