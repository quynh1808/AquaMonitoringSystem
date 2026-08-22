using AquacultureMonitoring.Models;
using Microsoft.EntityFrameworkCore;

namespace AquacultureMonitoring.Data;

/// <summary>
/// PostgreSQL boundary for administrator-defined node connections and desired/reported state.
/// Workers never read connection endpoints from source files; every active endpoint comes here.
/// </summary>
public sealed class SystemConfigurationRepository(IDbContextFactory<AquaDbContext> factory)
{
    public Task<List<MqttBrokerProfile>> GetBrokerProfilesAsync(CancellationToken ct = default) =>
        QueryAsync(db => db.MqttBrokerProfiles.AsNoTracking().OrderBy(x => x.Name), ct);

    public Task<MqttBrokerProfile?> GetBrokerProfileAsync(string id, CancellationToken ct = default) =>
        FirstAsync(db => db.MqttBrokerProfiles.AsNoTracking().Where(x => x.Id == id), ct);

    public Task UpsertBrokerProfileAsync(MqttBrokerProfile item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);

    public Task<List<NodeSocket>> GetSocketsAsync(bool includeArchived = false, CancellationToken ct = default) =>
        QueryAsync(db => db.NodeSockets.AsNoTracking().Where(x => includeArchived || !x.IsArchived).OrderBy(x => x.Name), ct);

    public Task<NodeSocket?> GetSocketAsync(string id, CancellationToken ct = default) =>
        FirstAsync(db => db.NodeSockets.AsNoTracking().Where(x => x.Id == id), ct);

    public Task<NodeSocket?> GetSocketByNodeAsync(string nodeId, string transport, CancellationToken ct = default) =>
        FirstAsync(db => db.NodeSockets.AsNoTracking().Where(x => x.NodeId == nodeId && x.Transport == transport && x.IsEnabled && !x.IsArchived), ct);

    public Task UpsertSocketAsync(NodeSocket item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);

    public Task<List<SensorDefinition>> GetSensorsAsync(string? pondId = null, CancellationToken ct = default) =>
        QueryAsync(db => db.Sensors.AsNoTracking().Where(x => !x.IsArchived && (pondId == null || x.PondId == pondId)).OrderBy(x => x.Name), ct);

    public Task<SensorDefinition?> GetSensorAsync(string sensorId, CancellationToken ct = default) =>
        FirstAsync(db => db.Sensors.AsNoTracking().Where(x => x.Id == sensorId && !x.IsArchived && !x.IsPendingRemoval), ct);

    public Task<SensorDefinition?> GetSensorForConfigurationAsync(string sensorId, CancellationToken ct = default) =>
        FirstAsync(db => db.Sensors.AsNoTracking().Where(x => x.Id == sensorId && !x.IsArchived), ct);

    public Task UpsertSensorAsync(SensorDefinition sensor, CancellationToken ct = default) => UpsertAsync(sensor, sensor.Id, ct);

    public Task<List<Device>> GetDevicesAsync(string? pondId = null, CancellationToken ct = default) =>
        QueryAsync(db => db.Devices.AsNoTracking().Where(x => !x.IsArchived && (pondId == null || x.PondId == pondId)).OrderBy(x => x.Name), ct);

    public Task UpsertDeviceAsync(Device device, CancellationToken ct = default) => UpsertAsync(device, device.Id, ct);

