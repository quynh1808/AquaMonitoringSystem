using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using AquacultureMonitoring.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

/// <summary>Marks stale sensors offline and raises one durable connectivity alert per sensor.</summary>
public sealed class SensorOfflineMonitor : BackgroundService
{
	private readonly FarmRuntimeState _runtime;

	private readonly FarmRepository _repository;

	private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;

	private readonly AlertNotificationService _notifications;
	private readonly AutomationEngineService _automation;

	public SensorOfflineMonitor(FarmRuntimeState runtime, FarmRepository repository, IOptionsMonitor<SystemBehaviorOptions> behavior, AlertNotificationService notifications, AutomationEngineService automation)
	{
		_runtime = runtime;
		_repository = repository;
		_behavior = behavior;
		_notifications = notifications;
		_automation = automation;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!_runtime.IsInitialized && !stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(100, stoppingToken);
		}
		while (!stoppingToken.IsCancellationRequested)
		{
			SystemBehaviorOptions options = _behavior.CurrentValue;
			List<SensorDefinition> sensors = _runtime.Sensors;
			foreach (SensorDefinition sensor in sensors.Where(x => x.IsEnabled && !x.IsArchived && !x.IsPendingRemoval && x.PublishIntervalSeconds > 0 &&
				x.Status is not ("maintenance" or "not-configured" or "unavailable-via-node") &&
				(!x.LastSeenAtUtc.HasValue || x.LastSeenAtUtc.Value < DateTime.UtcNow.AddSeconds(-Math.Max(5, x.OfflineTimeoutSeconds))) && x.Status != "offline"))
			{
				SensorDefinition? sensorDefinition = _runtime.UpdateSensor(sensor.Id, delegate(SensorDefinition x)
				{
					x.Status = "offline";
					x.LastQuality = "no-data";
					x.LastError = "Không nhận được dữ liệu trong thời gian quy định.";
				});
				if (sensorDefinition != null)
				{
					await _repository.UpsertSensorAsync(sensorDefinition, stoppingToken);
					Pond? pond = _runtime.GetPond(sensor.PondId);
					string metric = "connectivity:" + sensor.Id;
					AlertItem? existingAlert = _runtime.GetActiveAlert(sensor.PondId, metric);
					AlertItem alert = existingAlert ?? new AlertItem
					{
						PondId = sensor.PondId,
						PondName = (pond?.Name ?? sensor.PondId),
						Metric = metric,
						SensorId = sensor.Id,
						Status = "new",
						Timestamp = DateTime.UtcNow,
						Source = "offline-monitor"
					};
					alert.Value = 0.0;
					alert.Threshold = $"> {options.SensorOfflineAfterSeconds} giây không có dữ liệu";
					alert.Severity = "critical";
					alert.Message = "Mất kết nối với " + sensor.Name + ".";
					alert.ProposedSolution = "Kiểm tra nguồn, dây RS-485/Ethernet, Wi-Fi, gateway và tiến trình thu thập dữ liệu.";
					alert.LastUpdatedAtUtc = DateTime.UtcNow;
					alert.IsResolved = false;
					_runtime.UpsertAlert(alert);
					await _repository.UpsertAlertAsync(alert, stoppingToken);
					if (existingAlert == null)
					{
						await _notifications.SendAsync(alert, stoppingToken);
					}
					await _automation.HandleInputFailureAsync(sensor, "offline", stoppingToken);
				}
			}
			List<SensorDefinition> refreshedSensors = _runtime.Sensors;
			foreach (Pond pond2 in _runtime.Ponds)
			{
				List<SensorDefinition> list = refreshedSensors.Where((SensorDefinition x) => x.PondId == pond2.Id && x.IsEnabled).ToList();
				if (list.Count == 0)
				{
					continue;
				}
				int num = list.Count(x => x.LastSeenAtUtc.HasValue && x.LastSeenAtUtc.Value >= DateTime.UtcNow.AddSeconds(-Math.Max(5, x.OfflineTimeoutSeconds)) && x.Status == "online");
				bool expectedOnline = num > 0;
				TelemetryData telemetry = _runtime.GetTelemetry(pond2.Id);
				int num2 = num;
				string text;
				if (num2 == 0)
				{
					text = "no-data";
				}
				else if (num2 < list.Count)
				{
					text = "partial";
				}
				else
				{
					string quality = telemetry.Quality;
					bool flag = ((quality == "no-data" || quality == "partial") ? true : false);
					text = ((!flag) ? telemetry.Quality : "good");
				}
				string expectedQuality = text;
				if (telemetry.IsOnline != expectedOnline || !(telemetry.Quality == expectedQuality))
				{
					TelemetryData? telemetryData = _runtime.UpdateTelemetry(pond2.Id, delegate(TelemetryData x)
					{
						x.IsOnline = expectedOnline;
						x.Quality = expectedQuality;
					});
					if (telemetryData != null)
					{
						await _repository.UpsertTelemetryAsync(telemetryData, stoppingToken);
					}
				}
			}
			await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.SensorHealthCheckIntervalSeconds)), stoppingToken);
		}
	}
}
