using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using AquacultureMonitoring.Services;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

public sealed class SensorIngestionWorker : BackgroundService
{
    private readonly SensorMessageBus _bus;
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly AlertEngineService _alerts;
    private readonly AutomationEngineService _automation;
    private readonly DataSourceHealthService _sourceHealth;
    private readonly IOptionsMonitor<SensorThresholdCatalog> _thresholds;
    private readonly IOptionsMonitor<SimulationProfileOptions> _simulation;
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;
    private readonly ILogger<SensorIngestionWorker> _logger;

    public SensorIngestionWorker(
        SensorMessageBus bus,
        FarmRuntimeState runtime,
        FarmRepository repository,
        AlertEngineService alerts,
        AutomationEngineService automation,
        DataSourceHealthService sourceHealth,
        IOptionsMonitor<SensorThresholdCatalog> thresholds,
        IOptionsMonitor<SimulationProfileOptions> simulation,
        IOptionsMonitor<SystemBehaviorOptions> behavior,
        ILogger<SensorIngestionWorker> logger)
    {
        _bus = bus;
        _runtime = runtime;
        _repository = repository;
        _alerts = alerts;
        _automation = automation;
        _sourceHealth = sourceHealth;
        _thresholds = thresholds;
        _simulation = simulation;
        _behavior = behavior;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!_runtime.IsInitialized && !stoppingToken.IsCancellationRequested)
            await Task.Delay(100, stoppingToken);

