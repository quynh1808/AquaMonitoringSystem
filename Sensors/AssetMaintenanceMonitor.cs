using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

/// <summary>
/// Creates calibration work orders and alerts before a sensor reaches its configured due date.
/// This service does not calibrate hardware; it manages the operational workflow around calibration.
/// </summary>
public sealed class AssetMaintenanceMonitor : BackgroundService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;
    private readonly ILogger<AssetMaintenanceMonitor> _logger;

    public AssetMaintenanceMonitor(
        FarmRuntimeState runtime,
        FarmRepository repository,
        IOptionsMonitor<SystemBehaviorOptions> behavior,
        ILogger<AssetMaintenanceMonitor> logger)
    {
        _runtime = runtime;
        _repository = repository;
        _behavior = behavior;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!_runtime.IsInitialized && !stoppingToken.IsCancellationRequested)
            await Task.Delay(100, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _behavior.CurrentValue;
            var reminderCutoff = DateTime.UtcNow.AddDays(options.CalibrationReminderDaysBefore);

            foreach (var sensor in _runtime.Sensors.Where(x =>
                         x.IsEnabled && x.CalibrationDueDateUtc.HasValue && x.CalibrationDueDateUtc.Value <= reminderCutoff))
            {
                var dueAt = sensor.CalibrationDueDateUtc!.Value;
                var taskId = $"calibration:{sensor.Id}";
                var existing = _runtime.GetMaintenanceTask(taskId);
                var status = dueAt <= DateTime.UtcNow ? "overdue" : "open";
                var task = new MaintenanceTask
                {
                    Id = taskId,
                    PondId = sensor.PondId,
                    AssetId = sensor.Id,
                    AssetType = "sensor",
                    Title = $"Hiệu chuẩn {sensor.Name}",
                    Status = status,
                    DueAtUtc = dueAt,
                    CompletedAtUtc = null,
                    AssignedTo = existing?.AssignedTo
                };

                _runtime.UpsertMaintenanceTask(task);
                await _repository.UpsertMaintenanceTaskAsync(task, stoppingToken);

                var metric = $"maintenance:{sensor.Id}";
                var pond = _runtime.GetPond(sensor.PondId);
                var alert = _runtime.GetActiveAlert(sensor.PondId, metric) ?? new AlertItem
                {
                    PondId = sensor.PondId,
                    PondName = pond?.Name ?? sensor.PondId,
                    Metric = metric,
                    SensorId = sensor.Id,
                    Status = "new",
                    Timestamp = DateTime.UtcNow,
                    Source = "maintenance-monitor"
                };
                alert.Threshold = $"Đến hạn {dueAt.ToLocalTime():dd/MM/yyyy}";
                alert.Severity = status == "overdue" ? "critical" : "warning";
                alert.Message = status == "overdue"
                    ? $"{sensor.Name} đã quá hạn hiệu chuẩn."
                    : $"{sensor.Name} sắp đến hạn hiệu chuẩn.";
                alert.ProposedSolution = "Đo đối chứng, hiệu chuẩn theo hướng dẫn nhà sản xuất và ghi lại giá trị trước/sau.";
                alert.LastUpdatedAtUtc = DateTime.UtcNow;
                _runtime.UpsertAlert(alert);
                await _repository.UpsertAlertAsync(alert, stoppingToken);
            }

            _logger.LogDebug("Đã kiểm tra lịch hiệu chuẩn của {Count} cảm biến.", _runtime.Sensors.Count);
            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.MaintenanceCheckIntervalMinutes)), stoppingToken);
        }
    }
}
