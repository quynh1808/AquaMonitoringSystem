using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

/// <summary>Evaluates threshold/quality conditions and owns the alert lifecycle projection.</summary>
public sealed class AlertEngineService
{
	private sealed class CandidateState
	{
		public DateTime FirstSeenAtUtc { get; set; }

		public DateTime? LastAlertAtUtc { get; set; }

		public string Direction { get; set; } = string.Empty;

		public string Severity { get; set; } = string.Empty;
	}

	private sealed record AlertCondition(string Severity, string Direction, string ThresholdText);

	private readonly FarmRuntimeState _runtime;

	private readonly FarmRepository _repository;

	private readonly IOptionsMonitor<SensorThresholdCatalog> _catalog;

	private readonly ConcurrentDictionary<string, CandidateState> _states = new ConcurrentDictionary<string, CandidateState>(StringComparer.OrdinalIgnoreCase);

	private readonly AlertNotificationService _notifications;

	public AlertEngineService(FarmRuntimeState runtime, FarmRepository repository, IOptionsMonitor<SensorThresholdCatalog> catalog, AlertNotificationService notifications)
	{
		_runtime = runtime;
		_repository = repository;
		_catalog = catalog;
		_notifications = notifications;
	}

	public async Task EvaluateAsync(SensorDefinition sensor, SensorReading reading, CancellationToken ct)
	{
		if (!_catalog.CurrentValue.Metrics.TryGetValue(sensor.Metric, out var value) || value is null)
		{
			return;
		}
		AlertCondition? condition = EvaluateCondition(reading.Value, value);
		string key = sensor.PondId + ":" + sensor.Metric;
		AlertItem? active = _runtime.GetActiveAlert(sensor.PondId, sensor.Metric);
		if (condition is null)
		{
			_states.TryRemove(key, out _);
			if (active != null && IsRecovered(reading.Value, value))
			{
				AlertItem? alertItem = _runtime.ResolveMetricAlert(sensor.PondId, sensor.Metric, "threshold-engine");
				if (alertItem != null)
				{
					await _repository.UpsertAlertAsync(alertItem, ct);
				}
			}
			return;
		}
		CandidateState state = _states.GetOrAdd(key, (string _) => new CandidateState
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
		if (!((reading.ReceivedAtUtc - state.FirstSeenAtUtc).TotalSeconds < (double)value.DelaySeconds) && (active == null || !((reading.ReceivedAtUtc - active.LastUpdatedAtUtc).TotalSeconds < (double)value.CooldownSeconds)))
		{
			Pond? pond = _runtime.GetPond(sensor.PondId);
			string template = ((condition.Severity == "critical") ? value.CriticalMessage : value.WarningMessage);
			AlertItem alert = active ?? new AlertItem
			{
				Id = Guid.NewGuid().ToString("N"),
				PondId = sensor.PondId,
				PondName = (pond?.Name ?? sensor.PondId),
				Metric = sensor.Metric,
				SensorId = sensor.Id,
				Status = "new",
				Timestamp = reading.Timestamp,
				Source = "threshold-engine"
			};
			alert.Value = reading.Value;
			alert.Severity = condition.Severity;
			alert.Threshold = condition.ThresholdText;
			alert.Message = Expand(template, value, reading.Value);
			alert.ProposedSolution = Expand(value.ProposedSolution, value, reading.Value);
			alert.LastUpdatedAtUtc = reading.ReceivedAtUtc;
			alert.IsResolved = false;
			alert.OccurrenceCount = ((active == null) ? 1 : Math.Max(1, alert.OccurrenceCount + 1));
			_runtime.UpsertAlert(alert);
			await _repository.UpsertAlertAsync(alert, ct);
			if (active == null || condition.Severity == "critical")
			{
				await _notifications.SendAsync(alert, ct);
			}
			state.LastAlertAtUtc = reading.ReceivedAtUtc;
		}
	}

	public async Task ReportQualityIssueAsync(SensorDefinition sensor, SensorReading reading, CancellationToken ct)
	{
		string metric = "quality:" + sensor.Metric;
		AlertItem? active = _runtime.GetActiveAlert(sensor.PondId, metric);
		Pond? pond = _runtime.GetPond(sensor.PondId);
		AlertItem alert = active ?? new AlertItem
		{
			Id = Guid.NewGuid().ToString("N"),
			PondId = sensor.PondId,
			PondName = (pond?.Name ?? sensor.PondId),
			Metric = metric,
			SensorId = sensor.Id,
			Timestamp = reading.Timestamp,
			Source = "data-quality"
		};
		alert.Value = reading.Value;
		alert.Severity = "critical";
		alert.Threshold = ((reading.Quality == "suspect") ? "Sequence telemetry phải liên tục" : $"{sensor.MinValidValue}–{sensor.MaxValidValue} {sensor.Unit}");
		alert.Message = ((reading.Quality == "suspect") ? ("Dữ liệu " + sensor.Name + " bị thiếu một hoặc nhiều sequence.") : ("Dữ liệu " + sensor.Name + " nằm ngoài miền hợp lệ hoặc không thể sử dụng."));
		alert.ProposedSolution = ((reading.Quality == "suspect") ? "Kiểm tra buffer/retry của gateway và gửi các mẫu bị thiếu qua luồng backfill có EventId." : "Kiểm tra dây tín hiệu, nguồn, register mapping và đo đối chứng trước khi dùng giá trị này để điều khiển.");
		alert.LastUpdatedAtUtc = DateTime.UtcNow;
		alert.IsResolved = false;
		alert.OccurrenceCount = ((active == null) ? 1 : Math.Max(1, alert.OccurrenceCount + 1));
		_runtime.UpsertAlert(alert);
		await _repository.UpsertAlertAsync(alert, ct);
		if (active == null)
		{
			await _notifications.SendAsync(alert, ct);
		}
	}

	public async Task ResolveQualityIssueAsync(SensorDefinition sensor, CancellationToken ct)
	{
		AlertItem? alertItem = _runtime.ResolveMetricAlert(sensor.PondId, "quality:" + sensor.Metric, "data-quality");
		if (alertItem != null)
		{
			await _repository.UpsertAlertAsync(alertItem, ct);
		}
	}

	private static AlertCondition? EvaluateCondition(double value, MetricThresholdOptions threshold)
	{
		if (threshold.CriticalLow.HasValue && value <= threshold.CriticalLow.Value)
		{
			return new AlertCondition("critical", "low", $"≤ {threshold.CriticalLow.Value} {threshold.Unit}");
		}
		if (threshold.CriticalHigh.HasValue && value >= threshold.CriticalHigh.Value)
		{
			return new AlertCondition("critical", "high", $"≥ {threshold.CriticalHigh.Value} {threshold.Unit}");
		}
		if (threshold.WarningLow.HasValue && value <= threshold.WarningLow.Value)
		{
			return new AlertCondition("warning", "low", $"≤ {threshold.WarningLow.Value} {threshold.Unit}");
		}
		if (threshold.WarningHigh.HasValue && value >= threshold.WarningHigh.Value)
		{
			return new AlertCondition("warning", "high", $"≥ {threshold.WarningHigh.Value} {threshold.Unit}");
		}
		return null;
	}

	private static bool IsRecovered(double value, MetricThresholdOptions threshold)
	{
		bool flag = !threshold.WarningLow.HasValue || value > threshold.WarningLow.Value + threshold.Hysteresis;
		bool flag2 = !threshold.WarningHigh.HasValue || value < threshold.WarningHigh.Value - threshold.Hysteresis;
		return flag & flag2;
	}

	private static string Expand(string template, MetricThresholdOptions threshold, double value)
	{
		return template.Replace("{value}", value.ToString($"F{threshold.DecimalPlaces}"), StringComparison.OrdinalIgnoreCase).Replace("{unit}", threshold.Unit, StringComparison.OrdinalIgnoreCase).Replace("{metric}", threshold.DisplayName, StringComparison.OrdinalIgnoreCase);
	}
}
