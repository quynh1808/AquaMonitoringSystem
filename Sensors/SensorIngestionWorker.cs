using System.Collections.Concurrent;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using AquacultureMonitoring.Services;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;
    private readonly ILogger<SensorIngestionWorker> _logger;
    private readonly ConcurrentDictionary<string, long> _lastSequences = new(StringComparer.OrdinalIgnoreCase);

    public SensorIngestionWorker(
        SensorMessageBus bus,
        FarmRuntimeState runtime,
        FarmRepository repository,
        AlertEngineService alerts,
        AutomationEngineService automation,
        DataSourceHealthService sourceHealth,
        IOptionsMonitor<SensorThresholdCatalog> thresholds,
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
                envelope.Completion?.TrySetResult(new(true));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không xử lý được dữ liệu từ sensor {SensorId}.", envelope.SensorId);
                envelope.Completion?.TrySetResult(new(false, ex.Message));
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
            throw new InvalidDataException($"PondId '{envelope.PondId}' chưa được đăng ký trong PostgreSQL.");

        var now = DateTime.UtcNow;
        var timestamp = envelope.TimestampUtc == default ? now : envelope.TimestampUtc;
        if (timestamp > now.AddSeconds(_behavior.CurrentValue.MaxFutureClockSkewSeconds))
            throw new InvalidDataException("Timestamp sensor nằm quá xa trong tương lai.");
        if (timestamp < now.AddMinutes(-_behavior.CurrentValue.MaxTelemetryAgeMinutes) && !envelope.IsBackfill)
            throw new InvalidDataException("Bản tin sensor quá cũ; gateway phải gửi theo luồng backfill riêng.");
        if (envelope.IsBackfill && string.IsNullOrWhiteSpace(envelope.EventId))
            throw new InvalidDataException("Bản tin backfill bắt buộc có EventId để chống ghi trùng.");

        if (string.IsNullOrWhiteSpace(envelope.SensorId))
            throw new InvalidDataException("SensorId là bắt buộc; AquaOps không tự đăng ký cảm biến từ telemetry.");
        // Work on a copy; durable insertion must succeed before live state is mutated.
        var currentSensor = CloneSensor(_runtime.GetSensor(envelope.SensorId));
        if (currentSensor is not null &&
            (!currentSensor.PondId.Equals(envelope.PondId, StringComparison.OrdinalIgnoreCase) ||
             !currentSensor.Metric.Equals(envelope.Metric, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"SensorId '{envelope.SensorId}' không khớp PondId/Metric đã đăng ký.");

        var sensor = currentSensor ?? throw new InvalidDataException(
            $"SensorId '{envelope.SensorId}' chưa được admin đăng ký; bản tin phải đưa vào quarantine.");
        if (!sensor.IsEnabled || sensor.IsArchived || sensor.PublishIntervalSeconds <= 0)
            throw new InvalidDataException($"SensorId '{envelope.SensorId}' đang disable hoặc chưa được cấu hình chu kỳ gửi.");
        if (!string.IsNullOrWhiteSpace(sensor.SocketId) && !sensor.SocketId.Equals(envelope.SocketId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SensorId '{envelope.SensorId}' không khớp SocketId đã đăng ký.");
        if (!string.IsNullOrWhiteSpace(sensor.NodeId) && !sensor.NodeId.Equals(envelope.NodeId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SensorId '{envelope.SensorId}' không khớp NodeId đã đăng ký.");

        var wasOffline = sensor.Status == "offline";
        var isFinite = !double.IsNaN(envelope.Value) && !double.IsInfinity(envelope.Value);
        // Physical limits are reported by the edge driver. The metric catalog remains a safe
        // fallback for sensors migrated before their first capabilities handshake.
        var hasPhysicalLimits = sensor.MinValidValue < sensor.MaxValidValue;
        var isValid = isFinite && hasPhysicalLimits && envelope.Value >= sensor.MinValidValue && envelope.Value <= sensor.MaxValidValue;
        var hasSequenceGap = false;
        if (!envelope.IsBackfill && envelope.SequenceNumber.HasValue && _lastSequences.TryGetValue(envelope.SensorId, out var lastSequence))
        {
            if (envelope.SequenceNumber.Value <= lastSequence)
            {
                _logger.LogDebug("Bỏ qua sequence cũ/trùng {Sequence} của {SensorId}.", envelope.SequenceNumber, envelope.SensorId);
                return;
            }
            hasSequenceGap = envelope.SequenceNumber.Value > lastSequence + 1;
        }
        var quality = !isValid ? "invalid" : hasSequenceGap ? "suspect" : string.IsNullOrWhiteSpace(envelope.Quality) ? "good" : envelope.Quality;
        var sourceChanged = !sensor.SourceType.Equals(envelope.SourceType, StringComparison.OrdinalIgnoreCase);
        sensor.SourceType = envelope.SourceType;
        sensor.Unit = string.IsNullOrWhiteSpace(envelope.Unit) ? threshold.Unit : envelope.Unit;
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
                ,SocketId = envelope.SocketId
                ,NodeId = envelope.NodeId
                ,InstallationId = string.IsNullOrWhiteSpace(envelope.InstallationId) ? sensor.InstallationId : envelope.InstallationId
            },
            Value = envelope.Value,
            Unit = sensor.Unit,
            Quality = quality,
            Timestamp = timestamp,
            ReceivedAtUtc = DateTime.UtcNow,
            BatteryLevel = envelope.BatteryLevel,
            SignalStrength = envelope.SignalStrength,
            FirmwareVersion = envelope.FirmwareVersion,
            RawPayload = envelope.RawPayload,
            EventId = envelope.EventId.Trim(),
            SequenceNumber = envelope.SequenceNumber
            ,IsBackfill = envelope.IsBackfill
            ,TimeQuality = envelope.TimeQuality
        };

        try
        {
            await _repository.InsertReadingAsync(reading, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation } && !string.IsNullOrWhiteSpace(reading.EventId))
        {
            _logger.LogDebug("Bỏ qua bản tin trùng EventId {EventId} của {SensorId}.", reading.EventId, envelope.SensorId);
            return;
        }
        // Historical backfill is persisted but must not rewind the live dashboard or automation.
        if (envelope.IsBackfill) return;
        if (envelope.SequenceNumber.HasValue) _lastSequences[envelope.SensorId] = envelope.SequenceNumber.Value;

        var applied = _runtime.ApplyReading(reading, sensor);
        await Task.WhenAll(
            _repository.UpsertSensorAsync(applied.Sensor, ct),
            _sourceHealth.RecordMessageAsync(
                string.IsNullOrWhiteSpace(envelope.SourceId) ? envelope.SourceType : envelope.SourceId, ct));

        if (wasOffline)
        {
            var connectivity = _runtime.ResolveMetricAlert(sensor.PondId, $"connectivity:{sensor.Id}", "sensor-ingestion");
            if (connectivity is not null) await _repository.UpsertAlertAsync(connectivity, ct);
        }

        if (!isValid || hasSequenceGap)
        {
            await _alerts.ReportQualityIssueAsync(sensor, reading, ct);
            await _automation.HandleInputFailureAsync(sensor, quality, ct);
            return;
        }

        await _alerts.ResolveQualityIssueAsync(sensor, ct);
        await _alerts.EvaluateAsync(sensor, reading, ct);
        await _automation.ProcessAsync(sensor, reading, ct);
    }

    private static SensorDefinition? CloneSensor(SensorDefinition? source) => source is null ? null : new SensorDefinition
    {
        Id = source.Id, PondId = source.PondId, Name = source.Name, Metric = source.Metric,
        Unit = source.Unit, SourceType = source.SourceType, ExternalAddress = source.ExternalAddress,
        SocketId = source.SocketId, NodeId = source.NodeId, ChannelId = source.ChannelId,
        InstallationId = source.InstallationId, PublishIntervalSeconds = source.PublishIntervalSeconds,
        OfflineTimeoutSeconds = source.OfflineTimeoutSeconds,
        SchedulingMode = source.SchedulingMode, ConfigurationStatus = source.ConfigurationStatus,
        DesiredConfigurationVersion = source.DesiredConfigurationVersion,
        ReportedConfigurationVersion = source.ReportedConfigurationVersion,
        IsPrimary = source.IsPrimary, IsArchived = source.IsArchived, IsPendingRemoval = source.IsPendingRemoval,
        RemovalRequestedAtUtc = source.RemovalRequestedAtUtc, RemovalRequestedBy = source.RemovalRequestedBy,
        RemovalReason = source.RemovalReason, Notes = source.Notes,
        IsEnabled = source.IsEnabled, Status = source.Status, LastSeenAtUtc = source.LastSeenAtUtc,
        BatteryLevel = source.BatteryLevel, SignalStrength = source.SignalStrength,
        FirmwareVersion = source.FirmwareVersion, CalibrationDateUtc = source.CalibrationDateUtc,
        CalibrationDueDateUtc = source.CalibrationDueDateUtc, MinValidValue = source.MinValidValue,
        MaxValidValue = source.MaxValidValue, LastQuality = source.LastQuality,
        LastError = source.LastError, UpdatedAtUtc = source.UpdatedAtUtc
    };
}
