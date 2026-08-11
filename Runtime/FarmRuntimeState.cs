using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Runtime;

public sealed class FarmRuntimeState
{
    private readonly object _sync = new();
    private readonly int _readingLimit;
    private readonly List<Pond> _ponds = [];
    private readonly Dictionary<string, TelemetryData> _telemetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SensorDefinition> _sensors = [];
    private readonly List<SensorReading> _readings = [];
    private readonly List<Device> _devices = [];
    private readonly List<DeviceCommand> _deviceCommands = [];
    private readonly List<AlertItem> _alerts = [];
    private readonly List<FeedLog> _feedLogs = [];
    private readonly List<AutomationLog> _automationLogs = [];
    private readonly List<CalibrationRecord> _calibrationRecords = [];
    private readonly List<MaintenanceTask> _maintenanceTasks = [];
    private readonly List<DataSourceHealth> _dataSources = [];

    public FarmRuntimeState(IOptions<SystemBehaviorOptions> behavior) => _readingLimit = behavior.Value.RuntimeReadingLimit;

    public event Action? Changed;
    public bool IsInitialized { get; private set; }
    public long Revision { get; private set; }

    public List<Pond> Ponds { get { lock (_sync) return [.. _ponds]; } }
    public Dictionary<string, TelemetryData> Telemetry { get { lock (_sync) return new(_telemetry, StringComparer.OrdinalIgnoreCase); } }
    public List<SensorDefinition> Sensors { get { lock (_sync) return [.. _sensors]; } }
    public List<Device> Devices { get { lock (_sync) return [.. _devices]; } }
    public List<DeviceCommand> DeviceCommands { get { lock (_sync) return [.. _deviceCommands]; } }
    public List<AlertItem> Alerts { get { lock (_sync) return [.. _alerts]; } }
    public List<FeedLog> FeedLogs { get { lock (_sync) return [.. _feedLogs]; } }
    public List<AutomationLog> AutomationLogs { get { lock (_sync) return [.. _automationLogs]; } }
    public List<CalibrationRecord> CalibrationRecords { get { lock (_sync) return [.. _calibrationRecords]; } }
    public List<MaintenanceTask> MaintenanceTasks { get { lock (_sync) return [.. _maintenanceTasks]; } }
    public List<DataSourceHealth> DataSources { get { lock (_sync) return [.. _dataSources]; } }

    public void Initialize(FarmSnapshot snapshot)
    {
        lock (_sync)
        {
            _ponds.Clear(); _ponds.AddRange(snapshot.Ponds);
            _telemetry.Clear();
            foreach (var item in snapshot.Telemetry) _telemetry[item.Key] = item.Value;
            _sensors.Clear(); _sensors.AddRange(snapshot.Sensors);
            _readings.Clear(); _readings.AddRange(snapshot.RecentReadings.TakeLast(_readingLimit));
            _devices.Clear(); _devices.AddRange(snapshot.Devices);
            _deviceCommands.Clear(); _deviceCommands.AddRange(snapshot.DeviceCommands);
            _alerts.Clear(); _alerts.AddRange(snapshot.Alerts);
            _feedLogs.Clear(); _feedLogs.AddRange(snapshot.FeedLogs);
            _automationLogs.Clear(); _automationLogs.AddRange(snapshot.AutomationLogs);
            _calibrationRecords.Clear(); _calibrationRecords.AddRange(snapshot.CalibrationRecords);
            _maintenanceTasks.Clear(); _maintenanceTasks.AddRange(snapshot.MaintenanceTasks);
            _dataSources.Clear(); _dataSources.AddRange(snapshot.DataSources);
            IsInitialized = true;
        }
        NotifyChanged();
    }

    public Pond? GetPond(string pondId)
    {
        lock (_sync) return _ponds.FirstOrDefault(x => x.Id == pondId);
    }

    public TelemetryData GetTelemetry(string pondId)
    {
        lock (_sync)
        {
            if (_telemetry.TryGetValue(pondId, out var value)) return value;
            var created = new TelemetryData { Id = pondId, PondId = pondId, IsOnline = false, Quality = "no-data", Timestamp = DateTime.UtcNow };
            _telemetry[pondId] = created;
            return created;
        }
    }

