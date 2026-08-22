using AquacultureMonitoring.Models;
using Microsoft.EntityFrameworkCore;

namespace AquacultureMonitoring.Data;

/// <summary>
/// PostgreSQL persistence boundary for the live farm projection. A fresh DbContext is created per
/// operation because this repository is consumed by singleton protocol workers as well as UI scopes.
/// </summary>
public sealed class FarmRepository(IDbContextFactory<AquaDbContext> factory)
{
    public async Task<FarmSnapshot> LoadSnapshotAsync(int readingLimit, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var ponds = await db.Ponds.AsNoTracking().Where(x => x.Status != "archived").ToListAsync(ct);
        var latest = await db.LatestSensorStates.AsNoTracking().ToListAsync(ct);

        return new FarmSnapshot
        {
            Ponds = ponds,
            Telemetry = BuildPondTelemetry(ponds, latest),
            Sensors = await db.Sensors.AsNoTracking().Where(x => !x.IsArchived).ToListAsync(ct),
            RecentReadings = (await db.SensorReadings.AsNoTracking().OrderByDescending(x => x.Timestamp)
                .Take(readingLimit).ToListAsync(ct)).OrderBy(x => x.Timestamp).ToList(),
            Devices = await db.Devices.AsNoTracking().Where(x => !x.IsArchived).ToListAsync(ct),
            DeviceCommands = await db.DeviceCommands.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync(ct),
            Alerts = await db.Alerts.AsNoTracking().OrderByDescending(x => x.Timestamp).Take(500).ToListAsync(ct),
            FeedLogs = await db.FeedLogs.AsNoTracking().OrderByDescending(x => x.Timestamp).Take(1000).ToListAsync(ct),
            AutomationLogs = await db.AutomationLogs.AsNoTracking().OrderByDescending(x => x.Timestamp).Take(500).ToListAsync(ct),
            CalibrationRecords = await db.CalibrationRecords.AsNoTracking().OrderByDescending(x => x.PerformedAtUtc).Take(1000).ToListAsync(ct),
            MaintenanceTasks = await db.MaintenanceTasks.AsNoTracking().OrderBy(x => x.DueAtUtc).Take(1000).ToListAsync(ct),
            DataSources = await db.DataSourceHealth.AsNoTracking().ToListAsync(ct),
            ProductionCycles = await db.ProductionCycles.AsNoTracking().ToListAsync(ct)
        };
    }

    public Task UpsertPondAsync(Pond item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task UpsertSensorAsync(SensorDefinition item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task UpsertDeviceAsync(Device item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task UpsertDeviceCommandAsync(DeviceCommand item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task UpsertAlertAsync(AlertItem item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task UpsertAlarmRuleAsync(AlarmRule item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task UpsertMaintenanceTaskAsync(MaintenanceTask item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task UpsertSourceHealthAsync(DataSourceHealth item, CancellationToken ct) => UpsertAsync(item, item.Id, ct);
    public Task InsertAlertNotificationAsync(AlertNotification item, CancellationToken ct) => InsertAsync(item, ct);
    public Task InsertFeedLogAsync(FeedLog item, CancellationToken ct) => InsertAsync(item, ct);
    public Task InsertAutomationLogAsync(AutomationLog item, CancellationToken ct) => InsertAsync(item, ct);
    public Task InsertAuditLogAsync(AuditLog item, CancellationToken ct) => InsertAsync(item, ct);
    public Task InsertCalibrationRecordAsync(CalibrationRecord item, CancellationToken ct) => InsertAsync(item, ct);

    /// <summary>
    /// Commits the immutable reading and the per-sensor latest projection atomically. Backfill is
    /// historical only and therefore never rewinds the live dashboard projection.
    /// </summary>
    public async Task InsertReadingAsync(SensorReading item, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.SensorReadings.Add(item);
        if (!item.IsBackfill)
        {
            var latest = await db.LatestSensorStates.FindAsync([item.SensorId], ct);
            if (latest is null) db.LatestSensorStates.Add(ToLatest(item));
            else if (item.Timestamp >= latest.MeasuredAtUtc) db.Entry(latest).CurrentValues.SetValues(ToLatest(item));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Archives the pond and commissioned assets in one transaction. Measurements, audit and
    /// production history are deliberately retained for traceability.
    /// </summary>
    public async Task DeletePondAsync(string pondId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pond = await db.Ponds.FindAsync([pondId], ct);
        if (pond is null) return;
        pond.Status = "archived";
        pond.UpdatedAtUtc = DateTime.UtcNow;
        await db.Sensors.Where(x => x.PondId == pondId && !x.IsArchived).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.IsArchived, true).SetProperty(x => x.IsEnabled, false)
            .SetProperty(x => x.Status, "archived").SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), ct);
        await db.Devices.Where(x => x.PondId == pondId && !x.IsArchived).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.IsArchived, true).SetProperty(x => x.IsEnabled, false)
            .SetProperty(x => x.ConnectionStatus, "archived").SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task DeleteAutomationLogsAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.AutomationLogs.ExecuteDeleteAsync(ct);
    }

    private async Task UpsertAsync<TEntity>(TEntity item, string id, CancellationToken ct) where TEntity : class
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.UpsertAsync(item, id, ct);
    }

    private async Task InsertAsync<TEntity>(TEntity item, CancellationToken ct) where TEntity : class
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Set<TEntity>().Add(item);
        await db.SaveChangesAsync(ct);
    }

    private static LatestSensorState ToLatest(SensorReading reading) => new()
    {
        SensorId = reading.SensorId, PondId = reading.PondId, Metric = reading.Metric,
        Value = reading.Value, Unit = reading.Unit, Quality = reading.Quality,
        MeasuredAtUtc = reading.Timestamp, ReceivedAtUtc = reading.ReceivedAtUtc,
        InstallationId = reading.InstallationId
    };

    private static Dictionary<string, TelemetryData> BuildPondTelemetry(IEnumerable<Pond> ponds,
        IEnumerable<LatestSensorState> states)
    {
        var result = ponds.ToDictionary(x => x.Id, x => new TelemetryData
        {
            Id = x.Id, PondId = x.Id, Quality = "no-data", SourceType = "postgresql"
        }, StringComparer.OrdinalIgnoreCase);
        foreach (var state in states.OrderBy(x => x.MeasuredAtUtc))
        {
            if (!result.TryGetValue(state.PondId, out var telemetry)) continue;
            switch (state.Metric)
            {
                case "temperature": telemetry.Temperature = state.Value; break;
                case "ph": telemetry.Ph = state.Value; break;
                case "dissolvedOxygen": telemetry.DissolvedOxygen = state.Value; break;
                case "salinity": telemetry.Salinity = state.Value; break;
                case "ammonia": telemetry.Ammonia = state.Value; break;
                case "waterLevel": telemetry.WaterLevel = state.Value; break;
            }
            telemetry.Timestamp = state.MeasuredAtUtc;
            telemetry.LastSeenAtUtc = state.ReceivedAtUtc;
            telemetry.Quality = state.Quality;
            telemetry.IsOnline = true;
        }
        return result;
    }
}
