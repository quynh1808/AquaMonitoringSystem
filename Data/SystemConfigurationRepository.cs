using AquacultureMonitoring.Models;
using MongoDB.Driver;

namespace AquacultureMonitoring.Data;

/// <summary>
/// Persistence boundary for configuration and edge-node runtime state. Keeping these queries in
/// one service prevents UI components and protocol workers from implementing inconsistent rules.
/// </summary>
public sealed class SystemConfigurationRepository
{
    private readonly MongoContext _db;

    public SystemConfigurationRepository(MongoContext db) => _db = db;

    public Task<List<MqttBrokerProfile>> GetBrokerProfilesAsync(CancellationToken ct = default) =>
        _db.MqttBrokerProfiles.Find(FilterDefinition<MqttBrokerProfile>.Empty).SortBy(x => x.Name).ToListAsync(ct);

    public async Task<MqttBrokerProfile?> GetBrokerProfileAsync(string id, CancellationToken ct = default) =>
        await _db.MqttBrokerProfiles.Find(x => x.Id == id).FirstOrDefaultAsync(ct);

    public Task UpsertBrokerProfileAsync(MqttBrokerProfile item, CancellationToken ct = default) =>
        _db.MqttBrokerProfiles.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task<List<NodeSocket>> GetSocketsAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var filter = includeArchived ? FilterDefinition<NodeSocket>.Empty : Builders<NodeSocket>.Filter.Eq(x => x.IsArchived, false);
        return _db.NodeSockets.Find(filter).SortBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<NodeSocket?> GetSocketAsync(string id, CancellationToken ct = default) =>
        await _db.NodeSockets.Find(x => x.Id == id).FirstOrDefaultAsync(ct);

    public async Task<NodeSocket?> GetSocketByNodeAsync(string nodeId, string transport, CancellationToken ct = default) =>
        await _db.NodeSockets.Find(x => x.NodeId == nodeId && x.Transport == transport && x.IsEnabled && !x.IsArchived)
            .FirstOrDefaultAsync(ct);

    public Task UpsertSocketAsync(NodeSocket item, CancellationToken ct = default) =>
        _db.NodeSockets.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task<List<SensorDefinition>> GetSensorsAsync(string? pondId = null, CancellationToken ct = default)
    {
        var filter = Builders<SensorDefinition>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(pondId)) filter &= Builders<SensorDefinition>.Filter.Eq(x => x.PondId, pondId);
        return _db.Sensors.Find(filter).SortBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<SensorDefinition?> GetSensorAsync(string sensorId, CancellationToken ct = default) =>
        await _db.Sensors.Find(x => x.Id == sensorId && !x.IsArchived).FirstOrDefaultAsync(ct);

    public Task UpsertSensorAsync(SensorDefinition sensor, CancellationToken ct = default) =>
        _db.Sensors.ReplaceOneAsync(x => x.Id == sensor.Id, sensor, new ReplaceOptions { IsUpsert = true }, ct);

