using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using AquacultureMonitoring.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

/// <summary>Creates calibration work reminders and escalates overdue sensor maintenance.</summary>
public sealed class AssetMaintenanceMonitor : BackgroundService
{
	private readonly FarmRuntimeState _runtime;

	private readonly FarmRepository _repository;

	private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;

	private readonly ILogger<AssetMaintenanceMonitor> _logger;

	private readonly AlertNotificationService _notifications;

	public AssetMaintenanceMonitor(FarmRuntimeState runtime, FarmRepository repository, IOptionsMonitor<SystemBehaviorOptions> behavior, ILogger<AssetMaintenanceMonitor> logger, AlertNotificationService notifications)
	{
		_runtime = runtime;
		_repository = repository;
		_behavior = behavior;
		_logger = logger;
		_notifications = notifications;
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
			DateTime reminderCutoff = DateTime.UtcNow.AddDays(options.CalibrationReminderDaysBefore);
			foreach (SensorDefinition sensor in _runtime.Sensors.Where((SensorDefinition x) => x.IsEnabled && x.CalibrationDueDateUtc.HasValue && x.CalibrationDueDateUtc.Value <= reminderCutoff))
			{
				DateTime dueAt = sensor.CalibrationDueDateUtc!.Value;
				string text = "calibration:" + sensor.Id;
				MaintenanceTask? maintenanceTask = _runtime.GetMaintenanceTask(text);
				string status = ((dueAt <= DateTime.UtcNow) ? "overdue" : "open");
				MaintenanceTask maintenanceTask2 = new MaintenanceTask
				{
					Id = text,
					PondId = sensor.PondId,
					AssetId = sensor.Id,
					AssetType = "sensor",
					Title = "Hiệu chuẩn " + sensor.Name,
					Status = status,
					DueAtUtc = dueAt,
					CompletedAtUtc = null,
					AssignedTo = maintenanceTask?.AssignedTo
				};
				_runtime.UpsertMaintenanceTask(maintenanceTask2);
				await _repository.UpsertMaintenanceTaskAsync(maintenanceTask2, stoppingToken);
				string metric = "maintenance:" + sensor.Id;
				Pond? pond = _runtime.GetPond(sensor.PondId);
				AlertItem? existingAlert = _runtime.GetActiveAlert(sensor.PondId, metric);
				AlertItem alert = existingAlert ?? new AlertItem
				{
					PondId = sensor.PondId,
					PondName = (pond?.Name ?? sensor.PondId),
					Metric = metric,
					SensorId = sensor.Id,
					Status = "new",
					Timestamp = DateTime.UtcNow,
					Source = "maintenance-monitor"
				};
				alert.Threshold = $"Đến hạn {dueAt.ToLocalTime():dd/MM/yyyy}";
				alert.Severity = ((status == "overdue") ? "critical" : "warning");
				alert.Message = ((status == "overdue") ? (sensor.Name + " đã quá hạn hiệu chuẩn.") : (sensor.Name + " sắp đến hạn hiệu chuẩn."));
				alert.ProposedSolution = "Đo đối chứng, hiệu chuẩn theo hướng dẫn nhà sản xuất và ghi lại giá trị trước/sau.";
				alert.LastUpdatedAtUtc = DateTime.UtcNow;
				_runtime.UpsertAlert(alert);
				await _repository.UpsertAlertAsync(alert, stoppingToken);
				if (existingAlert == null)
				{
					await _notifications.SendAsync(alert, stoppingToken);
				}
			}
			_logger.LogDebug("Đã kiểm tra lịch hiệu chuẩn của {Count} cảm biến.", _runtime.Sensors.Count);
			await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.MaintenanceCheckIntervalMinutes)), stoppingToken);
		}
	}
}
