namespace _365MigrationTracker.Models;

/// <summary>
/// Represents calculated progress for a declining metric.
/// </summary>
public class ProgressMetrics
{
    /// <summary>
    /// Current count of the metric.
    /// </summary>
    public int CurrentCount { get; set; }

    /// <summary>
    /// Previous count from the last snapshot, or null if no previous snapshot exists.
    /// </summary>
    public int? PreviousCount { get; set; }

    /// <summary>
    /// Absolute change since previous snapshot.
    /// Negative = progress (declining), Positive = regress (increasing).
    /// </summary>
    public int? AbsoluteChange { get; set; }

    /// <summary>
    /// Percentage change since previous snapshot.
    /// Null if PreviousCount is null or zero.
    /// </summary>
    public decimal? PercentageChange { get; set; }

    /// <summary>
    /// The baseline count when migration started.
    /// </summary>
    public int BaselineCount { get; set; }

    /// <summary>
    /// Percentage migrated since baseline.
    /// Formula: ((Baseline - Current) / Baseline) * 100
    /// Null if BaselineCount is zero.
    /// </summary>
    public decimal? PercentageMigrated { get; set; }

    /// <summary>
    /// Display-friendly absolute change label.
    /// </summary>
    public string AbsoluteChangeLabel
    {
        get
        {
            if (!AbsoluteChange.HasValue) return "—";
            var change = AbsoluteChange.Value;
            if (change == 0) return "No change";
            return change < 0 ? $"↓ {Math.Abs(change)}" : $"↑ {change}";
        }
    }

    /// <summary>
    /// Display-friendly percentage change label.
    /// </summary>
    public string PercentageChangeLabel
    {
        get
        {
            if (!PercentageChange.HasValue) return "—";
            var pct = PercentageChange.Value;
            if (pct == 0) return "0%";
            return pct < 0 ? $"↓ {Math.Abs(pct):F1}%" : $"↑ {pct:F1}%";
        }
    }

    /// <summary>
    /// Display-friendly percentage migrated label.
    /// </summary>
    public string PercentageMigratedLabel
    {
        get
        {
            if (!PercentageMigrated.HasValue) return "—";
            return $"{PercentageMigrated:F1}%";
        }
    }

    /// <summary>
    /// Indicates if this metric shows progress (declining count).
    /// </summary>
    public bool IsProgressingWell
    {
        get
        {
            if (!AbsoluteChange.HasValue) return false;
            return AbsoluteChange.Value <= 0;
        }
    }
}
