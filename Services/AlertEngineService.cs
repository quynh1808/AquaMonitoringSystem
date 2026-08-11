using System.Collections.Concurrent;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

public sealed class AlertEngineService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly IOptionsMonitor<SensorThresholdCatalog> _catalog;
    private readonly ConcurrentDictionary<string, CandidateState> _states = new(StringComparer.OrdinalIgnoreCase);

    public AlertEngineService(FarmRuntimeState runtime, FarmRepository repository,
        IOptionsMonitor<SensorThresholdCatalog> catalog)
    {
        _runtime = runtime;
        _repository = repository;
        _catalog = catalog;
    }

    public async Task EvaluateAsync(SensorDefinition sensor, SensorReading reading, CancellationToken ct)
    {
        if (!_catalog.CurrentValue.Metrics.TryGetValue(sensor.Metric, out var threshold)) return;
        var condition = EvaluateCondition(reading.Value, threshold);
        var key = $"{sensor.PondId}:{sensor.Metric}";
        var active = _runtime.GetActiveAlert(sensor.PondId, sensor.Metric);

        if (condition is null)
        {
            _states.TryRemove(key, out _);
            if (active is not null && IsRecovered(reading.Value, threshold))
            {
                var resolved = _runtime.ResolveMetricAlert(sensor.PondId, sensor.Metric, "threshold-engine");
                if (resolved is not null) await _repository.UpsertAlertAsync(resolved, ct);
            }
            return;
        }

        var state = _states.GetOrAdd(key, _ => new CandidateState
        {
            FirstSeenAtUtc = reading.ReceivedAtUtc,
            Direction = condition.Direction,
            Severity = condition.Severity
        });

        if (state.Direction != condition.Direction || state.Severity != condition.Severity)
        {
            state.Direction = condition.Direction;
            state.Severity = condition.Severity;
            state.FirstSeenAtUtc = reading.ReceivedAtUtc;
        }

        if ((reading.ReceivedAtUtc - state.FirstSeenAtUtc).TotalSeconds < threshold.DelaySeconds) return;
        if (active is not null && (reading.ReceivedAtUtc - active.LastUpdatedAtUtc).TotalSeconds < threshold.CooldownSeconds) return;

        var pond = _runtime.GetPond(sensor.PondId);
        var messageTemplate = condition.Severity == "critical" ? threshold.CriticalMessage : threshold.WarningMessage;
        var alert = active ?? new AlertItem
        {
            Id = Guid.NewGuid().ToString("N"),
            PondId = sensor.PondId,
            PondName = pond?.Name ?? sensor.PondId,
            Metric = sensor.Metric,
            SensorId = sensor.Id,
            Status = "new",
            Timestamp = reading.Timestamp,
            Source = "threshold-engine"
        };
        alert.Value = reading.Value;
        alert.Severity = condition.Severity;
        alert.Threshold = condition.ThresholdText;
        alert.Message = Expand(messageTemplate, threshold, reading.Value);
        alert.ProposedSolution = Expand(threshold.ProposedSolution, threshold, reading.Value);
        alert.LastUpdatedAtUtc = reading.ReceivedAtUtc;
        alert.IsResolved = false;

        _runtime.UpsertAlert(alert);
        await _repository.UpsertAlertAsync(alert, ct);
        state.LastAlertAtUtc = reading.ReceivedAtUtc;
    }

    public async Task ReportQualityIssueAsync(SensorDefinition sensor, SensorReading reading, CancellationToken ct)
    {
        var metric = $"quality:{sensor.Metric}";
        var active = _runtime.GetActiveAlert(sensor.PondId, metric);
        var pond = _runtime.GetPond(sensor.PondId);
        var alert = active ?? new AlertItem
        {
            Id = Guid.NewGuid().ToString("N"),
            PondId = sensor.PondId,
            PondName = pond?.Name ?? sensor.PondId,
            Metric = metric,
            SensorId = sensor.Id,
            Timestamp = reading.Timestamp,
            Source = "data-quality"
        };
        alert.Value = reading.Value;
        alert.Severity = "critical";
        alert.Status = "new";
        alert.Threshold = $"{sensor.MinValidValue}–{sensor.MaxValidValue} {sensor.Unit}";
        alert.Message = $"Dữ liệu {sensor.Name} nằm ngoài miền hợp lệ hoặc không thể sử dụng.";
        alert.ProposedSolution = "Kiểm tra dây tín hiệu, nguồn, register mapping và đo đối chứng trước khi dùng giá trị này để điều khiển.";
        alert.LastUpdatedAtUtc = DateTime.UtcNow;
        alert.IsResolved = false;
        _runtime.UpsertAlert(alert);
        await _repository.UpsertAlertAsync(alert, ct);
    }

    public async Task ResolveQualityIssueAsync(SensorDefinition sensor, CancellationToken ct)
    {
        var resolved = _runtime.ResolveMetricAlert(sensor.PondId, $"quality:{sensor.Metric}", "data-quality");
        if (resolved is not null) await _repository.UpsertAlertAsync(resolved, ct);
    }

    private static AlertCondition? EvaluateCondition(double value, MetricThresholdOptions threshold)
    {
        if (threshold.CriticalLow.HasValue && value <= threshold.CriticalLow.Value)
            return new("critical", "low", $"≤ {threshold.CriticalLow.Value} {threshold.Unit}");
        if (threshold.CriticalHigh.HasValue && value >= threshold.CriticalHigh.Value)
            return new("critical", "high", $"≥ {threshold.CriticalHigh.Value} {threshold.Unit}");
        if (threshold.WarningLow.HasValue && value <= threshold.WarningLow.Value)
            return new("warning", "low", $"≤ {threshold.WarningLow.Value} {threshold.Unit}");
        if (threshold.WarningHigh.HasValue && value >= threshold.WarningHigh.Value)
            return new("warning", "high", $"≥ {threshold.WarningHigh.Value} {threshold.Unit}");
        return null;
    }

    private static bool IsRecovered(double value, MetricThresholdOptions threshold)
    {
        var aboveLow = !threshold.WarningLow.HasValue || value > threshold.WarningLow.Value + threshold.Hysteresis;
        var belowHigh = !threshold.WarningHigh.HasValue || value < threshold.WarningHigh.Value - threshold.Hysteresis;
        return aboveLow && belowHigh;
    }

    private static string Expand(string template, MetricThresholdOptions threshold, double value) => template
        .Replace("{value}", value.ToString($"F{threshold.DecimalPlaces}"), StringComparison.OrdinalIgnoreCase)
        .Replace("{unit}", threshold.Unit, StringComparison.OrdinalIgnoreCase)
        .Replace("{metric}", threshold.DisplayName, StringComparison.OrdinalIgnoreCase);

    private sealed class CandidateState
    {
        public DateTime FirstSeenAtUtc { get; set; }
        public DateTime? LastAlertAtUtc { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }

    private sealed record AlertCondition(string Severity, string Direction, string ThresholdText);
}