    public Task<List<Device>> GetDevicesAsync(string? pondId = null, CancellationToken ct = default)
    {
        var filter = Builders<Device>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(pondId)) filter &= Builders<Device>.Filter.Eq(x => x.PondId, pondId);
        return _db.Devices.Find(filter).SortBy(x => x.Name).ToListAsync(ct);
    }

    public Task UpsertDeviceAsync(Device device, CancellationToken ct = default) =>
        _db.Devices.ReplaceOneAsync(x => x.Id == device.Id, device, new ReplaceOptions { IsUpsert = true }, ct);

    public Task<List<NodeCapabilitySnapshot>> GetCapabilitiesAsync(CancellationToken ct = default) =>
        _db.NodeCapabilities.Find(FilterDefinition<NodeCapabilitySnapshot>.Empty).ToListAsync(ct);

    public Task UpsertCapabilitiesAsync(NodeCapabilitySnapshot item, CancellationToken ct = default) =>
        _db.NodeCapabilities.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task<List<ConfigurationDeployment>> GetDeploymentsAsync(string? socketId = null, int limit = 100, CancellationToken ct = default)
    {
        var filter = string.IsNullOrWhiteSpace(socketId)
            ? FilterDefinition<ConfigurationDeployment>.Empty
            : Builders<ConfigurationDeployment>.Filter.Eq(x => x.SocketId, socketId);
        return _db.ConfigurationDeployments.Find(filter).SortByDescending(x => x.CreatedAtUtc).Limit(limit).ToListAsync(ct);
    }

    public async Task<ConfigurationDeployment?> GetPendingDeploymentAsync(string socketId, CancellationToken ct = default) =>
        await _db.ConfigurationDeployments.Find(x => x.SocketId == socketId &&
            (x.Status == "applying" || x.Status == "pending-reconnect" || x.Status == "out-of-sync"))
            .SortByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public Task UpsertDeploymentAsync(ConfigurationDeployment item, CancellationToken ct = default) =>
        _db.ConfigurationDeployments.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task InsertConnectionLogAsync(ConnectionLogEntry item, CancellationToken ct = default) =>
        _db.ConnectionLogs.InsertOneAsync(item, cancellationToken: ct);

    public Task<List<ConnectionLogEntry>> GetConnectionLogsAsync(string? socketId = null, int limit = 200, CancellationToken ct = default)
    {
        var filter = string.IsNullOrWhiteSpace(socketId)
            ? FilterDefinition<ConnectionLogEntry>.Empty
            : Builders<ConnectionLogEntry>.Filter.Eq(x => x.SocketId, socketId);
        return _db.ConnectionLogs.Find(filter).SortByDescending(x => x.TimestampUtc).Limit(limit).ToListAsync(ct);
    }

    public Task UpsertSensorHealthAsync(SensorHealthSnapshot item, CancellationToken ct = default) =>
        _db.SensorHealthSnapshots.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task<List<SensorHealthSnapshot>> GetSensorHealthAsync(string? sensorId = null, CancellationToken ct = default)
    {
        var filter = string.IsNullOrWhiteSpace(sensorId)
            ? FilterDefinition<SensorHealthSnapshot>.Empty
            : Builders<SensorHealthSnapshot>.Filter.Eq(x => x.SensorId, sensorId);
        return _db.SensorHealthSnapshots.Find(filter).ToListAsync(ct);
    }

    public Task InsertSensorHealthEventAsync(SensorHealthEvent item, CancellationToken ct = default) =>
        _db.SensorHealthEvents.InsertOneAsync(item, cancellationToken: ct);

    public Task UpsertInstallationAsync(SensorInstallation item, CancellationToken ct = default) =>
        _db.SensorInstallations.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public Task<List<SensorInstallation>> GetInstallationsAsync(string sensorId, CancellationToken ct = default) =>
        _db.SensorInstallations.Find(x => x.SensorId == sensorId).SortByDescending(x => x.InstalledAtUtc).ToListAsync(ct);

    public Task InsertQuarantinedMessageAsync(QuarantinedNodeMessage item, CancellationToken ct = default) =>
        _db.QuarantinedNodeMessages.InsertOneAsync(item, cancellationToken: ct);

    public Task<List<DeviceAutomationPolicy>> GetAutomationPoliciesAsync(string? deviceId = null, CancellationToken ct = default)
    {
        var filter = string.IsNullOrWhiteSpace(deviceId)
            ? FilterDefinition<DeviceAutomationPolicy>.Empty
            : Builders<DeviceAutomationPolicy>.Filter.Eq(x => x.DeviceId, deviceId);
        return _db.AutomationPolicies.Find(filter).SortBy(x => x.Name).ToListAsync(ct);
    }

    public Task UpsertAutomationPolicyAsync(DeviceAutomationPolicy item, CancellationToken ct = default) =>
        _db.AutomationPolicies.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);

    public async Task<AlarmRule?> GetAlarmRuleAsync(string pondId, string metric, CancellationToken ct = default) =>
        await _db.AlarmRules.Find(x => x.PondId == pondId && x.Metric == metric).FirstOrDefaultAsync(ct)
        ?? await _db.AlarmRules.Find(x => x.PondId == "*" && x.Metric == metric).FirstOrDefaultAsync(ct);

    public Task<List<AlarmRule>> GetPondAlarmRulesAsync(string pondId, CancellationToken ct = default) =>
        _db.AlarmRules.Find(x => x.PondId == pondId).SortBy(x => x.Metric).ToListAsync(ct);

    public Task UpsertAlarmRuleAsync(AlarmRule item, CancellationToken ct = default) =>
        _db.AlarmRules.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true }, ct);
}
