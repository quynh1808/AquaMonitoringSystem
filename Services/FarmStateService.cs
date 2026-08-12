using System.Globalization;
using System.Security.Claims;
using System.Text;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Infrastructure;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using AquacultureMonitoring.Sensors;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Components.Authorization;

namespace AquacultureMonitoring.Services;

public sealed class FarmStateService : IDisposable
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly IMongoWriteQueue _writeQueue;
    private readonly IOptionsMonitor<SensorThresholdCatalog> _thresholds;
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly FarmAccessService _access;
    private readonly DeviceCommandService _deviceCommands;
    private readonly ProductionCycleService _productionCycles;
    private readonly SystemConfigurationRepository _systemConfiguration;
    private ClaimsPrincipal _principal = new(new ClaimsIdentity());

    public FarmStateService(
        FarmRuntimeState runtime,
        FarmRepository repository,
        IMongoWriteQueue writeQueue,
        IOptionsMonitor<SensorThresholdCatalog> thresholds,
        IOptionsMonitor<SystemBehaviorOptions> behavior,
        AuthenticationStateProvider authenticationStateProvider,
        FarmAccessService access,
        DeviceCommandService deviceCommands,
        ProductionCycleService productionCycles,
        SystemConfigurationRepository systemConfiguration)
    {
        _runtime = runtime;
        _repository = repository;
        _writeQueue = writeQueue;
        _thresholds = thresholds;
        _behavior = behavior;
        _authenticationStateProvider = authenticationStateProvider;
        _access = access;
        _deviceCommands = deviceCommands;
        _productionCycles = productionCycles;
        _systemConfiguration = systemConfiguration;
        _runtime.Changed += ForwardChanged;
        SelectedPondId = string.Empty;
    }

    public event Action? Changed;
    // Every UI projection is scoped by the authenticated user's pond claims. Returning filtered
    // snapshots here also protects shared Dashboard components from accidentally leaking a pond.
    public List<Pond> Ponds => _runtime.Ponds.Where(x => _access.CanAccessPond(_principal, x.Id)).ToList();
    public Dictionary<string, TelemetryData> Telemetry => _runtime.Telemetry
        .Where(x => _access.CanAccessPond(_principal, x.Key))
        .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    public List<SensorDefinition> Sensors => _runtime.Sensors.Where(x => _access.CanAccessPond(_principal, x.PondId)).ToList();
    public List<Device> Devices => _runtime.Devices.Where(x => _access.CanAccessPond(_principal, x.PondId)).ToList();
    public List<AlertItem> Alerts => _runtime.Alerts.Where(x => _access.CanAccessPond(_principal, x.PondId)).ToList();
    public List<FeedLog> FeedLogs => _runtime.FeedLogs.Where(x => _access.CanAccessPond(_principal, x.PondId)).ToList();
    public List<AutomationLog> AutomationLogs => _runtime.AutomationLogs.Where(x => _access.CanAccessPond(_principal, x.PondId)).ToList();
    public List<DataSourceHealth> DataSources => _principal.Identity?.IsAuthenticated == true ? _runtime.DataSources : [];
    public List<CalibrationRecord> CalibrationRecords
    {
        get
        {
            var sensorIds = Sensors.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _runtime.CalibrationRecords.Where(x => sensorIds.Contains(x.SensorId)).ToList();
        }
    }
    public List<MaintenanceTask> MaintenanceTasks => _runtime.MaintenanceTasks.Where(x => _access.CanAccessPond(_principal, x.PondId)).ToList();
    public List<ProductionCycle> ProductionCycles => _runtime.ProductionCycles.Where(x => _access.CanAccessPond(_principal, x.PondId)).ToList();

    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated == true;
    public string CurrentDisplayName => FarmAccessService.DisplayName(_principal);
    public bool IsDarkMode { get; private set; }
    public string SelectedPondId { get; private set; }
    public long Revision => _runtime.Revision;
    public Pond? SelectedPond => Ponds.FirstOrDefault(p => p.Id == SelectedPondId) ?? Ponds.FirstOrDefault();

    public TelemetryData GetTelemetry(string pondId)
    {
        if (!_access.CanAccessPond(_principal, pondId))
            return new TelemetryData { Id = pondId, PondId = pondId, IsOnline = false, Quality = "no-access" };
        return _runtime.GetTelemetry(pondId);
    }

    /// <summary>
    /// Loads the cookie principal into this Blazor circuit before farm data is projected.
    /// Layouts call this once and trigger a refresh for their child components.
    /// </summary>
    public async Task InitializeUserAsync()
    {
        _principal = (await _authenticationStateProvider.GetAuthenticationStateAsync()).User;
        if (string.IsNullOrWhiteSpace(SelectedPondId) || !Ponds.Any(x => x.Id == SelectedPondId))
            SelectedPondId = Ponds.FirstOrDefault()?.Id ?? string.Empty;
        Changed?.Invoke();
    }

    public void SelectPond(string pondId)
    {
        if (Ponds.Any(p => p.Id == pondId))
        {
            SelectedPondId = pondId;
            Changed?.Invoke();
        }
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        Changed?.Invoke();
    }

    public async Task<Pond> AddPondAsync(AddPondRequest request, CancellationToken ct = default)
    {
        _access.EnsureCanManageFarm(_principal);
        var id = $"pond_{Guid.NewGuid():N}"[..13];
        var growDays = request.Species.Contains("cá", StringComparison.OrdinalIgnoreCase)
            ? _behavior.CurrentValue.DefaultFishGrowDays
            : _behavior.CurrentValue.DefaultShrimpGrowDays;
        var pond = new Pond
        {
            Id = id,
            Name = request.Name.Trim(),
            Species = request.Species,
            Area = request.Area,
            StockingDensity = request.StockingDensity,
            StockingDate = request.StockingDate,
            EstimatedHarvestDate = request.StockingDate.AddDays(growDays),
            Status = "active",
            UpdatedAtUtc = DateTime.UtcNow
        };

        var telemetry = new TelemetryData
        {
            Id = id,
            PondId = id,
            Timestamp = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.MinValue,
            IsOnline = false,
            Quality = "no-data",
            SourceType = "mqtt"
        };
        // A pond starts with no sensors. Field assets are explicitly mapped to a socket and
        // channel by an administrator, avoiding six fictitious probes on every pond.
        var sensors = new List<SensorDefinition>();

        // Device addresses are intentionally not generated. Every new actuator must receive an
        // explicit, reviewed PLC mapping before it can be controlled.
        var devices = new List<Device>();

        _runtime.AddPond(pond, telemetry, sensors, devices);
        SelectedPondId = id;
        await _writeQueue.EnqueueAsync(async writeCt =>
        {
            await _repository.UpsertPondAsync(pond, writeCt);
            await _repository.UpsertTelemetryAsync(telemetry, writeCt);
            foreach (var sensor in sensors) await _repository.UpsertSensorAsync(sensor, writeCt);
            foreach (var device in devices) await _repository.UpsertDeviceAsync(device, writeCt);
        }, ct);
        return pond;
    }

    public async Task<bool> DeletePondAsync(string pondId, CancellationToken ct = default)
    {
        _access.EnsureCanManageFarm(_principal);
        _access.EnsureCanViewPond(_principal, pondId);
        var removed = _runtime.DeletePond(pondId);
        if (!removed) return false;
        if (SelectedPondId == pondId) SelectedPondId = Ponds.FirstOrDefault()?.Id ?? string.Empty;
        await _writeQueue.EnqueueAsync(writeCt => _repository.DeletePondAsync(pondId, writeCt), ct);
        return true;
    }

    public async Task<Device> AddDeviceAsync(AddDeviceRequest request, CancellationToken ct = default)
    {
        _access.EnsureCanManageFarm(_principal);
        _access.EnsureCanViewPond(_principal, request.PondId);
        var socket = await _systemConfiguration.GetSocketAsync(request.SocketId, ct);
        if (socket is null || socket.Transport != "modbus-tcp" || socket.PondId != request.PondId || !socket.IsEnabled)
            throw new InvalidOperationException("Thiết bị phải dùng một Modbus TCP socket đang Enable và cùng ao.");
        var mappings = new[] { request.ManualStartRequestVariable, request.ManualStopRequestVariable,
            request.AutoRequestVariable, request.AutoFeedbackVariable, request.LocalOverrideVariable,
            request.RemoteAllowedVariable, request.RunFeedbackVariable, request.FaultVariable }
            .Select(x => x.Trim().ToUpperInvariant()).ToArray();
        if (mappings.Any(string.IsNullOrWhiteSpace) || mappings.Distinct(StringComparer.OrdinalIgnoreCase).Count() != mappings.Length)
            throw new InvalidOperationException("Phải nhập đủ các M variables và không được trùng trong cùng thiết bị.");
        var used = Devices.Where(x => x.SocketId == request.SocketId).SelectMany(x => new[]
        { x.ManualStartRequestVariable, x.ManualStopRequestVariable, x.AutoRequestVariable, x.AutoFeedbackVariable,
          x.LocalOverrideVariable, x.RemoteAllowedVariable, x.RunFeedbackVariable, x.FaultVariable })
          .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflict = mappings.FirstOrDefault(used.Contains);
        if (conflict is not null) throw new InvalidOperationException($"PLC variable {conflict} đã được thiết bị khác sử dụng trên socket.");
        var device = new Device
        {
            PondId = request.PondId,
            Name = request.Name.Trim(),
            Type = request.Type,
            PowerKw = request.PowerKw,
            Status = "off",
            Mode = "remote-manual",
            ReportedMode = "unknown",
            ControlTransport = request.ControlTransport.Trim(),
            ExternalAddress = request.ExternalAddress.Trim(),
            SocketId = request.SocketId,
            ManualStartRequestVariable = request.ManualStartRequestVariable.ToUpperInvariant(),
            ManualStopRequestVariable = request.ManualStopRequestVariable.ToUpperInvariant(),
            LocalOverrideVariable = request.LocalOverrideVariable.ToUpperInvariant(),
            RemoteAllowedVariable = request.RemoteAllowedVariable.ToUpperInvariant(),
            RunFeedbackVariable = request.RunFeedbackVariable.ToUpperInvariant(),
            FaultVariable = request.FaultVariable.ToUpperInvariant(),
            AutoRequestVariable = request.AutoRequestVariable.ToUpperInvariant(),
            AutoFeedbackVariable = request.AutoFeedbackVariable.ToUpperInvariant(),
            FailSafeState = request.FailSafeState,
            ConnectionStatus = "offline"
        };
        _runtime.AddDevice(device);
        await AddAutomationLogAsync(request.PondId, $"Đã khai báo thiết bị mới: {device.Name} ({GetDeviceTypeLabel(device.Type)}).", string.Empty, null, ct);
        await _writeQueue.EnqueueAsync(writeCt => _repository.UpsertDeviceAsync(device, writeCt), ct);
        await _repository.InsertAuditLogAsync(new AuditLog
        {
            Actor = FarmAccessService.ActorName(_principal), Action = "device.create", EntityType = "device",
            EntityId = device.Id, PondId = device.PondId, NewValue = $"{device.Name} · socket {device.SocketId}", Result = "success"
        }, ct);
        return device;
    }

    public async Task<DeviceCommand?> ToggleDeviceAsync(string deviceId, string reason = "manual-ui", CancellationToken ct = default)
    {
        var device = Devices.FirstOrDefault(x => x.Id == deviceId);
        if (device is null) return null;
        _access.EnsureCanOperatePond(_principal, device.PondId);
        var desired = device.Status == "on" ? "off" : "on";
        var actor = FarmAccessService.ActorName(_principal);
        var command = await _deviceCommands.ExecuteAsync(device.Id, desired, actor, reason, ct);
        var message = command.Status == "completed"
            ? $"Điều khiển thủ công: {device.Name} chuyển sang {(desired == "on" ? "BẬT" : "TẮT")}."
            : $"Lệnh {device.Name} thất bại ({command.Status}): {command.Error}";
        await AddAutomationLogAsync(device.PondId, message, string.Empty, command.Id, ct);
        return command;
    }

    public async Task ToggleDeviceModeAsync(string deviceId, string reason = "mode-change-ui", CancellationToken ct = default)
    {
        var device = Devices.FirstOrDefault(x => x.Id == deviceId);
        if (device is null) return;
        _access.EnsureCanOperatePond(_principal, device.PondId);
        if (device.IsLocalOverrideActive || device.ReportedMode == "local-manual")
            throw new InvalidOperationException("Thiết bị đang ở Physical/Local Manual; không thể đổi mode từ web.");
        var targetMode = device.Mode == "auto" ? "remote-manual" : "auto";
        var command = await _deviceCommands.SetModeAsync(device.Id, targetMode, FarmAccessService.ActorName(_principal), reason, ct);
        await AddAutomationLogAsync(device.PondId,
            command.Status == "completed" ? $"{device.Name} chuyển sang {(targetMode == "auto" ? "TỰ ĐỘNG" : "REMOTE MANUAL")}." :
            $"Không đổi được mode {device.Name}: {command.Status} · {command.Error}", string.Empty, command.Id, ct);
    }

    public async Task<FeedLog> AddFeedLogAsync(AddFeedRequest request, CancellationToken ct = default)
    {
        _access.EnsureCanOperatePond(_principal, request.PondId);
        if (request.AmountKg <= 0) throw new ArgumentOutOfRangeException(nameof(request.AmountKg), "Lượng thức ăn phải lớn hơn 0.");
        var pond = Ponds.FirstOrDefault(p => p.Id == request.PondId)
            ?? throw new InvalidOperationException("Không tìm thấy ao nuôi.");
        var cycle = !string.IsNullOrWhiteSpace(request.CycleId)
            ? _runtime.ProductionCycles.FirstOrDefault(x => x.Id == request.CycleId)
            : await _productionCycles.GetActiveForPondAsync(request.PondId, ct);
        if (cycle is null || !cycle.PondId.Equals(request.PondId, StringComparison.OrdinalIgnoreCase) || !cycle.IsOpen)
            throw new InvalidOperationException("Ao chưa có vụ nuôi đang mở để ghi nhận thức ăn.");
        var log = new FeedLog
        {
            PondId = pond.Id,
            PondName = pond.Name,
            CycleId = cycle.Id,
            FeedLotId = request.FeedLotId?.Trim() ?? string.Empty,
            FeedType = request.FeedType.Trim(),
            AmountKg = request.AmountKg,
            Notes = request.Notes?.Trim(),
            Technician = FarmAccessService.DisplayName(_principal),
            Timestamp = DateTime.UtcNow
        };
        // Feeding changes FCR; persist first, then project and recalculate the cycle.
        await _repository.InsertFeedLogAsync(log, ct);
        _runtime.AddFeedLog(log);
        await _productionCycles.RecalculateAsync(cycle.Id, ct);
        return log;
    }

    public async Task AcknowledgeAlertAsync(string alertId, CancellationToken ct = default)
    {
        var current = Alerts.FirstOrDefault(x => x.Id == alertId);
        if (current is null) return;
        _access.EnsureCanOperatePond(_principal, current.PondId);
        var alert = _runtime.AcknowledgeAlert(alertId, FarmAccessService.ActorName(_principal), null);
        if (alert is not null) await _repository.UpsertAlertAsync(alert, ct);
    }

    public async Task ResolveAlertAsync(string alertId, CancellationToken ct = default)
    {
        var current = Alerts.FirstOrDefault(x => x.Id == alertId);
        if (current is null) return;
        _access.EnsureCanOperatePond(_principal, current.PondId);
        var alert = _runtime.ResolveAlert(alertId, FarmAccessService.ActorName(_principal));
        if (alert is not null)
            await _writeQueue.EnqueueAsync(writeCt => _repository.UpsertAlertAsync(alert, writeCt), ct);
    }

    public async Task ClearAutomationLogsAsync(CancellationToken ct = default)
    {
        _access.EnsureCanManageFarm(_principal);
        _runtime.ClearAutomationLogs();
        await _writeQueue.EnqueueAsync(writeCt => _repository.DeleteAutomationLogsAsync(writeCt), ct);
    }

    public async Task<CalibrationRecord> RecordCalibrationAsync(CalibrationRequest request, CancellationToken ct = default)
    {
        var sensor = _runtime.GetSensor(request.SensorId)
            ?? throw new InvalidOperationException("Không tìm thấy cảm biến cần hiệu chuẩn.");
        _access.EnsureCanOperatePond(_principal, sensor.PondId);
        var now = DateTime.UtcNow;
        var nextDue = now.AddDays(_behavior.CurrentValue.DefaultCalibrationIntervalDays);
        var record = new CalibrationRecord
        {
            SensorId = sensor.Id,
            PerformedAtUtc = now,
            NextDueAtUtc = nextDue,
            PerformedBy = request.PerformedBy.Trim(),
            ValueBefore = request.ValueBefore,
            ValueAfter = request.ValueAfter,
            Notes = request.Notes?.Trim() ?? string.Empty
        };

        var updatedSensor = _runtime.UpdateSensor(sensor.Id, x =>
        {
            x.CalibrationDateUtc = now;
            x.CalibrationDueDateUtc = nextDue;
            x.LastError = null;
        })!;
        _runtime.AddCalibrationRecord(record);

        var taskId = $"calibration:{sensor.Id}";
        var existingTask = _runtime.GetMaintenanceTask(taskId);
        MaintenanceTask? task = null;
        if (existingTask is not null)
        {
            task = new MaintenanceTask
            {
                Id = existingTask.Id,
                PondId = existingTask.PondId,
                AssetId = existingTask.AssetId,
                AssetType = existingTask.AssetType,
                Title = existingTask.Title,
                Status = "completed",
                DueAtUtc = existingTask.DueAtUtc,
                CompletedAtUtc = now,
                AssignedTo = request.PerformedBy.Trim()
            };
            _runtime.UpsertMaintenanceTask(task);
        }

        var resolvedAlert = _runtime.ResolveMetricAlert(sensor.PondId, $"maintenance:{sensor.Id}", request.PerformedBy.Trim());
        await _writeQueue.EnqueueAsync(async writeCt =>
        {
            await _repository.InsertCalibrationRecordAsync(record, writeCt);
            await _repository.UpsertSensorAsync(updatedSensor, writeCt);
            if (task is not null) await _repository.UpsertMaintenanceTaskAsync(task, writeCt);
            if (resolvedAlert is not null) await _repository.UpsertAlertAsync(resolvedAlert, writeCt);
            await _repository.InsertAuditLogAsync(new AuditLog
            {
                Actor = request.PerformedBy.Trim(),
                Action = "calibrate-sensor",
                EntityType = "sensor",
                EntityId = sensor.Id,
                PondId = sensor.PondId,
                PreviousValue = request.ValueBefore?.ToString(CultureInfo.InvariantCulture),
                NewValue = request.ValueAfter?.ToString(CultureInfo.InvariantCulture)
            }, writeCt);
        }, ct);
        return record;
    }

    public double GetSensorCurrentValue(SensorDefinition sensor) => GetMetricValue(GetTelemetry(sensor.PondId), sensor.Metric);

    public void Tick() => _runtime.Refresh();

    public IReadOnlyList<TrendPoint> GetTrend(string pondId, string metric, int count = 24)
    {
        var points = _runtime.GetTrend(pondId, metric, count);
        if (points.Count > 0) return points;
        return [new TrendPoint { Label = DateTime.Now.ToString("HH:mm"), Value = GetMetricValue(GetTelemetry(pondId), metric) }];
    }

    public string AnalyzePond(string pondId, string prompt)
    {
        var pond = Ponds.FirstOrDefault(p => p.Id == pondId);
        if (pond is null) return "Không tìm thấy dữ liệu ao nuôi để phân tích.";
        var telemetry = GetTelemetry(pondId);
        var activeAlerts = Alerts.Where(x => x.PondId == pondId && !x.IsResolved).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"PHÂN TÍCH CHO {pond.Name.ToUpperInvariant()}");
        sb.AppendLine($"Yêu cầu: {(!string.IsNullOrWhiteSpace(prompt) ? prompt.Trim() : "Đánh giá nhanh tình trạng ao")}");
        sb.AppendLine();
        sb.AppendLine(telemetry.IsOnline
            ? $"Dữ liệu gần nhất: {telemetry.Timestamp.ToLocalTime():dd/MM/yyyy HH:mm:ss}, nguồn {telemetry.SourceType}, chất lượng {telemetry.Quality}."
            : "Hiện không có dữ liệu cảm biến trực tuyến đáng tin cậy.");
        if (activeAlerts.Count == 0)
        {
            sb.AppendLine("Không có cảnh báo đang hoạt động. Tiếp tục đo đối chứng và theo dõi xu hướng.");
        }
        else
        {
            sb.AppendLine("Cảnh báo cần xử lý:");
            foreach (var alert in activeAlerts.OrderByDescending(x => x.Severity == "critical"))
                sb.AppendLine($"• {alert.Message} Đề xuất: {alert.ProposedSolution}");
        }
        sb.AppendLine();
        sb.AppendLine("Lưu ý: phân tích sử dụng dữ liệu đang có trong hệ thống; quyết định vận hành phải tuân theo đo đối chứng và liên động tại PLC.");
        return sb.ToString();
    }

    public MetricThresholdOptions GetThreshold(string metric)
    {
        if (_thresholds.CurrentValue.Metrics.TryGetValue(metric, out var value)) return value;
        throw new KeyNotFoundException($"Metric '{metric}' chưa được cấu hình trong sensor-thresholds.json.");
    }

    public bool IsMetricSafe(string metric, double value)
    {
        var threshold = GetThreshold(metric);
        var low = threshold.WarningLow ?? threshold.ValidMin;
        var high = threshold.WarningHigh ?? threshold.ValidMax;
        return value > low && value < high;
    }

    public string GetTargetText(string metric)
    {
        var threshold = GetThreshold(metric);
        if (threshold.WarningLow.HasValue && threshold.WarningHigh.HasValue)
            return $"Vùng theo dõi: {threshold.WarningLow.Value}–{threshold.WarningHigh.Value} {threshold.Unit}";
        if (threshold.WarningLow.HasValue) return $"Vùng theo dõi: > {threshold.WarningLow.Value} {threshold.Unit}";
        if (threshold.WarningHigh.HasValue) return $"Vùng theo dõi: < {threshold.WarningHigh.Value} {threshold.Unit}";
        return $"Miền hợp lệ: {threshold.ValidMin}–{threshold.ValidMax} {threshold.Unit}";
    }

    public int CalculateHealthScore(TelemetryData telemetry)
    {
        var options = _behavior.CurrentValue.HealthScore;
        if (!telemetry.IsOnline || telemetry.Quality is "invalid" or "no-data") return options.OfflineScore;
        var checks = new[]
        {
            IsMetricSafe("dissolvedOxygen", telemetry.DissolvedOxygen),
            IsMetricSafe("ph", telemetry.Ph),
            IsMetricSafe("temperature", telemetry.Temperature),
            IsMetricSafe("salinity", telemetry.Salinity),
            IsMetricSafe("ammonia", telemetry.Ammonia),
            IsMetricSafe("waterLevel", telemetry.WaterLevel)
        };
        var score = options.InitialScore - checks.Count(x => !x) * options.MetricOutOfRangePenalty;
        var criticalCount = Alerts.Count(x => x.PondId == telemetry.PondId && !x.IsResolved && x.Severity == "critical");
        return Math.Clamp(score - criticalCount * options.CriticalAlertPenalty, 0, 100);
    }

    public HealthScoreOptions GetHealthScoreOptions() => _behavior.CurrentValue.HealthScore;

    public static string GetDeviceTypeLabel(string type) => type switch
    {
        "aerator" => "Máy sục khí",
        "auto_feeder" => "Máy cho ăn tự động",
        "water_pump" => "Máy bơm nước",
        "light" => "Hệ thống chiếu sáng",
        _ => "Giàn quạt nước"
    };

    public static string GetMetricLabel(string metric)
    {
        if (metric.StartsWith("connectivity:", StringComparison.OrdinalIgnoreCase)) return "Kết nối cảm biến";
        if (metric.StartsWith("quality:", StringComparison.OrdinalIgnoreCase)) return "Chất lượng dữ liệu";
        return metric switch
        {
            "ph" => "Độ pH",
            "temperature" => "Nhiệt độ",
            "salinity" => "Độ mặn",
            "ammonia" => "Khí NH₃",
            "waterLevel" => "Mực nước",
            _ => "Oxy hòa tan"
        };
    }

    private async Task AddAutomationLogAsync(
        string pondId,
        string message,
        string metric,
        string? commandId,
        CancellationToken ct)
    {
        var pond = Ponds.FirstOrDefault(p => p.Id == pondId);
        var log = new AutomationLog
        {
            PondId = pondId,
            PondName = pond?.Name ?? "Ao nuôi",
            Message = message,
            TriggerMetric = metric,
            DeviceCommandId = commandId,
            Timestamp = DateTime.UtcNow
        };
        _runtime.AddAutomationLog(log);
        await _writeQueue.EnqueueAsync(writeCt => _repository.InsertAutomationLogAsync(log, writeCt), ct);
    }

    private static double GetMetricValue(TelemetryData telemetry, string metric) => metric switch
    {
        "temperature" => telemetry.Temperature,
        "ph" => telemetry.Ph,
        "salinity" => telemetry.Salinity,
        "ammonia" => telemetry.Ammonia,
        "waterLevel" => telemetry.WaterLevel,
        _ => telemetry.DissolvedOxygen
    };

    private void ForwardChanged() => Changed?.Invoke();

    public void Dispose() => _runtime.Changed -= ForwardChanged;
}
