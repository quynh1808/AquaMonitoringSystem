using AquacultureMonitoring.Models;
using MongoDB.Driver;

namespace AquacultureMonitoring.Data;

public sealed class FarmRepository
{
    private readonly MongoContext _db;

    public FarmRepository(MongoContext db) => _db = db;

    public async Task<FarmSnapshot> LoadSnapshotAsync(int readingLimit, CancellationToken cancellationToken)
    {
        var pondsTask = _db.Ponds.Find(FilterDefinition<Pond>.Empty).ToListAsync(cancellationToken);
        var telemetryTask = _db.LatestTelemetry.Find(FilterDefinition<TelemetryData>.Empty).ToListAsync(cancellationToken);
        var sensorsTask = _db.Sensors.Find(FilterDefinition<SensorDefinition>.Empty).ToListAsync(cancellationToken);
        var readingsTask = _db.SensorReadings.Find(FilterDefinition<SensorReading>.Empty)
            .SortByDescending(x => x.Timestamp)
            .Limit(readingLimit)
            .ToListAsync(cancellationToken);
        var devicesTask = _db.Devices.Find(FilterDefinition<Device>.Empty).ToListAsync(cancellationToken);
        var commandsTask = _db.DeviceCommands.Find(FilterDefinition<DeviceCommand>.Empty)
            .SortByDescending(x => x.CreatedAtUtc).Limit(300).ToListAsync(cancellationToken);
        var alertsTask = _db.Alerts.Find(FilterDefinition<AlertItem>.Empty)
            .SortByDescending(x => x.Timestamp).Limit(500).ToListAsync(cancellationToken);
        var feedsTask = _db.FeedLogs.Find(FilterDefinition<FeedLog>.Empty)
            .SortByDescending(x => x.Timestamp).Limit(1000).ToListAsync(cancellationToken);
        var automationsTask = _db.AutomationLogs.Find(FilterDefinition<AutomationLog>.Empty)
            .SortByDescending(x => x.Timestamp).Limit(500).ToListAsync(cancellationToken);
        var calibrationsTask = _db.CalibrationRecords.Find(FilterDefinition<CalibrationRecord>.Empty)
            .SortByDescending(x => x.PerformedAtUtc).Limit(1000).ToListAsync(cancellationToken);
        var maintenanceTask = _db.MaintenanceTasks.Find(FilterDefinition<MaintenanceTask>.Empty)
            .SortBy(x => x.DueAtUtc).Limit(1000).ToListAsync(cancellationToken);
        var sourceHealthTask = _db.DataSourceHealth.Find(FilterDefinition<DataSourceHealth>.Empty).ToListAsync(cancellationToken);
        var cyclesTask = _db.ProductionCycles.Find(FilterDefinition<ProductionCycle>.Empty).ToListAsync(cancellationToken);

        await Task.WhenAll(pondsTask, telemetryTask, sensorsTask, readingsTask, devicesTask, commandsTask,
            alertsTask, feedsTask, automationsTask, calibrationsTask, maintenanceTask, sourceHealthTask, cyclesTask);

        return new FarmSnapshot
        {
            Ponds = await pondsTask,
            Telemetry = (await telemetryTask).ToDictionary(x => x.PondId, StringComparer.OrdinalIgnoreCase),
            Sensors = await sensorsTask,
            RecentReadings = (await readingsTask).OrderBy(x => x.Timestamp).ToList(),
            Devices = await devicesTask,
            DeviceCommands = await commandsTask,
            Alerts = await alertsTask,
            FeedLogs = await feedsTask,
            AutomationLogs = await automationsTask,
            CalibrationRecords = await calibrationsTask,
            MaintenanceTasks = await maintenanceTask,
            DataSources = await sourceHealthTask,
            ProductionCycles = await cyclesTask
        };
    }