    public SensorDefinition? GetSensor(string sensorId)
    {
        lock (_sync) return _sensors.FirstOrDefault(x => x.Id == sensorId);
    }

    public void AddPond(Pond pond, TelemetryData telemetry, IEnumerable<SensorDefinition> sensors, IEnumerable<Device> devices)
    {
        lock (_sync)
        {
            _ponds.Add(pond);
            _telemetry[pond.Id] = telemetry;
            _sensors.AddRange(sensors);
            _devices.AddRange(devices);
        }
        NotifyChanged();
    }

    public bool DeletePond(string pondId)
    {
        bool removed;
        lock (_sync)
        {
            var sensorIds = _sensors.Where(x => x.PondId == pondId).Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            removed = _ponds.RemoveAll(x => x.Id == pondId) > 0;
            _telemetry.Remove(pondId);
            _sensors.RemoveAll(x => x.PondId == pondId);
            _readings.RemoveAll(x => x.Metadata.PondId == pondId);
            _devices.RemoveAll(x => x.PondId == pondId);
            _deviceCommands.RemoveAll(x => x.PondId == pondId);
            _alerts.RemoveAll(x => x.PondId == pondId);
            _feedLogs.RemoveAll(x => x.PondId == pondId);
            _automationLogs.RemoveAll(x => x.PondId == pondId);
            _calibrationRecords.RemoveAll(x => sensorIds.Contains(x.SensorId));
            _maintenanceTasks.RemoveAll(x => x.PondId == pondId);
        }
        if (removed) NotifyChanged();
        return removed;
    }

    public void AddDevice(Device device)
    {
        lock (_sync) _devices.Add(device);
        NotifyChanged();
    }

    public Device? UpdateDevice(string deviceId, Action<Device> change)
    {
        Device? device;
        lock (_sync)
        {
            device = _devices.FirstOrDefault(x => x.Id == deviceId);
            if (device is null) return null;
            change(device);
            device.UpdatedAtUtc = DateTime.UtcNow;
        }
        NotifyChanged();
        return device;
    }

    public void AddDeviceCommand(DeviceCommand command)
    {
        lock (_sync)
        {
            _deviceCommands.Insert(0, command);
            if (_deviceCommands.Count > 1000) _deviceCommands.RemoveRange(1000, _deviceCommands.Count - 1000);
        }
        NotifyChanged();
    }

    public void AddFeedLog(FeedLog feedLog)
    {
        lock (_sync) _feedLogs.Insert(0, feedLog);
        NotifyChanged();
    }

    public AlertItem? GetActiveAlert(string pondId, string metric)
    {
        lock (_sync) return _alerts.FirstOrDefault(x => x.PondId == pondId && x.Metric == metric && !x.IsResolved);
    }

    public void UpsertAlert(AlertItem alert)
    {
        lock (_sync)
        {
            var index = _alerts.FindIndex(x => x.Id == alert.Id);
            if (index >= 0) _alerts[index] = alert;
            else _alerts.Insert(0, alert);
        }
        NotifyChanged();
    }

    public AlertItem? ResolveAlert(string alertId, string actor)
    {
        AlertItem? alert;
        lock (_sync)
        {
            alert = _alerts.FirstOrDefault(x => x.Id == alertId);
            if (alert is null) return null;
            alert.IsResolved = true;
            alert.Status = "resolved";
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedBy = actor;
            alert.LastUpdatedAtUtc = DateTime.UtcNow;
        }
        NotifyChanged();
        return alert;
    }

    public AlertItem? ResolveMetricAlert(string pondId, string metric, string actor)
    {
        AlertItem? alert;
        lock (_sync)
        {
            alert = _alerts.FirstOrDefault(x => x.PondId == pondId && x.Metric == metric && !x.IsResolved);
            if (alert is null) return null;
            alert.IsResolved = true;
            alert.Status = "resolved";
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedBy = actor;
            alert.LastUpdatedAtUtc = DateTime.UtcNow;
        }
        NotifyChanged();
        return alert;
    }

