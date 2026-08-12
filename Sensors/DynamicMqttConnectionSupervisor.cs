using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Text.Json;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Services;
using AquacultureMonitoring.Runtime;
using MQTTnet;

namespace AquacultureMonitoring.Sensors;

/// <summary>
/// Reconciles enabled MongoDB broker profiles with live MQTT clients. One client is shared per
/// broker, while every incoming message is authorized against its one-node logical socket.
/// </summary>
public sealed class DynamicMqttConnectionSupervisor : BackgroundService
{
    private readonly SystemConfigurationRepository _repository;
    private readonly SecretProtectionService _secrets;
    private readonly SensorMessageBus _bus;
    private readonly ILogger<DynamicMqttConnectionSupervisor> _logger;
    private readonly FarmRuntimeState _runtime;
    private readonly ConcurrentDictionary<string, BrokerSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DynamicMqttConnectionSupervisor(SystemConfigurationRepository repository,
        SecretProtectionService secrets, SensorMessageBus bus, FarmRuntimeState runtime,
        ILogger<DynamicMqttConnectionSupervisor> logger)
    {
        _repository = repository; _secrets = secrets; _bus = bus; _runtime = runtime; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReconcileAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Không reconcile được MQTT broker sessions."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        foreach (var session in _sessions.Values) session.Cancellation.Cancel();
        await Task.WhenAll(_sessions.Values.Select(x => x.Task).ToArray());
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var profiles = (await _repository.GetBrokerProfilesAsync(ct)).Where(x => x.IsEnabled).ToList();
        var desiredIds = profiles.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in _sessions.ToArray())
        {
            var current = profiles.FirstOrDefault(x => x.Id == existing.Key);
            if (current is null || existing.Value.Version != current.Version || existing.Value.Task.IsCompleted)
            {
                existing.Value.Cancellation.Cancel();
                _sessions.TryRemove(existing.Key, out _);
            }
        }
        foreach (var profile in profiles)
        {
            if (_sessions.ContainsKey(profile.Id)) continue;
            var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var task = RunBrokerAsync(profile, linked.Token);
            _sessions[profile.Id] = new BrokerSession(profile.Version, linked, task);
        }
    }

    private async Task RunBrokerAsync(MqttBrokerProfile profile, CancellationToken ct)
    {
        var delaySeconds = 1;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var factory = new MqttClientFactory();
                using var client = factory.CreateMqttClient();
                client.ApplicationMessageReceivedAsync += args => HandleMessageAsync(profile, client,
                    args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString() ?? string.Empty, ct);

                var builder = new MqttClientOptionsBuilder()
                    .WithClientId($"aquaops-{profile.Id}")
                    .WithTcpServer(profile.Host, profile.Port)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(Math.Max(5, profile.KeepAliveSeconds)))
                    .WithCleanSession(false);
                if (!string.IsNullOrWhiteSpace(profile.Username))
                    builder.WithCredentials(profile.Username, _secrets.Unprotect(profile.ProtectedPassword));
                if (profile.UseTls)
                    builder.WithTlsOptions(tls =>
                    {
                        tls.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13);
                        if (profile.AllowUntrustedCertificates) tls.WithCertificateValidationHandler(_ => true);
                    });

                await client.ConnectAsync(builder.Build(), ct);
                await client.SubscribeAsync(factory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter("aquaculture/+/+/nodes/+/#").Build(), ct);
                delaySeconds = 1;
                await LogBrokerSocketsAsync(profile.Id, "connected", $"Đã kết nối MQTT broker {profile.Host}:{profile.Port}.", ct);
                while (client.IsConnected && !ct.IsCancellationRequested)
                {
                    await PublishDueConfigurationsAsync(profile.Id, client, ct);
                    await Task.Delay(1000, ct);
                }
                throw new IOException("MQTT broker đã đóng kết nối.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT broker {BrokerId} bị ngắt; retry sau {Delay}s.", profile.Id, delaySeconds);
                await LogBrokerSocketsAsync(profile.Id, "disconnected", ex.Message, ct, "warning");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                delaySeconds = Math.Min(60, delaySeconds * 2);
            }
        }
    }

    private async Task HandleMessageAsync(MqttBrokerProfile profile, IMqttClient client, string topic, string payload, CancellationToken ct)
    {
        var levels = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (levels.Length < 6 || levels[0] != "aquaculture" || levels[3] != "nodes")
        {
            await QuarantineAsync(profile.Id, topic, string.Empty, "Topic không đúng contract node AquaOps.", payload, ct);
            return;
        }
        var pondId = levels[2]; var nodeId = levels[4];
        var socket = await _repository.GetSocketByNodeAsync(nodeId, "mqtt", ct);
        if (socket is null || socket.PondId != pondId || socket.Mqtt?.BrokerProfileId != profile.Id ||
            !topic.StartsWith(socket.Mqtt.TopicPrefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            await QuarantineAsync(profile.Id, topic, nodeId, "Node/socket/pond chưa được admin đăng ký hoặc mapping không khớp.", payload, ct);
            return;
        }

        socket.LastMessageAtUtc = DateTime.UtcNow; socket.LastConnectedAtUtc ??= DateTime.UtcNow;
        socket.RuntimeStatus = "connected"; socket.HealthStatus = "healthy"; socket.ConsecutiveFailures = 0; socket.LastError = null;
        await _repository.UpsertSocketAsync(socket, ct);
        var branch = levels[5];
        try
        {
            if (branch == "status")
            {
                var status = JsonSerializer.Deserialize<NodeStatusPayload>(payload, _json) ?? new();
                socket.RuntimeStatus = status.Online ? "connected" : "disconnected";
                socket.HealthStatus = status.Online ? status.Health : "offline";
                socket.LastConnectedAtUtc = status.Online ? DateTime.UtcNow : socket.LastConnectedAtUtc;
                await _repository.UpsertSocketAsync(socket, ct);
                if (status.Online) await PublishPendingConfigurationAsync(socket, client, ct);
            }
            else if (branch == "capabilities")
            {
                var capabilities = JsonSerializer.Deserialize<NodeCapabilitySnapshot>(payload, _json)
                    ?? throw new JsonException("Capability payload rỗng.");
                capabilities.Id = socket.Id; capabilities.SocketId = socket.Id; capabilities.NodeId = socket.NodeId;
                capabilities.ReportedAtUtc = DateTime.UtcNow;
                await _repository.UpsertCapabilitiesAsync(capabilities, ct);
                foreach (var sensor in (await _repository.GetSensorsAsync(socket.PondId, ct)).Where(x => x.SocketId == socket.Id))
                {
                    var channel = capabilities.SensorChannels.FirstOrDefault(x => x.ChannelId.Equals(sensor.ChannelId, StringComparison.OrdinalIgnoreCase));
                    if (channel is null) continue;
                    sensor.MinValidValue = channel.PhysicalMin; sensor.MaxValidValue = channel.PhysicalMax;
                    sensor.Unit = channel.Unit; await _repository.UpsertSensorAsync(sensor, ct); _runtime.UpsertSensor(sensor);
                }
            }
            else if (branch == "config" && levels.Length >= 7 && levels[6] == "request")
            {
                var request = JsonSerializer.Deserialize<ConfigurationRequestPayload>(payload, _json) ?? new();
                if (request.ReportedVersion != socket.DesiredVersion && socket.DesiredVersion > 0 &&
                    await _repository.GetPendingDeploymentAsync(socket.Id, ct) is null)
                {
                    var latest = (await _repository.GetDeploymentsAsync(socket.Id, 1, ct)).FirstOrDefault();
                    if (latest is not null)
                    {
                        latest.Status = "out-of-sync"; latest.ReportedVersion = request.ReportedVersion;
                        latest.AttemptCount = 0; latest.SentAtUtc = null;
                        latest.Error = "Node request config với version khác desired."; latest.UpdatedAtUtc = DateTime.UtcNow;
                        await _repository.UpsertDeploymentAsync(latest, ct);
                    }
                }
                await PublishPendingConfigurationAsync(socket, client, ct);
            }
            else if (branch == "config" && levels.Length >= 7 && levels[6] == "reported")
            {
                await HandleReportedConfigurationAsync(socket, payload, ct);
            }
            else if (branch == "sensors" && levels.Length >= 8 && levels[7] == "telemetry")
            {
                var envelope = JsonSerializer.Deserialize<SensorEnvelope>(payload, _json)
                    ?? throw new JsonException("Telemetry payload rỗng.");
                if (!levels[6].Equals(envelope.SensorId, StringComparison.OrdinalIgnoreCase))
                    throw new JsonException("SensorId không khớp topic.");
                var registered = await _repository.GetSensorAsync(envelope.SensorId, ct);
                if (registered is null || registered.SocketId != socket.Id || registered.NodeId != nodeId ||
                    registered.PondId != pondId || !registered.IsEnabled || registered.PublishIntervalSeconds <= 0)
                    throw new InvalidDataException("Telemetry thuộc sensor chưa đăng ký/disable hoặc sai socket-node mapping.");
                if (!string.IsNullOrWhiteSpace(registered.InstallationId) &&
                    !registered.InstallationId.Equals(envelope.InstallationId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Telemetry live không thuộc installation hiện đang hoạt động của sensor.");
                envelope.PondId = pondId; envelope.NodeId = nodeId; envelope.SocketId = socket.Id;
                envelope.SourceId = socket.Id; envelope.SourceType = "mqtt";
                envelope.SourceAddress = $"mqtt://{profile.Host}:{profile.Port}/{topic}"; envelope.RawPayload = payload;
                await _bus.PublishAsync(envelope, ct);
            }
            else if (branch == "sensors" && levels.Length >= 8 && levels[7] == "health")
            {
                await HandleSensorHealthAsync(socket, levels[6], payload, ct);
            }
            else if (branch == "sync" && levels.Length >= 7 && levels[6] == "data")
            {
                await HandleBackfillAsync(socket, client, payload, ct);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException)
        {
            await QuarantineAsync(profile.Id, topic, nodeId, ex.Message, payload, ct);
        }
    }

    private async Task PublishPendingConfigurationAsync(NodeSocket socket, IMqttClient client, CancellationToken ct)
    {
        var deployment = await _repository.GetPendingDeploymentAsync(socket.Id, ct);
        if (deployment is null) return;
        if (deployment.AttemptCount >= 3 && deployment.SentAtUtc < DateTime.UtcNow.AddSeconds(-15))
        {
            deployment.Status = "timeout"; deployment.Error = "Node không ACK sau 3 lần gửi.";
            deployment.UpdatedAtUtc = DateTime.UtcNow; await _repository.UpsertDeploymentAsync(deployment, ct);
            await _repository.InsertConnectionLogAsync(new ConnectionLogEntry
            { SocketId = socket.Id, Level = "warning", EventType = "configuration-timeout", CorrelationId = deployment.CommandId, Message = deployment.Error }, ct);
            return;
        }
        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"{socket.Mqtt!.TopicPrefix}/config/desired")
            .WithPayload(deployment.DesiredJson).WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true).Build();
        await client.PublishAsync(message, ct);
        deployment.Status = "applying"; deployment.AttemptCount++; deployment.SentAtUtc = DateTime.UtcNow;
        deployment.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpsertDeploymentAsync(deployment, ct);
        await _repository.InsertConnectionLogAsync(new ConnectionLogEntry
        {
            SocketId = socket.Id, EventType = "configuration-sent", CorrelationId = deployment.CommandId,
            Message = $"Đã gửi desired config v{deployment.DesiredVersion}; đang chờ ACK từ node."
        }, ct);
    }

    private async Task HandleReportedConfigurationAsync(NodeSocket socket, string payload, CancellationToken ct)
    {
        var reported = JsonSerializer.Deserialize<ReportedConfigurationPayload>(payload, _json)
            ?? throw new JsonException("Reported config rỗng.");
        var deployments = await _repository.GetDeploymentsAsync(socket.Id, 20, ct);
        var deployment = deployments.FirstOrDefault(x => x.CommandId == reported.CommandId)
            ?? throw new InvalidDataException("CommandId ACK không tồn tại.");
        deployment.ReportedVersion = reported.Version; deployment.ReportedJson = payload;
        deployment.AcknowledgedAtUtc = DateTime.UtcNow; deployment.UpdatedAtUtc = DateTime.UtcNow;
        deployment.Status = reported.Accepted && reported.Version == deployment.DesiredVersion ? "applied" : "rejected";
        deployment.Error = reported.Error;
        if (deployment.Status == "applied") socket.ReportedVersion = reported.Version;
        else if (reported.Accepted) deployment.Status = "out-of-sync";
        await _repository.UpsertDeploymentAsync(deployment, ct); await _repository.UpsertSocketAsync(socket, ct);
        foreach (var sensor in (await _repository.GetSensorsAsync(socket.PondId, ct)).Where(x => x.SocketId == socket.Id))
        {
            sensor.ConfigurationStatus = deployment.Status;
            if (deployment.Status == "applied") sensor.ReportedConfigurationVersion = reported.Version;
            await _repository.UpsertSensorAsync(sensor, ct); _runtime.UpsertSensor(sensor);
        }
        await _repository.InsertConnectionLogAsync(new ConnectionLogEntry
        {
            SocketId = socket.Id, EventType = "configuration-reported", CorrelationId = deployment.CommandId,
            Level = deployment.Status == "applied" ? "information" : "warning",
            Message = $"Node reported v{reported.Version}: {deployment.Status}. {reported.Error}".Trim()
        }, ct);
    }

    private async Task PublishDueConfigurationsAsync(string profileId, IMqttClient client, CancellationToken ct)
    {
        var sockets = (await _repository.GetSocketsAsync(false, ct)).Where(x => x.IsEnabled &&
            x.RuntimeStatus == "connected" && x.Mqtt?.BrokerProfileId == profileId);
        foreach (var socket in sockets)
        {
            var pending = await _repository.GetPendingDeploymentAsync(socket.Id, ct);
            if (pending is not null && (!pending.SentAtUtc.HasValue || pending.SentAtUtc < DateTime.UtcNow.AddSeconds(-15)))
                await PublishPendingConfigurationAsync(socket, client, ct);
        }
    }

    private async Task HandleSensorHealthAsync(NodeSocket socket, string sensorId, string payload, CancellationToken ct)
    {
        var sensor = await _repository.GetSensorAsync(sensorId, ct);
        if (sensor is null || sensor.SocketId != socket.Id) throw new InvalidDataException("Health thuộc sensor chưa đăng ký trên socket.");
        var health = JsonSerializer.Deserialize<SensorHealthSnapshot>(payload, _json) ?? throw new JsonException("Sensor health rỗng.");
        if (!string.IsNullOrWhiteSpace(health.InstallationId) &&
            !health.InstallationId.Equals(sensor.InstallationId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Health không thuộc installation hiện đang hoạt động của sensor.");
        health.Id = sensor.Id; health.SensorId = sensor.Id; health.SocketId = socket.Id; health.NodeId = socket.NodeId;
        health.InstallationId = string.IsNullOrWhiteSpace(health.InstallationId) ? sensor.InstallationId : health.InstallationId;
        health.ReportedAtUtc = DateTime.UtcNow;
        await _repository.UpsertSensorHealthAsync(health, ct);
        sensor.Status = health.Status; sensor.LastError = health.ErrorMessage;
        sensor.FirmwareVersion = string.IsNullOrWhiteSpace(health.DriverVersion) ? sensor.FirmwareVersion : health.DriverVersion;
        sensor.LastQuality = health.Quality; sensor.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpsertSensorAsync(sensor, ct); _runtime.UpsertSensor(sensor);
        await _repository.InsertSensorHealthEventAsync(new SensorHealthEvent
        {
            EventId = $"{socket.NodeId}:{sensor.Id}:{health.ReportedAtUtc.Ticks}", SensorId = sensor.Id,
            InstallationId = health.InstallationId, SocketId = socket.Id, NodeId = socket.NodeId,
            Status = health.Status, ErrorCode = health.ErrorCode, Message = health.ErrorMessage,
            TimestampUtc = health.ReportedAtUtc
        }, ct);
    }

    private async Task HandleBackfillAsync(NodeSocket socket, IMqttClient client, string payload, CancellationToken ct)
    {
        var batch = JsonSerializer.Deserialize<BackfillBatchPayload>(payload, _json) ?? throw new JsonException("Backfill batch rỗng.");
        var persistedEventIds = new List<string>();
        foreach (var envelope in batch.Items)
        {
            var sensor = await _repository.GetSensorAsync(envelope.SensorId, ct)
                ?? throw new InvalidDataException($"Backfill chứa sensor chưa đăng ký: {envelope.SensorId}.");
            if (sensor.SocketId != socket.Id || sensor.NodeId != socket.NodeId || sensor.PondId != socket.PondId)
                throw new InvalidDataException($"Backfill sensor {envelope.SensorId} sai socket/node/pond mapping.");
            var installationId = string.IsNullOrWhiteSpace(envelope.InstallationId)
                ? sensor.InstallationId : envelope.InstallationId;
            if (!string.IsNullOrWhiteSpace(installationId) &&
                !(await _repository.GetInstallationsAsync(sensor.Id, ct)).Any(x => x.Id == installationId))
                throw new InvalidDataException($"Backfill sensor {envelope.SensorId} có InstallationId không tồn tại.");
            envelope.PondId = socket.PondId; envelope.NodeId = socket.NodeId; envelope.SocketId = socket.Id;
            envelope.InstallationId = installationId; envelope.IsBackfill = true;
            envelope.SourceId = socket.Id; envelope.SourceType = "mqtt";
            var result = await _bus.PublishAndWaitAsync(envelope, ct);
            if (result.Persisted) persistedEventIds.Add(envelope.EventId);
        }
        // Only durable or idempotently-existing EventIds are echoed. Omitted items stay in flash.
        var ack = JsonSerializer.Serialize(new { batchId = batch.BatchId, eventIds = persistedEventIds, acceptedAtUtc = DateTime.UtcNow }, _json);
        await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"{socket.Mqtt!.TopicPrefix}/sync/ack")
            .WithPayload(ack).WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);
    }

    private async Task LogBrokerSocketsAsync(string profileId, string status, string message, CancellationToken ct, string level = "information")
    {
        var sockets = (await _repository.GetSocketsAsync(false, ct)).Where(x => x.Transport == "mqtt" && x.Mqtt?.BrokerProfileId == profileId);
        foreach (var socket in sockets)
        {
            socket.RuntimeStatus = status; socket.HealthStatus = status == "connected" ? "unknown" : "offline";
            socket.LastError = status == "connected" ? null : message; await _repository.UpsertSocketAsync(socket, ct);
            await _repository.InsertConnectionLogAsync(new ConnectionLogEntry { SocketId = socket.Id, Level = level, EventType = "broker", Message = message }, ct);
        }
    }

    private Task QuarantineAsync(string brokerId, string topic, string nodeId, string reason, string payload, CancellationToken ct) =>
        _repository.InsertQuarantinedMessageAsync(new QuarantinedNodeMessage
        {
            BrokerProfileId = brokerId, Topic = topic, NodeId = nodeId, Reason = reason,
            Payload = payload.Length > 16000 ? payload[..16000] : payload
        }, ct);

    private sealed record BrokerSession(long Version, CancellationTokenSource Cancellation, Task Task);
    private sealed class NodeStatusPayload { public bool Online { get; set; } public string Health { get; set; } = "healthy"; }
    private sealed class ReportedConfigurationPayload { public string CommandId { get; set; } = string.Empty; public long Version { get; set; } public bool Accepted { get; set; } public string? Error { get; set; } }
    private sealed class ConfigurationRequestPayload { public long ReportedVersion { get; set; } public string Reason { get; set; } = string.Empty; }
    private sealed class BackfillBatchPayload { public string BatchId { get; set; } = string.Empty; public List<SensorEnvelope> Items { get; set; } = []; }
}