    /// <summary>Finalizes acknowledged removals atomically so sensor lineage cannot be half-retired.</summary>
    public async Task<FinalizedAssetRemovals> FinalizePendingRemovalsAsync(string socketId,
        long reportedVersion, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var sensors = await db.Sensors.Where(x => x.SocketId == socketId && x.IsPendingRemoval && !x.IsArchived).ToListAsync(ct);
        foreach (var sensor in sensors)
        {
            sensor.IsPendingRemoval = false; sensor.IsArchived = true; sensor.IsEnabled = false;
            sensor.Status = "archived"; sensor.ConfigurationStatus = "archived";
            sensor.ReportedConfigurationVersion = reportedVersion; sensor.UpdatedAtUtc = now;
            await db.SensorInstallations.Where(x => x.SensorId == sensor.Id && x.Status == "active")
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "retired")
                    .SetProperty(x => x.RemovedAtUtc, now).SetProperty(x => x.RemovalReason, sensor.RemovalReason), ct);
        }
        var devices = await db.Devices.Where(x => x.SocketId == socketId && x.IsPendingRemoval && !x.IsArchived).ToListAsync(ct);
        foreach (var device in devices)
        {
            device.IsPendingRemoval = false; device.IsArchived = true; device.IsEnabled = false;
            device.ConfigurationStatus = "archived"; device.ConnectionStatus = "archived";
            device.ReportedConfigurationVersion = reportedVersion; device.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(sensors, devices);
    }

    public Task<List<NodeCapabilitySnapshot>> GetCapabilitiesAsync(CancellationToken ct = default) =>
        QueryAsync(db => db.NodeCapabilities.AsNoTracking(), ct);
    public Task UpsertCapabilitiesAsync(NodeCapabilitySnapshot item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);

    public Task<List<ConfigurationDeployment>> GetDeploymentsAsync(string? socketId = null, int limit = 100, CancellationToken ct = default) =>
        QueryAsync(db => db.ConfigurationDeployments.AsNoTracking().Where(x => socketId == null || x.SocketId == socketId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(limit), ct);

    public Task<ConfigurationDeployment?> GetPendingDeploymentAsync(string socketId, CancellationToken ct = default) =>
        FirstAsync(db => db.ConfigurationDeployments.AsNoTracking().Where(x => x.SocketId == socketId &&
            (x.Status == "applying" || x.Status == "pending-reconnect" || x.Status == "out-of-sync"))
            .OrderByDescending(x => x.CreatedAtUtc), ct);

    public Task UpsertDeploymentAsync(ConfigurationDeployment item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);
    public Task InsertConnectionLogAsync(ConnectionLogEntry item, CancellationToken ct = default) => InsertAsync(item, ct);

    public Task<List<ConnectionLogEntry>> GetConnectionLogsAsync(string? socketId = null, int limit = 200, CancellationToken ct = default) =>
        QueryAsync(db => db.ConnectionLogs.AsNoTracking().Where(x => socketId == null || x.SocketId == socketId)
            .OrderByDescending(x => x.TimestampUtc).Take(limit), ct);

    public Task UpsertSensorHealthAsync(SensorHealthSnapshot item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);
    public Task<List<SensorHealthSnapshot>> GetSensorHealthAsync(string? sensorId = null, CancellationToken ct = default) =>
        QueryAsync(db => db.SensorHealthSnapshots.AsNoTracking().Where(x => sensorId == null || x.SensorId == sensorId), ct);
    public Task InsertSensorHealthEventAsync(SensorHealthEvent item, CancellationToken ct = default) => InsertAsync(item, ct);
    public Task UpsertInstallationAsync(SensorInstallation item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);
    public Task<List<SensorInstallation>> GetInstallationsAsync(string sensorId, CancellationToken ct = default) =>
        QueryAsync(db => db.SensorInstallations.AsNoTracking().Where(x => x.SensorId == sensorId).OrderByDescending(x => x.InstalledAtUtc), ct);
    public Task InsertQuarantinedMessageAsync(QuarantinedNodeMessage item, CancellationToken ct = default) => InsertAsync(item, ct);

    public Task<List<DeviceAutomationPolicy>> GetAutomationPoliciesAsync(string? deviceId = null, CancellationToken ct = default) =>
        QueryAsync(db => db.AutomationPolicies.AsNoTracking().Where(x => deviceId == null || x.DeviceId == deviceId).OrderBy(x => x.Name), ct);
    public Task UpsertAutomationPolicyAsync(DeviceAutomationPolicy item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);

    public async Task<AlarmRule?> GetAlarmRuleAsync(string pondId, string metric, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AlarmRules.AsNoTracking().FirstOrDefaultAsync(x => x.PondId == pondId && x.Metric == metric, ct)
            ?? await db.AlarmRules.AsNoTracking().FirstOrDefaultAsync(x => x.PondId == "*" && x.Metric == metric, ct);
    }

    public Task<List<AlarmRule>> GetPondAlarmRulesAsync(string pondId, CancellationToken ct = default) =>
        QueryAsync(db => db.AlarmRules.AsNoTracking().Where(x => x.PondId == pondId).OrderBy(x => x.Metric), ct);
    public Task UpsertAlarmRuleAsync(AlarmRule item, CancellationToken ct = default) => UpsertAsync(item, item.Id, ct);

    private async Task<List<TEntity>> QueryAsync<TEntity>(Func<AquaDbContext, IQueryable<TEntity>> query,
        CancellationToken ct) where TEntity : class
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await query(db).ToListAsync(ct);
    }

    private async Task<TEntity?> FirstAsync<TEntity>(Func<AquaDbContext, IQueryable<TEntity>> query,
        CancellationToken ct) where TEntity : class
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await query(db).FirstOrDefaultAsync(ct);
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
}

public sealed record FinalizedAssetRemovals(List<SensorDefinition> Sensors, List<Device> Devices);