        await foreach (var envelope in _bus.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(envelope, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không xử lý được dữ liệu từ sensor {SensorId}.", envelope.SensorId);
            }
        }
    }

    private async Task ProcessAsync(SensorEnvelope envelope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.PondId) || string.IsNullOrWhiteSpace(envelope.Metric))
            throw new InvalidDataException("Sensor envelope thiếu PondId hoặc Metric.");
        if (!_thresholds.CurrentValue.Metrics.TryGetValue(envelope.Metric, out var threshold))
            throw new InvalidDataException($"Metric '{envelope.Metric}' chưa có trong sensor-thresholds.json.");
        if (_runtime.GetPond(envelope.PondId) is null)
            throw new InvalidDataException($"PondId '{envelope.PondId}' chưa được đăng ký trong MongoDB.");

        envelope.SensorId = string.IsNullOrWhiteSpace(envelope.SensorId)
            ? $"sensor_{envelope.PondId}_{envelope.Metric}"
            : envelope.SensorId;
        var currentSensor = _runtime.GetSensor(envelope.SensorId);
        if (currentSensor is not null &&
            (!currentSensor.PondId.Equals(envelope.PondId, StringComparison.OrdinalIgnoreCase) ||
             !currentSensor.Metric.Equals(envelope.Metric, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"SensorId '{envelope.SensorId}' không khớp PondId/Metric đã đăng ký.");

        if (currentSensor is not null && IsSimulationSource(envelope.SourceType) &&
            !IsSimulationSource(currentSensor.SourceType) && !_simulation.CurrentValue.AllowSimulationOverrideRealSensors)
        {
            _logger.LogDebug("Bỏ qua bản tin {SourceType} của {SensorId} vì sensor đang dùng nguồn thật {CurrentSource}.",
                envelope.SourceType, envelope.SensorId, currentSensor.SourceType);
            return;
        }

        var sensor = currentSensor ?? new SensorDefinition
        {
            Id = envelope.SensorId,
            PondId = envelope.PondId,
            Name = $"{threshold.DisplayName} · {envelope.PondId}",
            Metric = envelope.Metric,
            Unit = string.IsNullOrWhiteSpace(envelope.Unit) ? threshold.Unit : envelope.Unit,
            MinValidValue = threshold.ValidMin,
            MaxValidValue = threshold.ValidMax,
            CalibrationDateUtc = DateTime.UtcNow,
            CalibrationDueDateUtc = DateTime.UtcNow.AddDays(_behavior.CurrentValue.DefaultCalibrationIntervalDays)
        };

        var wasOffline = sensor.Status == "offline";
        var isFinite = !double.IsNaN(envelope.Value) && !double.IsInfinity(envelope.Value);
        var isValid = isFinite && envelope.Value >= threshold.ValidMin && envelope.Value <= threshold.ValidMax;
        var quality = isValid ? (string.IsNullOrWhiteSpace(envelope.Quality) ? "good" : envelope.Quality) : "invalid";
        var sourceChanged = !sensor.SourceType.Equals(envelope.SourceType, StringComparison.OrdinalIgnoreCase);
        sensor.SourceType = envelope.SourceType;
        sensor.Unit = string.IsNullOrWhiteSpace(envelope.Unit) ? threshold.Unit : envelope.Unit;
        sensor.MinValidValue = threshold.ValidMin;
        sensor.MaxValidValue = threshold.ValidMax;
        if (sourceChanged || string.IsNullOrWhiteSpace(sensor.ExternalAddress))
            sensor.ExternalAddress = string.IsNullOrWhiteSpace(envelope.SourceAddress)
                ? $"{envelope.SourceType}://{envelope.SensorId}"
                : envelope.SourceAddress;
        sensor.Status = "online";
        sensor.LastSeenAtUtc = DateTime.UtcNow;
        sensor.BatteryLevel = envelope.BatteryLevel;
        sensor.SignalStrength = envelope.SignalStrength;
        sensor.FirmwareVersion = envelope.FirmwareVersion ?? sensor.FirmwareVersion;
        sensor.LastQuality = quality;
        sensor.LastError = isValid ? null : "Giá trị ngoài miền hợp lệ.";
        sensor.UpdatedAtUtc = DateTime.UtcNow;

        var reading = new SensorReading
        {
            Metadata = new SensorReadingMetadata
            {
                PondId = envelope.PondId,
                SensorId = envelope.SensorId,
                Metric = envelope.Metric,
                SourceType = envelope.SourceType
            },
            Value = envelope.Value,
            Unit = sensor.Unit,
            Quality = quality,
            Timestamp = envelope.TimestampUtc == default ? DateTime.UtcNow : envelope.TimestampUtc,
            ReceivedAtUtc = DateTime.UtcNow,
            BatteryLevel = envelope.BatteryLevel,
            SignalStrength = envelope.SignalStrength,
            FirmwareVersion = envelope.FirmwareVersion,
            RawPayload = envelope.RawPayload
        };

        var applied = _runtime.ApplyReading(reading, sensor);
        await Task.WhenAll(
            _repository.InsertReadingAsync(reading, ct),
            _repository.UpsertSensorAsync(applied.Sensor, ct),
            _repository.UpsertTelemetryAsync(applied.Telemetry, ct),
            _sourceHealth.RecordMessageAsync(
                string.IsNullOrWhiteSpace(envelope.SourceId) ? envelope.SourceType : envelope.SourceId, ct));

        if (wasOffline)
        {
            var connectivity = _runtime.ResolveMetricAlert(sensor.PondId, $"connectivity:{sensor.Id}", "sensor-ingestion");
            if (connectivity is not null) await _repository.UpsertAlertAsync(connectivity, ct);
        }

        if (!isValid)
        {
            await _alerts.ReportQualityIssueAsync(sensor, reading, ct);
            return;
        }

        await _alerts.ResolveQualityIssueAsync(sensor, ct);
        await _alerts.EvaluateAsync(sensor, reading, ct);
        await _automation.ProcessAsync(sensor.PondId, sensor.Metric, reading.Value, ct);
    }

    private static bool IsSimulationSource(string sourceType) =>
        sourceType.Equals("simulation", StringComparison.OrdinalIgnoreCase) ||
        sourceType.Equals("manual-simulation", StringComparison.OrdinalIgnoreCase);
}
