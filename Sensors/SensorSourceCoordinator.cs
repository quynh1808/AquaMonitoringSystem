namespace AquacultureMonitoring.Sensors;

public sealed class SensorSourceCoordinator : BackgroundService
{
    private readonly IEnumerable<ISensorDataSource> _sources;
    private readonly DataSourceHealthService _health;
    private readonly ILogger<SensorSourceCoordinator> _logger;

    public SensorSourceCoordinator(IEnumerable<ISensorDataSource> sources, DataSourceHealthService health,
        ILogger<SensorSourceCoordinator> logger)
    {
        _sources = sources;
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var allSources = _sources.ToList();
        foreach (var source in allSources)
            await _health.SetEnabledAsync(source.SourceName, source.IsEnabled, stoppingToken);
        var enabled = allSources.Where(x => x.IsEnabled).ToList();
        _logger.LogInformation("Khởi động {Count} nguồn cảm biến: {Sources}.", enabled.Count,
            string.Join(", ", enabled.Select(x => x.SourceName)));
        await Task.WhenAll(enabled.Select(x => x.RunAsync(stoppingToken)));
    }
}