    public void AddAutomationLog(AutomationLog log)
    {
        lock (_sync)
        {
            _automationLogs.Insert(0, log);
            if (_automationLogs.Count > 1000) _automationLogs.RemoveRange(1000, _automationLogs.Count - 1000);
        }
        NotifyChanged();
    }

    public void ClearAutomationLogs()
    {
        lock (_sync) _automationLogs.Clear();
        NotifyChanged();
    }

    public (SensorDefinition Sensor, TelemetryData Telemetry) ApplyReading(SensorReading reading, SensorDefinition sensor)
    {
        TelemetryData telemetry;
        lock (_sync)
        {
            var sensorIndex = _sensors.FindIndex(x => x.Id == sensor.Id);
            if (sensorIndex >= 0) _sensors[sensorIndex] = sensor;
            else _sensors.Add(sensor);

            _readings.Add(reading);
            if (_readings.Count > _readingLimit) _readings.RemoveRange(0, _readings.Count - _readingLimit);

            if (!_telemetry.TryGetValue(sensor.PondId, out telemetry!))
            {
                telemetry = new TelemetryData { Id = sensor.PondId, PondId = sensor.PondId };
                _telemetry[sensor.PondId] = telemetry;
            }

            MongoDatabaseInitializer.ApplyMetric(telemetry, sensor.Metric, reading.Value);
            telemetry.Timestamp = reading.Timestamp;
            telemetry.LastSeenAtUtc = reading.ReceivedAtUtc;
            telemetry.IsOnline = true;
            telemetry.Quality = reading.Quality;
            telemetry.SourceType = sensor.SourceType;
        }
        NotifyChanged();
        return (sensor, telemetry);
    }

    public SensorDefinition? UpdateSensor(string sensorId, Action<SensorDefinition> change, bool notify = true)
    {
        SensorDefinition? sensor;
        lock (_sync)
        {
            sensor = _sensors.FirstOrDefault(x => x.Id == sensorId);
            if (sensor is null) return null;
            change(sensor);
            sensor.UpdatedAtUtc = DateTime.UtcNow;
        }
        if (notify) NotifyChanged();
        return sensor;
    }

    public TelemetryData? UpdateTelemetry(string pondId, Action<TelemetryData> change, bool notify = true)
    {
        TelemetryData? telemetry;
        lock (_sync)
        {
            if (!_telemetry.TryGetValue(pondId, out telemetry)) return null;
            change(telemetry);
        }
        if (notify) NotifyChanged();
        return telemetry;
    }

    public void AddCalibrationRecord(CalibrationRecord record)
    {
        lock (_sync)
        {
            _calibrationRecords.Insert(0, record);
            if (_calibrationRecords.Count > 2000)
                _calibrationRecords.RemoveRange(2000, _calibrationRecords.Count - 2000);
        }
        NotifyChanged();
    }

    public void UpsertMaintenanceTask(MaintenanceTask task)
    {
        lock (_sync)
        {
            var index = _maintenanceTasks.FindIndex(x => x.Id == task.Id);
            if (index >= 0) _maintenanceTasks[index] = task;
            else _maintenanceTasks.Add(task);
        }
        NotifyChanged();
    }

    public MaintenanceTask? GetMaintenanceTask(string taskId)
    {
        lock (_sync) return _maintenanceTasks.FirstOrDefault(x => x.Id == taskId);
    }

    public void UpsertDataSourceHealth(DataSourceHealth health)
    {
        lock (_sync)
        {
            var index = _dataSources.FindIndex(x => x.Id == health.Id);
            if (index >= 0) _dataSources[index] = health;
            else _dataSources.Add(health);
        }
        NotifyChanged();
    }

    public IReadOnlyList<TrendPoint> GetTrend(string pondId, string metric, int count)
    {
        lock (_sync)
        {
            return _readings
                .Where(x => x.Metadata.PondId == pondId && x.Metadata.Metric.Equals(metric, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .OrderBy(x => x.Timestamp)
                .Select(x => new TrendPoint
                {
                    Label = x.Timestamp.ToLocalTime().ToString("HH:mm"),
                    Value = x.Value
                })
                .ToList();
        }
    }

    public void Refresh() => NotifyChanged();

    private void NotifyChanged()
    {
        Revision++;
        Changed?.Invoke();
    }
}
