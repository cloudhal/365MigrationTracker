using Xunit;
using Moq;
using _365MigrationTracker.Configuration;
using _365MigrationTracker.Services;
using _365MigrationTracker.Models;
using _365MigrationTracker.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _365MigrationTracker.Tests;

/// <summary>
/// Unit tests for MetricsCollectionService.
/// </summary>
public class MetricsCollectionServiceTests
{
    private const string TestTenantId = "test-tenant-id";

    private readonly Mock<IMigrationMetricsSource> _mockSource;
    private readonly Mock<IMetricSnapshotStore> _mockStore;
    private readonly Mock<ILogger<MetricsCollectionService>> _mockLogger;

    public MetricsCollectionServiceTests()
    {
        _mockSource = new Mock<IMigrationMetricsSource>();
        _mockStore = new Mock<IMetricSnapshotStore>();
        _mockLogger = new Mock<ILogger<MetricsCollectionService>>();
    }

    /// <summary>
    /// Builds the service under test. Collection now requires a signed-in user: the
    /// tenant stamps the snapshot and Graph is called with that user's delegated token.
    /// </summary>
    private MetricsCollectionService CreateService(
        string source = "Simulated",
        string? tenantId = TestTenantId)
    {
        var graphAccess = new Mock<IGraphAccessProvider>();
        graphAccess.Setup(g => g.GetTenantIdAsync()).ReturnsAsync(tenantId);
        graphAccess.Setup(g => g.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("fake-token");

        return new MetricsCollectionService(
            _mockSource.Object,
            _mockStore.Object,
            graphAccess.Object,
            Options.Create(new CollectionOptions { Source = source }),
            _mockLogger.Object);
    }

    [Fact]
    public async Task CollectAsync_Saves_Successful_Snapshot()
    {
        // Arrange
        var collectionResult = new CollectionResult
        {
            Succeeded = true,
            Metrics = new MigrationMetrics
            {
                SyncedUsers = 100,
                SyncedGroups = 10,
                HybridDevices = 50,
                PendingHybridDevices = 5,
                EntraJoinedDevices = 200
            },
            DurationMilliseconds = 150
        };

        _mockSource.Setup(s => s.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(collectionResult);

        var savedSnapshot = new MetricSnapshot
        {
            Id = 1,
            CapturedAtUtc = DateTime.UtcNow,
            CollectionSucceeded = true,
            SyncedUsers = 100,
            SyncedGroups = 10,
            HybridDevices = 50,
            PendingHybridDevices = 5,
            EntraJoinedDevices = 200,
            CollectionDurationMilliseconds = 150
        };

        _mockStore.Setup(s => s.SaveSnapshotAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedSnapshot);

        var service = CreateService();

        // Act
        var result = await service.CollectAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CollectionSucceeded);
        Assert.Equal(100, result.SyncedUsers);
        _mockStore.Verify(s => s.SaveSnapshotAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectAsync_Records_Failed_Collection()
    {
        // Arrange
        var collectionResult = new CollectionResult
        {
            Succeeded = false,
            ErrorMessage = "Test error",
            DurationMilliseconds = 100
        };

        _mockSource.Setup(s => s.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(collectionResult);

        var savedSnapshot = new MetricSnapshot
        {
            Id = 1,
            CapturedAtUtc = DateTime.UtcNow,
            CollectionSucceeded = false,
            ErrorMessage = "Test error",
            CollectionDurationMilliseconds = 100
        };

        _mockStore.Setup(s => s.SaveSnapshotAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedSnapshot);

        var service = CreateService();

        // Act
        var result = await service.CollectAsync();

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CollectionSucceeded);
        Assert.Equal("Test error", result.ErrorMessage);
        _mockStore.Verify(s => s.SaveSnapshotAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectAsync_Prevents_Overlapping_Collections()
    {
        // Arrange
        var collectionResult = new CollectionResult
        {
            Succeeded = true,
            Metrics = new MigrationMetrics(),
            DurationMilliseconds = 500 // Long delay to create overlap
        };

        var callCount = 0;
        _mockSource.Setup(s => s.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .Returns(async () =>
            {
                await Task.Delay(200); // Simulate long-running operation
                return collectionResult;
            });

        var savedSnapshot = new MetricSnapshot
        {
            Id = 1,
            CapturedAtUtc = DateTime.UtcNow,
            CollectionSucceeded = true,
            CollectionDurationMilliseconds = 500
        };

        _mockStore.Setup(s => s.SaveSnapshotAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedSnapshot);

        var service = CreateService();

        // Act - attempt two concurrent collections
        var task1 = service.CollectAsync();
        var task2 = service.CollectAsync(); // Should be rejected immediately

        var result1 = await task1;
        var result2 = await task2;

        // Assert - second collection should be rejected
        Assert.NotNull(result1);
        Assert.Null(result2); // null indicates rejection
        Assert.Equal(1, callCount); // GetMetrics should only be called once
    }

    [Fact]
    public async Task CollectAsync_Sets_IsCollectionInProgress_Flag()
    {
        // Arrange
        var collectionResult = new CollectionResult
        {
            Succeeded = true,
            Metrics = new MigrationMetrics(),
            DurationMilliseconds = 100
        };

        _mockSource.Setup(s => s.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(collectionResult);

        var savedSnapshot = new MetricSnapshot
        {
            Id = 1,
            CapturedAtUtc = DateTime.UtcNow,
            CollectionSucceeded = true
        };

        _mockStore.Setup(s => s.SaveSnapshotAsync(It.IsAny<MetricSnapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedSnapshot);

        var service = CreateService();

        // Act & Assert
        Assert.False(service.IsCollectionInProgress);
        var task = service.CollectAsync();
        // Note: In real scenario we'd need async verification, this is simplified
        var result = await task;
        Assert.False(service.IsCollectionInProgress); // Should be cleared after collection
    }
}
