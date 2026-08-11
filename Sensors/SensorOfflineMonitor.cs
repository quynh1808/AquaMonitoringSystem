using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Sensors;

public sealed class SensorOfflineMonitor : BackgroundService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;

    public SensorOfflineMonitor(FarmRuntimeState runtime, FarmRepository repository,
        IOptionsMonitor<SystemBehaviorOptions> behavior)
    {
        _runtime = runtime;
        _repository = repository;
        _behavior = behavior;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!_runtime.IsInitialized && !stoppingToken.IsCancellationRequested)
            await Task.Delay(100, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _behavior.CurrentValue;
            var cutoff = DateTime.UtcNow.AddSeconds(-options.SensorOfflineAfterSeconds);
            var sensors = _runtime.Sensors;

            foreach (var sensor in sensors.Where(x => x.IsEnabled &&
                         (!x.LastSeenAtUtc.HasValue || x.LastSeenAtUtc.Value < cutoff) && x.Status != "offline"))
            {
                var updated = _runtime.UpdateSensor(sensor.Id, x =>
                {
                    x.Status = "offline";
                    x.LastQuality = "no-data";
                    x.LastError = "Không nhận được dữ liệu trong thời gian quy định.";
                });
                if (updated is null) continue;
                await _repository.UpsertSensorAsync(updated, stoppingToken);

                var pond = _runtime.GetPond(sensor.PondId);
                var metric = $"connectivity:{sensor.Id}";
                var alert = _runtime.GetActiveAlert(sensor.PondId, metric) ?? new AlertItem
                {
                    PondId = sensor.PondId,
                    PondName = pond?.Name ?? sensor.PondId,
                    Metric = metric,
                    SensorId = sensor.Id,
                    Status = "new",
                    Timestamp = DateTime.UtcNow,
                    Source = "offline-monitor"
                };
                alert.Value = 0;
                alert.Threshold = $"> {options.SensorOfflineAfterSeconds} giây không có dữ liệu";
                alert.Severity = "critical";
                alert.Message = $"Mất kết nối với {sensor.Name}.";
                alert.ProposedSolution = "Kiểm tra nguồn, dây RS-485/Ethernet, Wi-Fi, gateway và tiến trình thu thập dữ liệu.";
                alert.LastUpdatedAtUtc = DateTime.UtcNow;
                alert.IsResolved = false;
                _runtime.UpsertAlert(alert);
                await _repository.UpsertAlertAsync(alert, stoppingToken);
            }

            // Trạng thái cấp ao không được tiếp tục hiển thị ONLINE khi toàn bộ sensor đã mất dữ liệu.
            var refreshedSensors = _runtime.Sensors;
            foreach (var pond in _runtime.Ponds)
            {
                var pondSensors = refreshedSensors.Where(x => x.PondId == pond.Id && x.IsEnabled).ToList();
                if (pondSensors.Count == 0) continue;
                var onlineCount = pondSensors.Count(x => x.LastSeenAtUtc.HasValue && x.LastSeenAtUtc.Value >= cutoff && x.Status == "online");
                var expectedOnline = onlineCount > 0;
                var current = _runtime.GetTelemetry(pond.Id);
                var expectedQuality = onlineCount switch
                {
                    0 => "no-data",
                    var count when count < pondSensors.Count => "partial",
                    _ when current.Quality is "no-data" or "partial" => "good",
                    _ => current.Quality
                };
                if (current.IsOnline == expectedOnline && current.Quality == expectedQuality) continue;

                var telemetry = _runtime.UpdateTelemetry(pond.Id, x =>
                {
                    x.IsOnline = expectedOnline;
                    x.Quality = expectedQuality;
                });
                if (telemetry is not null) await _repository.UpsertTelemetryAsync(telemetry, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.SensorHealthCheckIntervalSeconds)), stoppingToken);
        }
    }
}