    public Task UpsertPondAsync(Pond item, CancellationToken ct) =>
        _db.Ponds.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task UpsertTelemetryAsync(TelemetryData item, CancellationToken ct)
    {
        item.Id = item.PondId;
        return _db.LatestTelemetry.ReplaceOneAsync(x => x.PondId == item.PondId, item,
            new ReplaceOptions { IsUpsert = true }, ct);
    }

    public Task UpsertSensorAsync(SensorDefinition item, CancellationToken ct) =>
        _db.Sensors.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task InsertReadingAsync(SensorReading item, CancellationToken ct) =>
        _db.SensorReadings.InsertOneAsync(item, cancellationToken: ct);

    public Task UpsertDeviceAsync(Device item, CancellationToken ct) =>
        _db.Devices.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    /// <summary>
    /// Persists both the pre-dispatch intent and the final acknowledgement with the same id.
    /// Upsert makes retries idempotent and prevents duplicate command history entries.
    /// </summary>
    public Task UpsertDeviceCommandAsync(DeviceCommand item, CancellationToken ct) =>
        _db.DeviceCommands.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task UpsertAlertAsync(AlertItem item, CancellationToken ct) =>
        _db.Alerts.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task InsertAlertNotificationAsync(AlertNotification item, CancellationToken ct) =>
        _db.AlertNotifications.InsertOneAsync(item, cancellationToken: ct);

    public Task UpsertAlarmRuleAsync(AlarmRule item, CancellationToken ct) =>
        _db.AlarmRules.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task InsertFeedLogAsync(FeedLog item, CancellationToken ct) =>
        _db.FeedLogs.InsertOneAsync(item, cancellationToken: ct);

    public Task InsertAutomationLogAsync(AutomationLog item, CancellationToken ct) =>
        _db.AutomationLogs.InsertOneAsync(item, cancellationToken: ct);

    public Task InsertAuditLogAsync(AuditLog item, CancellationToken ct) =>
        _db.AuditLogs.InsertOneAsync(item, cancellationToken: ct);

    public Task InsertCalibrationRecordAsync(CalibrationRecord item, CancellationToken ct) =>
        _db.CalibrationRecords.InsertOneAsync(item, cancellationToken: ct);

    public Task UpsertMaintenanceTaskAsync(MaintenanceTask item, CancellationToken ct) =>
        _db.MaintenanceTasks.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task UpsertSourceHealthAsync(DataSourceHealth item, CancellationToken ct) =>
        _db.DataSourceHealth.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public async Task DeletePondAsync(string pondId, CancellationToken ct)
    {
        var sensorIds = await _db.Sensors.Find(x => x.PondId == pondId)
            .Project(x => x.Id).ToListAsync(ct);
        await Task.WhenAll(
            _db.Ponds.DeleteOneAsync(x => x.Id == pondId, ct),
            _db.LatestTelemetry.DeleteOneAsync(x => x.PondId == pondId, ct),
            _db.Sensors.DeleteManyAsync(x => x.PondId == pondId, ct),
            _db.SensorReadings.DeleteManyAsync(x => x.Metadata.PondId == pondId, ct),
            _db.Devices.DeleteManyAsync(x => x.PondId == pondId, ct),
            _db.DeviceCommands.DeleteManyAsync(x => x.PondId == pondId, ct),
            _db.Alerts.DeleteManyAsync(x => x.PondId == pondId, ct),
            _db.FeedLogs.DeleteManyAsync(x => x.PondId == pondId, ct),
            _db.AutomationLogs.DeleteManyAsync(x => x.PondId == pondId, ct),
            _db.CalibrationRecords.DeleteManyAsync(x => sensorIds.Contains(x.SensorId), ct),
            _db.MaintenanceTasks.DeleteManyAsync(x => x.PondId == pondId, ct));
    }

    public Task DeleteAutomationLogsAsync(CancellationToken ct) =>
        _db.AutomationLogs.DeleteManyAsync(FilterDefinition<AutomationLog>.Empty, ct);
}
