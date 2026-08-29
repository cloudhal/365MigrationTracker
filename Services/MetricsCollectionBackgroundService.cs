using _365MigrationTracker.Configuration;
using Microsoft.Extensions.Options;

namespace _365MigrationTracker.Services;

/// <summary>
/// Background service that performs scheduled metrics collection at configured intervals.
/// Respects application cancellation and shuts down cleanly.
/// </summary>
public class MetricsCollectionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsCollectionBackgroundService> _logger;
    private readonly CollectionOptions _options;

    public MetricsCollectionBackgroundService(
        IServiceProvider serviceProvider,
        IOptionsMonitor<CollectionOptions> optionsMonitor,
        ILogger<MetricsCollectionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = optionsMonitor.CurrentValue ?? throw new ArgumentNullException(nameof(optionsMonitor));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Metrics collection background service is disabled in configuration");
            return;
        }

        _logger.LogInformation(
            "Metrics collection background service starting. Interval: {IntervalHours} hours, Source: {Source}",
            _options.IntervalHours, _options.Source);

        // Initial delay before first collection (gives app time to stabilize)
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var collectionService = scope.ServiceProvider.GetRequiredService<MetricsCollectionService>();
                    await collectionService.CollectAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Metrics collection background service cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in metrics collection background service");
            }

            // Wait for the configured interval
            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background service delay cancelled");
                break;
            }
        }

        _logger.LogInformation("Metrics collection background service stopped");
    }
}
