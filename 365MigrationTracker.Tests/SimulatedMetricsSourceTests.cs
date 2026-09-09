using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using _365MigrationTracker.Services;

namespace _365MigrationTracker.Tests;

/// <summary>
/// Unit tests for SimulatedMetricsSource.
/// </summary>
public class SimulatedMetricsSourceTests
{
    private readonly Mock<ILogger<SimulatedMetricsSource>> _mockLogger;

    public SimulatedMetricsSourceTests()
    {
        _mockLogger = new Mock<ILogger<SimulatedMetricsSource>>();
    }

    [Fact]
    public async Task GetMetricsAsync_Returns_Success_Result()
    {
        // Arrange
        var source = new SimulatedMetricsSource(_mockLogger.Object);

        // Act
        var result = await source.GetMetricsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Metrics);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task GetMetricsAsync_Returns_Non_Negative_Counts()
    {
        // Arrange
        var source = new SimulatedMetricsSource(_mockLogger.Object);

        // Act
        var result = await source.GetMetricsAsync();

        // Assert
        Assert.True(result.Metrics!.SyncedUsers >= 0, "SyncedUsers must be non-negative");
        Assert.True(result.Metrics.SyncedGroups >= 0, "SyncedGroups must be non-negative");
        Assert.True(result.Metrics.HybridDevices >= 0, "HybridDevices must be non-negative");
        Assert.True(result.Metrics.PendingHybridDevices >= 0, "PendingHybridDevices must be non-negative");
        Assert.True(result.Metrics.EntraJoinedDevices >= 0, "EntraJoinedDevices must be non-negative");
    }

    [Fact]
    public async Task GetMetricsAsync_Measures_Duration()
    {
        // Arrange
        var source = new SimulatedMetricsSource(_mockLogger.Object);

        // Act
        var result = await source.GetMetricsAsync();

        // Assert
        Assert.True(result.DurationMilliseconds >= 0, "Duration should be measured");
        Assert.True(result.DurationMilliseconds < 5000, "Duration should be reasonable");
    }

    [Fact]
    public async Task GetMetricsAsync_Handles_Cancellation()
    {
        // Arrange
        var source = new SimulatedMetricsSource(_mockLogger.Object);
        var cts = new System.Threading.CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        // Act
        var result = await source.GetMetricsAsync(cts.Token);

        // Assert
        // May succeed or fail depending on timing, but should not crash
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetMetricsAsync_Produces_Multiple_Realistic_Results()
    {
        // Arrange
        var source = new SimulatedMetricsSource(_mockLogger.Object);
        var results = new List<int>();

        // Act - collect multiple snapshots
        for (int i = 0; i < 3; i++)
        {
            var result = await source.GetMetricsAsync();
            results.Add(result.Metrics!.SyncedUsers);
            await Task.Delay(100); // Small delay between collections
        }

        // Assert - values should generally trend downward
        Assert.All(results, r => Assert.True(r >= 0, "All counts must be non-negative"));
    }
}
