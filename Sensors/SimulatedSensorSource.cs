using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

public sealed class SimulatedSensorSource : ISensorDataSource
{
    private readonly SensorMessageBus _bus;
    private readonly FarmRuntimeState _runtime;
    private readonly DataSourceHealthService _health;
    private readonly IOptionsMonitor<SimulationProfileOptions> _profiles;
    private readonly IOptionsMonitor<SensorConnectionOptions> _connections;
    private readonly Dictionary<string, double> _currentValues = new(StringComparer.OrdinalIgnoreCase);
    private Random _random;

    public SimulatedSensorSource(
        SensorMessageBus bus,
        FarmRuntimeState runtime,
        DataSourceHealthService health,
        IOptionsMonitor<SimulationProfileOptions> profiles,
        IOptionsMonitor<SensorConnectionOptions> connections)
    {
        _bus = bus;
        _runtime = runtime;
        _health = health;
        _profiles = profiles;
        _connections = connections;
        _random = new Random(profiles.CurrentValue.RandomSeed);
    }

    public string SourceName => "simulation";
    public bool IsEnabled => _profiles.CurrentValue.Enabled &&
        _connections.CurrentValue.ActiveSources.Contains(SourceName, StringComparer.OrdinalIgnoreCase);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled) return;
        await _health.MarkConnectedAsync(SourceName, true, cancellationToken);

        while (!_runtime.IsInitialized && !cancellationToken.IsCancellationRequested)
            await Task.Delay(100, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var options = _profiles.CurrentValue;
            foreach (var sensor in _runtime.Sensors.Where(x => x.IsEnabled && (x.SourceType == "simulation" || x.SourceType == "manual-simulation")))
            {
                if (!options.Metrics.TryGetValue(sensor.Metric, out var profile)) continue;

                var pondOffset = options.PondMetricOffsets.TryGetValue(sensor.PondId, out var offsets) &&
                                 offsets.TryGetValue(sensor.Metric, out var configuredOffset)
                    ? configuredOffset
                    : 0;
                var current = _currentValues.TryGetValue(sensor.Id, out var last)
                    ? last
                    : profile.BaseValue + pondOffset;
                var hour = DateTime.Now.TimeOfDay.TotalHours;
                var dailyWave = Math.Sin((hour / 24d) * Math.PI * 2d) * profile.DailyAmplitude;
                var target = profile.BaseValue + pondOffset + dailyWave;
                var correction = (target - current) * profile.TargetCorrectionFactor;
                var randomStep = NextSigned(profile.MaxStep);
                var anomaly = _random.NextDouble() < profile.AnomalyProbability
                    ? NextSigned(profile.AnomalyAmplitude)
                    : 0;
                var next = Math.Clamp(current + correction + randomStep + anomaly, profile.MinValue, profile.MaxValue);
                next = Math.Round(next, profile.DecimalPlaces, MidpointRounding.AwayFromZero);
                _currentValues[sensor.Id] = next;

                await _bus.PublishAsync(new SensorEnvelope
                {
                    SensorId = sensor.Id,
                    PondId = sensor.PondId,
                    Metric = sensor.Metric,
                    Unit = sensor.Unit,
                    Value = next,
                    TimestampUtc = DateTime.UtcNow,
                    SourceType = SourceName,
                    SourceId = SourceName,
                    SourceAddress = $"simulation://{sensor.PondId}/{sensor.Metric}",
                    Quality = "good",
                    BatteryLevel = options.DefaultBatteryLevel,
                    SignalStrength = options.DefaultSignalStrength,
                    FirmwareVersion = options.FirmwareVersion
                }, cancellationToken);
            }

            await Task.Delay(Math.Max(250, options.IntervalMilliseconds), cancellationToken);
        }
    }

    private double NextSigned(double amplitude) => (_random.NextDouble() * 2d - 1d) * amplitude;
}
