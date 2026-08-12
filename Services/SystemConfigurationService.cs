using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.AspNetCore.Components.Authorization;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Administrator-facing application service for connection, sensor and PLC configuration.
/// It performs cross-document validation and writes an audit row for every material mutation.
/// </summary>
public sealed partial class SystemConfigurationService
{
    private readonly SystemConfigurationRepository _repository;
    private readonly FarmRepository _farmRepository;
    private readonly FarmRuntimeState _runtime;
    private readonly SecretProtectionService _secrets;
    private readonly AuthenticationStateProvider _authentication;

    public SystemConfigurationService(SystemConfigurationRepository repository, FarmRepository farmRepository,
        FarmRuntimeState runtime, SecretProtectionService secrets, AuthenticationStateProvider authentication)
    {
        _repository = repository;
        _farmRepository = farmRepository;
        _runtime = runtime;
        _secrets = secrets;
        _authentication = authentication;
    }

    public Task<List<MqttBrokerProfile>> GetBrokersAsync(CancellationToken ct = default) => _repository.GetBrokerProfilesAsync(ct);
    public Task<List<NodeSocket>> GetSocketsAsync(CancellationToken ct = default) => _repository.GetSocketsAsync(false, ct);
    public Task<List<NodeCapabilitySnapshot>> GetCapabilitiesAsync(CancellationToken ct = default) => _repository.GetCapabilitiesAsync(ct);
    public Task<List<ConnectionLogEntry>> GetLogsAsync(string? socketId = null, CancellationToken ct = default) => _repository.GetConnectionLogsAsync(socketId, 250, ct);
    public Task<List<ConfigurationDeployment>> GetDeploymentsAsync(string? socketId = null, CancellationToken ct = default) => _repository.GetDeploymentsAsync(socketId, 100, ct);
    public Task<List<SensorHealthSnapshot>> GetSensorHealthAsync(string? sensorId = null, CancellationToken ct = default) => _repository.GetSensorHealthAsync(sensorId, ct);
    public Task<List<DeviceAutomationPolicy>> GetAutomationPoliciesAsync(string? deviceId = null, CancellationToken ct = default) => _repository.GetAutomationPoliciesAsync(deviceId, ct);
    public Task<List<AlarmRule>> GetPondAlarmRulesAsync(string pondId, CancellationToken ct = default) => _repository.GetPondAlarmRulesAsync(pondId, ct);

    public async Task SaveBrokerAsync(MqttBrokerProfile item, string? newPassword, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        item.Name = item.Name.Trim(); item.Host = item.Host.Trim(); item.Username = item.Username.Trim();
        if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Host))
            throw new InvalidOperationException("Tên và host của MQTT broker là bắt buộc.");
        if (!string.IsNullOrEmpty(newPassword)) item.ProtectedPassword = _secrets.Protect(newPassword);
        var previous = await _repository.GetBrokerProfileAsync(item.Id, ct);
        if (previous is not null && item.Version != previous.Version)
            throw new InvalidOperationException("Broker đã được admin khác cập nhật. Hãy tải lại trước khi lưu.");
        item.Version = (previous?.Version ?? 0) + 1;
        item.UpdatedAtUtc = DateTime.UtcNow; item.UpdatedBy = actor;
        await _repository.UpsertBrokerProfileAsync(item, ct);
        await AuditAsync(actor, "broker.save", "mqtt-broker", item.Id, string.Empty, previous, item, ct);
    }

    public async Task SaveSocketAsync(NodeSocket item, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        item.Name = item.Name.Trim(); item.NodeId = NormalizeId(item.NodeId); item.PondId = item.PondId.Trim();
        item.Transport = item.Transport.Trim().ToLowerInvariant();
        if (!TopicSegmentRegex().IsMatch(item.NodeId))
            throw new InvalidOperationException("NodeId phải gồm 2–64 ký tự chữ thường, số, '_' hoặc '-' và không được chứa dấu '/'.");
        if (_runtime.GetPond(item.PondId) is null) throw new InvalidOperationException("Ao được chọn không tồn tại.");
        if (item.Transport is not ("mqtt" or "modbus-tcp")) throw new InvalidOperationException("Transport chỉ hỗ trợ MQTT hoặc Modbus TCP.");
        var sockets = await _repository.GetSocketsAsync(true, ct);
        if (sockets.Any(x => x.Id != item.Id && !x.IsArchived &&
            x.NodeId.Equals(item.NodeId, StringComparison.OrdinalIgnoreCase) && x.Transport == item.Transport))
            throw new InvalidOperationException("NodeId đã được sử dụng bởi một socket cùng transport.");
        if (item.Transport == "mqtt")
        {
            if (item.Mqtt is null || string.IsNullOrWhiteSpace(item.Mqtt.BrokerProfileId))
                throw new InvalidOperationException("MQTT socket phải chọn broker profile.");
            if (await _repository.GetBrokerProfileAsync(item.Mqtt.BrokerProfileId, ct) is null)
                throw new InvalidOperationException("MQTT broker profile không tồn tại.");
            item.ModbusTcp = null;
            // The prefix is identity, not a free-form routing hint. Deriving it prevents
            // wildcard/path mistakes from allowing a node to cross a pond boundary.
            item.Mqtt.TopicPrefix = $"aquaculture/farm_main/{item.PondId}/nodes/{item.NodeId}";
        }
        else
        {
            if (item.ModbusTcp is null || string.IsNullOrWhiteSpace(item.ModbusTcp.Host))
                throw new InvalidOperationException("Modbus TCP socket phải có host.");
            ValidatePlcHandshake(item.ModbusTcp);
            item.Mqtt = null;
        }
        var previous = await _repository.GetSocketAsync(item.Id, ct);
        if (previous is not null && item.DocumentVersion != previous.DocumentVersion)
            throw new InvalidOperationException("Socket đã được admin khác cập nhật. Hãy tải lại trước khi lưu.");
        item.DocumentVersion = (previous?.DocumentVersion ?? 0) + 1;
        item.UpdatedAtUtc = DateTime.UtcNow; item.UpdatedBy = actor;
        await _repository.UpsertSocketAsync(item, ct);
        await AuditAsync(actor, "socket.save", "node-socket", item.Id, item.PondId, previous, item, ct);
        await LogAsync(item.Id, "configuration", previous is null ? "Đã tạo socket." : "Đã lưu thay đổi socket.", ct);
    }

    public async Task<ConfigurationDeployment> ApplySocketAsync(string socketId, string reason, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var socket = await _repository.GetSocketAsync(socketId, ct) ?? throw new InvalidOperationException("Socket không tồn tại.");
        if (!socket.IsEnabled || socket.IsArchived) throw new InvalidOperationException("Không thể Apply socket đã disable hoặc archive.");
        var sensors = await _repository.GetSensorsAsync(socket.PondId, ct);
        var devices = (await _repository.GetDevicesAsync(socket.PondId, ct)).Where(x => x.SocketId == socket.Id).ToList();
        var desiredVersion = Math.Max(socket.DesiredVersion, socket.ReportedVersion) + 1;
        var desired = new
        {
            commandId = string.Empty,
            version = desiredVersion,
            generatedAtUtc = DateTime.UtcNow,
            sensors = sensors.Where(x => x.SocketId == socket.Id && x.IsEnabled && !x.IsArchived).Select(x => new
            {
                sensorId = x.Id, installationId = x.InstallationId, channelId = x.ChannelId,
                metric = x.Metric, unit = x.Unit, intervalSeconds = x.PublishIntervalSeconds
            }),
            devices = devices.Select(x => new
            {
                deviceId = x.Id, x.ManualStartRequestVariable, x.ManualStopRequestVariable,
                x.LocalOverrideVariable, x.RemoteAllowedVariable, x.RunFeedbackVariable, x.FaultVariable,
                x.AutoRequestVariable, x.AutoFeedbackVariable
            })
        };
        var deployment = new ConfigurationDeployment
        {
            SocketId = socket.Id, NodeId = socket.NodeId, DesiredVersion = desiredVersion,
            Status = socket.RuntimeStatus is "connected" or "reachable" ? "applying" : "pending-reconnect",
            RequestedBy = actor
        };
        deployment.PreviousJson = (await _repository.GetDeploymentsAsync(socket.Id, 1, ct)).FirstOrDefault()?.DesiredJson;
        // CommandId is part of the signed correlation contract and is injected before persistence.
        deployment.DesiredJson = JsonSerializer.Serialize(new
        {
            commandId = deployment.CommandId, desired.version, desired.generatedAtUtc, desired.sensors, desired.devices
        }, JsonOptions);
        socket.DesiredVersion = desiredVersion;
        socket.UpdatedAtUtc = DateTime.UtcNow; socket.UpdatedBy = actor;
        await _repository.UpsertSocketAsync(socket, ct);
        foreach (var sensor in sensors.Where(x => x.SocketId == socket.Id && !x.IsArchived))
        {
            sensor.DesiredConfigurationVersion = desiredVersion;
            sensor.ConfigurationStatus = deployment.Status;
            sensor.UpdatedAtUtc = DateTime.UtcNow;
            await _repository.UpsertSensorAsync(sensor, ct);
            _runtime.UpsertSensor(sensor);
        }
        await _repository.UpsertDeploymentAsync(deployment, ct);
        await AuditAsync(actor, "socket.apply", "node-socket", socket.Id, socket.PondId, null, new { reason, deployment }, ct);
        await LogAsync(socket.Id, "configuration-apply", $"Cấu hình v{desiredVersion} chuyển sang {deployment.Status}.", ct, deployment.CommandId);
        return deployment;
    }

    public async Task<string> TestSocketAsync(string socketId, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var socket = await _repository.GetSocketAsync(socketId, ct) ?? throw new InvalidOperationException("Socket không tồn tại.");
        string host; int port;
        if (socket.Transport == "mqtt")
        {
            var broker = await _repository.GetBrokerProfileAsync(socket.Mqtt?.BrokerProfileId ?? string.Empty, ct)
                ?? throw new InvalidOperationException("Broker profile không tồn tại.");
            host = broker.Host; port = broker.Port;
        }
        else { host = socket.ModbusTcp?.Host ?? string.Empty; port = socket.ModbusTcp?.Port ?? 0; }
        var started = DateTime.UtcNow;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var client = new TcpClient(); await client.ConnectAsync(host, port, timeout.Token);
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            await LogAsync(socket.Id, "connection-test", $"TCP endpoint {host}:{port} reachable trong {elapsed:0} ms (chưa thay thế node heartbeat/ACK).", ct);
            await AuditAsync(actor, "socket.test", "node-socket", socket.Id, socket.PondId, null, new { host, port, elapsed }, ct);
            return $"Endpoint reachable · {elapsed:0} ms";
        }
        catch (Exception ex)
        {
            await LogAsync(socket.Id, "connection-test-failed", ex.Message, ct);
            return $"Không kết nối được: {ex.Message}";
        }
    }

    public async Task<ConfigurationDeployment> RollbackSocketAsync(string socketId, string reason, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var socket = await _repository.GetSocketAsync(socketId, ct) ?? throw new InvalidOperationException("Socket không tồn tại.");
        var latest = (await _repository.GetDeploymentsAsync(socket.Id, 1, ct)).FirstOrDefault();
        if (latest is null || string.IsNullOrWhiteSpace(latest.PreviousJson)) throw new InvalidOperationException("Socket chưa có phiên bản trước để rollback.");
        var desiredVersion = Math.Max(socket.DesiredVersion, socket.ReportedVersion) + 1;
        var commandId = Guid.NewGuid().ToString("N");
        var json = JsonNode.Parse(latest.PreviousJson)?.AsObject() ?? throw new InvalidDataException("Previous config không hợp lệ.");
        json["commandId"] = commandId; json["version"] = desiredVersion; json["generatedAtUtc"] = DateTime.UtcNow;
        var rollback = new ConfigurationDeployment
        {
            CommandId = commandId, SocketId = socket.Id, NodeId = socket.NodeId, DesiredVersion = desiredVersion,
            DesiredJson = json.ToJsonString(JsonOptions), PreviousJson = latest.DesiredJson,
            Status = socket.RuntimeStatus is "connected" or "reachable" ? "applying" : "pending-reconnect",
            RequestedBy = actor
        };
        socket.DesiredVersion = desiredVersion; socket.UpdatedAtUtc = DateTime.UtcNow; socket.UpdatedBy = actor;
        await _repository.UpsertSocketAsync(socket, ct); await _repository.UpsertDeploymentAsync(rollback, ct);
        foreach (var sensor in (await _repository.GetSensorsAsync(socket.PondId, ct)).Where(x => x.SocketId == socket.Id))
        {
            sensor.DesiredConfigurationVersion = desiredVersion; sensor.ConfigurationStatus = rollback.Status;
            await _repository.UpsertSensorAsync(sensor, ct); _runtime.UpsertSensor(sensor);
        }
        await AuditAsync(actor, "socket.rollback", "node-socket", socket.Id, socket.PondId, latest, new { reason, rollback }, ct);
        await LogAsync(socket.Id, "configuration-rollback", $"Rollback tạo desired v{desiredVersion}; đang chờ node ACK.", ct, commandId);
        return rollback;
    }

    public async Task SaveSensorAsync(SensorDefinition sensor, bool createInstallation, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        sensor.Id = NormalizeId(sensor.Id); sensor.NodeId = NormalizeId(sensor.NodeId); sensor.ChannelId = sensor.ChannelId.Trim();
        if (string.IsNullOrWhiteSpace(sensor.Id) || string.IsNullOrWhiteSpace(sensor.SocketId) || string.IsNullOrWhiteSpace(sensor.ChannelId))
            throw new InvalidOperationException("SensorId, socket và ChannelId là bắt buộc.");
        if (!TopicSegmentRegex().IsMatch(sensor.Id) || !ChannelIdRegex().IsMatch(sensor.ChannelId))
            throw new InvalidOperationException("SensorId/ChannelId chỉ được chứa chữ, số, '_' hoặc '-' và không được chứa dấu '/'.");
        if (sensor.PublishIntervalSeconds is < 0 or > 86400) throw new InvalidOperationException("Chu kỳ gửi phải từ 0 đến 86400 giây.");
        if (sensor.OfflineTimeoutSeconds is < 5 or > 86400) throw new InvalidOperationException("Offline timeout phải từ 5 đến 86400 giây.");
        var socket = await _repository.GetSocketAsync(sensor.SocketId, ct) ?? throw new InvalidOperationException("Socket không tồn tại.");
        if (socket.PondId != sensor.PondId || !socket.NodeId.Equals(sensor.NodeId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sensor, socket, node và ao không cùng mapping.");
        var existing = await _repository.GetSensorAsync(sensor.Id, ct);
        var sensors = await _repository.GetSensorsAsync(sensor.PondId, ct);
        if (sensors.Any(x => x.Id != sensor.Id && x.SocketId == sensor.SocketId &&
            x.ChannelId.Equals(sensor.ChannelId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("ChannelId đã được gán cho một cảm biến khác trên socket này.");
        var capability = (await _repository.GetCapabilitiesAsync(ct)).FirstOrDefault(x => x.SocketId == socket.Id)?
            .SensorChannels.FirstOrDefault(x => x.ChannelId.Equals(sensor.ChannelId, StringComparison.OrdinalIgnoreCase));
        if (socket.Transport == "mqtt" && capability is null)
            throw new InvalidOperationException("Node chưa báo capability cho ChannelId này. Hãy kết nối ESP32 và kiểm tra trang Edge nodes trước.");
        if (capability is not null)
        {
            if (!capability.Metric.Equals(sensor.Metric, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Channel {sensor.ChannelId} chỉ báo capability metric '{capability.Metric}'.");
            sensor.MinValidValue = capability.PhysicalMin; sensor.MaxValidValue = capability.PhysicalMax;
            sensor.Unit = capability.Unit;
        }
        sensor.SourceType = socket.Transport;
        sensor.ExternalAddress = socket.Transport == "mqtt"
            ? $"mqtt://{socket.Mqtt?.TopicPrefix}/sensors/{sensor.Id}/telemetry"
            : $"modbus://{socket.ModbusTcp?.Host}:{socket.ModbusTcp?.Port}/{sensor.ChannelId}";
        sensor.ConfigurationStatus = "draft"; sensor.UpdatedAtUtc = DateTime.UtcNow;
        sensor.Status = sensor.PublishIntervalSeconds == 0 ? "not-configured" : "offline";
        if (createInstallation || string.IsNullOrWhiteSpace(sensor.InstallationId))
        {
            var installation = new SensorInstallation
            {
                SensorId = sensor.Id, SocketId = socket.Id, NodeId = socket.NodeId,
                ChannelId = sensor.ChannelId, InstalledBy = actor
            };
            sensor.InstallationId = installation.Id;
            await _repository.UpsertInstallationAsync(installation, ct);
        }
        await _repository.UpsertSensorAsync(sensor, ct);
        _runtime.UpsertSensor(sensor);
        await AuditAsync(actor, existing is null ? "sensor.create" : "sensor.update", "sensor", sensor.Id, sensor.PondId, existing, sensor, ct);
    }

    public async Task<Device> SaveDeviceAsync(AddDeviceRequest request, string? existingId, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var socket = await _repository.GetSocketAsync(request.SocketId, ct);
        if (socket is null || !socket.IsEnabled || socket.IsArchived || socket.Transport != "modbus-tcp" || socket.PondId != request.PondId)
            throw new InvalidOperationException("Thiết bị phải dùng Modbus TCP socket đang Enable và cùng ao.");
        var mappings = new[] { request.ManualStartRequestVariable, request.ManualStopRequestVariable,
            request.AutoRequestVariable, request.AutoFeedbackVariable, request.LocalOverrideVariable,
            request.RemoteAllowedVariable, request.RunFeedbackVariable, request.FaultVariable }
            .Select(x => x.Trim().ToUpperInvariant()).ToArray();
        if (mappings.Any(x => !MDeviceRegex().IsMatch(x)) || mappings.Distinct(StringComparer.OrdinalIgnoreCase).Count() != mappings.Length)
            throw new InvalidOperationException("Phải nhập đủ M variables hợp lệ và không trùng trong cùng thiết bị.");
        var devices = await _repository.GetDevicesAsync(request.PondId, ct);
        var used = devices.Where(x => x.Id != existingId && x.SocketId == request.SocketId).SelectMany(x => new[]
        { x.ManualStartRequestVariable, x.ManualStopRequestVariable, x.AutoRequestVariable, x.AutoFeedbackVariable,
          x.LocalOverrideVariable, x.RemoteAllowedVariable, x.RunFeedbackVariable, x.FaultVariable })
          .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflict = mappings.FirstOrDefault(used.Contains);
        if (conflict is not null) throw new InvalidOperationException($"PLC variable {conflict} đã được thiết bị khác sử dụng trên socket.");
        var previous = string.IsNullOrWhiteSpace(existingId) ? null : devices.FirstOrDefault(x => x.Id == existingId);
        // Work on a detached copy so the audit log retains the exact pre-edit state.
        // MongoDB returns mutable models and editing that same reference would otherwise
        // make PreviousValue and NewValue identical.
        var device = previous is null
            ? new Device { Id = Guid.NewGuid().ToString("N"), Status = "off", Mode = "remote-manual", ReportedMode = "unknown" }
            : JsonSerializer.Deserialize<Device>(JsonSerializer.Serialize(previous, JsonOptions), JsonOptions)
                ?? throw new InvalidOperationException("Không thể sao chép cấu hình thiết bị hiện tại.");
        device.PondId = request.PondId; device.Name = request.Name.Trim(); device.Type = request.Type;
        device.PowerKw = request.PowerKw; device.SocketId = request.SocketId; device.ControlTransport = "fx5u-modbus-tcp";
        device.FailSafeState = request.FailSafeState; device.ManualStartRequestVariable = mappings[0]; device.ManualStopRequestVariable = mappings[1];
        device.AutoRequestVariable = mappings[2]; device.AutoFeedbackVariable = mappings[3]; device.LocalOverrideVariable = mappings[4];
        device.RemoteAllowedVariable = mappings[5]; device.RunFeedbackVariable = mappings[6]; device.FaultVariable = mappings[7];
        device.ExternalAddress = $"{device.ManualStartRequestVariable}/{device.ManualStopRequestVariable}";
        device.UpdatedAtUtc = DateTime.UtcNow; device.IsEnabled = true; device.IsArchived = false;
        await _repository.UpsertDeviceAsync(device, ct); _runtime.UpsertDevice(device);
        await AuditAsync(actor, previous is null ? "device.create" : "device.update", "device", device.Id, device.PondId, previous, device, ct);
        return device;
    }

    public async Task ArchiveSensorAsync(string sensorId, string reason, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var sensor = await _repository.GetSensorAsync(sensorId, ct) ?? throw new InvalidOperationException("Sensor không tồn tại.");
        sensor.IsArchived = true; sensor.IsEnabled = false; sensor.PublishIntervalSeconds = 0;
        sensor.ConfigurationStatus = "draft"; sensor.Status = "disabled"; sensor.Notes = $"{sensor.Notes}\nArchive: {reason}".Trim();
        sensor.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpsertSensorAsync(sensor, ct);
        _runtime.UpsertSensor(sensor);
        await AuditAsync(actor, "sensor.archive", "sensor", sensor.Id, sensor.PondId, null, new { reason }, ct);
    }

    public async Task SaveAutomationPolicyAsync(DeviceAutomationPolicy policy, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var device = _runtime.Devices.FirstOrDefault(x => x.Id == policy.DeviceId)
            ?? throw new InvalidOperationException("Thiết bị của policy không tồn tại.");
        var sensor = _runtime.GetSensor(policy.SensorId) ?? throw new InvalidOperationException("Sensor của policy không tồn tại.");
        if (device.PondId != sensor.PondId || device.PondId != policy.PondId)
            throw new InvalidOperationException("Policy, thiết bị và cảm biến phải thuộc cùng một ao.");
        if (policy.TriggerDirection == "low" && policy.StartThreshold >= policy.StopThreshold)
            throw new InvalidOperationException("Rule LOW cần ngưỡng START nhỏ hơn STOP để tạo hysteresis.");
        if (policy.TriggerDirection == "high" && policy.StartThreshold <= policy.StopThreshold)
            throw new InvalidOperationException("Rule HIGH cần ngưỡng START lớn hơn STOP để tạo hysteresis.");
        policy.Version++; policy.UpdatedAtUtc = DateTime.UtcNow; policy.UpdatedBy = actor;
        await _repository.UpsertAutomationPolicyAsync(policy, ct);
        await AuditAsync(actor, "automation-policy.save", "automation-policy", policy.Id, policy.PondId, null, policy, ct);
    }

    public async Task SavePondAlarmRuleAsync(AlarmRule rule, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        if (_runtime.GetPond(rule.PondId) is null) throw new InvalidOperationException("Ao của ngưỡng cảnh báo không tồn tại.");
        if (string.IsNullOrWhiteSpace(rule.Metric)) throw new InvalidOperationException("Metric là bắt buộc.");
        if (rule.WarningLow.HasValue && rule.WarningHigh.HasValue && rule.WarningLow >= rule.WarningHigh)
            throw new InvalidOperationException("Ngưỡng dưới phải nhỏ hơn ngưỡng trên.");
        rule.Id = $"{rule.PondId}:{rule.Metric}"; rule.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpsertAlarmRuleAsync(rule, ct);
        await AuditAsync(actor, "pond-alarm-policy.save", "alarm-rule", rule.Id, rule.PondId, null, rule, ct);
    }

    public async Task BeginSensorReplacementAsync(string sensorId, string reason, bool nodePowerOff, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var sensor = await _repository.GetSensorAsync(sensorId, ct) ?? throw new InvalidOperationException("Sensor không tồn tại.");
        sensor.Status = "maintenance"; sensor.ConfigurationStatus = "draft"; sensor.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpsertSensorAsync(sensor, ct); _runtime.UpsertSensor(sensor);
        if (nodePowerOff)
        {
            var socket = await _repository.GetSocketAsync(sensor.SocketId, ct);
            if (socket is not null)
            {
                socket.RuntimeStatus = "maintenance-offline"; socket.HealthStatus = "maintenance";
                socket.LastError = $"Planned maintenance: {reason}"; await _repository.UpsertSocketAsync(socket, ct);
                foreach (var dependent in (await _repository.GetSensorsAsync(sensor.PondId, ct)).Where(x => x.SocketId == socket.Id && x.Id != sensor.Id))
                { dependent.Status = "unavailable-via-node"; await _repository.UpsertSensorAsync(dependent, ct); _runtime.UpsertSensor(dependent); }
            }
        }
        await AuditAsync(actor, "sensor.replacement.begin", "sensor", sensor.Id, sensor.PondId, null, new { reason, nodePowerOff }, ct);
    }

    public async Task CompleteSensorReplacementAsync(string sensorId, string serialNumber, string model,
        string manufacturer, string reason, CancellationToken ct = default)
    {
        var actor = await RequireAdministratorAsync();
        var sensor = await _repository.GetSensorAsync(sensorId, ct) ?? throw new InvalidOperationException("Sensor không tồn tại.");
        foreach (var old in (await _repository.GetInstallationsAsync(sensor.Id, ct)).Where(x => x.Status == "active"))
        { old.Status = "replaced"; old.RemovedAtUtc = DateTime.UtcNow; old.RemovalReason = reason; await _repository.UpsertInstallationAsync(old, ct); }
        var installation = new SensorInstallation
        {
            SensorId = sensor.Id, SocketId = sensor.SocketId, NodeId = sensor.NodeId, ChannelId = sensor.ChannelId,
            SerialNumber = serialNumber.Trim(), Model = model.Trim(), Manufacturer = manufacturer.Trim(),
            Status = "active", InstalledBy = actor
        };
        await _repository.UpsertInstallationAsync(installation, ct);
        sensor.InstallationId = installation.Id; sensor.Status = "initializing"; sensor.ConfigurationStatus = "draft";
        sensor.LastSeenAtUtc = null; sensor.LastQuality = "unknown"; sensor.LastError = null; sensor.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpsertSensorAsync(sensor, ct); _runtime.UpsertSensor(sensor);
        var socket = await _repository.GetSocketAsync(sensor.SocketId, ct);
        if (socket is not null && socket.RuntimeStatus == "maintenance-offline")
        { socket.RuntimeStatus = "disconnected"; socket.HealthStatus = "unknown"; socket.LastError = null; await _repository.UpsertSocketAsync(socket, ct); }
        await AuditAsync(actor, "sensor.replacement.complete", "sensor", sensor.Id, sensor.PondId, null,
            new { installation.Id, installation.SerialNumber, installation.Model, installation.Manufacturer, reason }, ct);
    }

    private async Task<string> RequireAdministratorAsync()
    {
        var principal = (await _authentication.GetAuthenticationStateAsync()).User;
        if (!principal.IsInRole(SystemRoles.Administrator)) throw new UnauthorizedAccessException("Chỉ Administrator được thay đổi thiết lập hệ thống.");
        return principal.Identity?.Name ?? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "administrator";
    }

    private async Task AuditAsync(string actor, string action, string type, string id, string pondId,
        object? previous, object? current, CancellationToken ct) => await _farmRepository.InsertAuditLogAsync(new AuditLog
    {
        Actor = actor, Action = action, EntityType = type, EntityId = id, PondId = pondId,
        PreviousValue = previous is null ? null : JsonSerializer.Serialize(previous, JsonOptions),
        NewValue = current is null ? null : JsonSerializer.Serialize(current, JsonOptions), Result = "success"
    }, ct);

    private Task LogAsync(string socketId, string eventType, string message, CancellationToken ct, string? correlationId = null) =>
        _repository.InsertConnectionLogAsync(new ConnectionLogEntry
        {
            SocketId = socketId, EventType = eventType, Message = message, CorrelationId = correlationId
        }, ct);

    private static string NormalizeId(string value) => value.Trim().ToLowerInvariant().Replace(' ', '-');

    private static void ValidatePlcHandshake(ModbusNodeSocketSettings settings)
    {
        var mValues = new[] { settings.ConfigRequestVariable, settings.ApplyRequestVariable,
            settings.ApplyAcknowledgedVariable, settings.ConfigurationErrorVariable };
        var dValues = new[] { settings.HeartbeatRegister, settings.DesiredVersionRegister,
            settings.AppliedVersionRegister, settings.ErrorCodeRegister };
        if (mValues.Any(x => !MDeviceRegex().IsMatch(x))) throw new InvalidOperationException("Biến handshake PLC phải có dạng M100.");
        if (dValues.Any(x => !DDeviceRegex().IsMatch(x))) throw new InvalidOperationException("Thanh ghi handshake PLC phải có dạng D100.");
        if (mValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != mValues.Length ||
            dValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != dValues.Length)
            throw new InvalidOperationException("Các địa chỉ handshake PLC không được trùng nhau.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{1,63}$")] private static partial Regex TopicSegmentRegex();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")] private static partial Regex ChannelIdRegex();
    [GeneratedRegex("^M[0-9]{1,5}$", RegexOptions.IgnoreCase)] private static partial Regex MDeviceRegex();
    [GeneratedRegex("^D[0-9]{1,5}$", RegexOptions.IgnoreCase)] private static partial Regex DDeviceRegex();
}
