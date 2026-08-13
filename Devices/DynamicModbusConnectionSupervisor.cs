using System.Diagnostics;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;

namespace AquacultureMonitoring.Devices;

/// <summary>
/// Polls every enabled Modbus node socket from MongoDB, reconciles reported PLC state and resumes
/// configuration deployments. It never replays old actuator commands after a reconnect.
/// </summary>
public sealed class DynamicModbusConnectionSupervisor : BackgroundService
{
    private readonly SystemConfigurationRepository _configuration;
    private readonly FarmRepository _farm;
    private readonly FarmRuntimeState _runtime;
    private readonly ModbusTcpProtocol _protocol;
    private readonly ILogger<DynamicModbusConnectionSupervisor> _logger;

    public DynamicModbusConnectionSupervisor(SystemConfigurationRepository configuration, FarmRepository farm,
        FarmRuntimeState runtime, ModbusTcpProtocol protocol, ILogger<DynamicModbusConnectionSupervisor> logger)
    {
        _configuration = configuration; _farm = farm; _runtime = runtime; _protocol = protocol; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!_runtime.IsInitialized && !stoppingToken.IsCancellationRequested) await Task.Delay(100, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var sockets = (await _configuration.GetSocketsAsync(false, stoppingToken))
                .Where(x => x.IsEnabled && x.Transport == "modbus-tcp" && x.ModbusTcp is not null).ToList();
            await Task.WhenAll(sockets.Select(x => PollSocketSafelyAsync(x, stoppingToken)));
            var delay = sockets.Select(x => x.ModbusTcp!.PollIntervalMilliseconds).DefaultIfEmpty(2000).Min();
            await Task.Delay(Math.Max(250, delay), stoppingToken);
        }
    }

    private async Task PollSocketSafelyAsync(NodeSocket socket, CancellationToken ct)
    {
        var settings = socket.ModbusTcp!;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Math.Max(100, settings.TimeoutMilliseconds));
            await _protocol.ReadHoldingRegisterAsync(settings.Host, settings.Port, settings.UnitId,
                Fx5uModbusTcpDeviceTransport.ToHoldingRegisterAddress(settings.HeartbeatRegister), timeout.Token);
            stopwatch.Stop();
            var recovered = socket.RuntimeStatus != "reachable";
            socket.RuntimeStatus = "reachable"; socket.HealthStatus = "healthy"; socket.LastError = null;
            socket.ConsecutiveFailures = 0; socket.LastLatencyMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            socket.LastConnectedAtUtc ??= DateTime.UtcNow; socket.LastMessageAtUtc = DateTime.UtcNow;
            await _configuration.UpsertSocketAsync(socket, ct);
            if (recovered) await LogAsync(socket.Id, "plc-reachable", "PLC reachable; đang reconcile trạng thái thực, không phát lại command cũ.", ct);
            await RefreshDevicesAsync(socket, timeout.Token);
            await ReconcileDeploymentAsync(socket, timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            socket.ConsecutiveFailures++; socket.RuntimeStatus = socket.ConsecutiveFailures < 3 ? "degraded" : "disconnected";
            socket.HealthStatus = socket.RuntimeStatus; socket.LastError = ex.Message;
            await _configuration.UpsertSocketAsync(socket, ct);
            if (socket.ConsecutiveFailures is 1 or 3)
                await LogAsync(socket.Id, "plc-poll-error", ex.Message, ct, "warning");
            _logger.LogDebug(ex, "Không poll được Modbus socket {SocketId}.", socket.Id);
        }
    }

    private async Task RefreshDevicesAsync(NodeSocket socket, CancellationToken ct)
    {
        var settings = socket.ModbusTcp!;
        foreach (var device in (await _configuration.GetDevicesAsync(socket.PondId, ct)).Where(x => x.SocketId == socket.Id && x.IsEnabled))
        {
            try
            {
                var local = await ReadOptionalCoilAsync(settings, device.LocalOverrideVariable, false, ct);
                var remoteAllowed = await ReadOptionalCoilAsync(settings, device.RemoteAllowedVariable, true, ct);
                var running = await ReadOptionalCoilAsync(settings, device.RunFeedbackVariable, false, ct);
                var fault = await ReadOptionalCoilAsync(settings, device.FaultVariable, false, ct);
                var auto = await ReadOptionalCoilAsync(settings, device.AutoFeedbackVariable, false, ct);
                var updated = _runtime.UpdateDevice(device.Id, x =>
                {
                    x.IsLocalOverrideActive = local; x.IsRemoteControlAllowed = remoteAllowed;
                    x.ReportedMode = local ? "local-manual" : auto ? "auto" : "remote-manual";
                    x.Mode = x.ReportedMode; x.Status = fault ? "fault" : running ? "on" : "off";
                    x.ConnectionStatus = "online"; x.LastSeenAtUtc = DateTime.UtcNow;
                });
                if (updated is not null) await _farm.UpsertDeviceAsync(updated, ct);
            }
            catch (FormatException ex)
            {
                await LogAsync(socket.Id, "device-mapping-invalid", $"{device.Name}: {ex.Message}", ct, "warning");
            }
        }
    }

    private async Task ReconcileDeploymentAsync(NodeSocket socket, CancellationToken ct)
    {
        var settings = socket.ModbusTcp!;
        var plcRequest = await _protocol.ReadSingleCoilAsync(settings.Host, settings.Port, settings.UnitId,
            Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(settings.ConfigRequestVariable), ct);
        var plcVersion = await _protocol.ReadHoldingRegisterAsync(settings.Host, settings.Port, settings.UnitId,
            Fx5uModbusTcpDeviceTransport.ToHoldingRegisterAddress(settings.AppliedVersionRegister), ct);
        var deployment = await _configuration.GetPendingDeploymentAsync(socket.Id, ct);
        if (deployment is null && socket.DesiredVersion > 0 && (plcRequest || plcVersion != (ushort)(socket.DesiredVersion & 0xffff)))
        {
            deployment = (await _configuration.GetDeploymentsAsync(socket.Id, 1, ct)).FirstOrDefault();
            if (deployment is not null)
            {
                deployment.Status = "out-of-sync"; deployment.ReportedVersion = plcVersion;
                deployment.Error = plcRequest ? "PLC yêu cầu gửi lại cấu hình." : "PLC applied version khác desired.";
                deployment.UpdatedAtUtc = DateTime.UtcNow; await _configuration.UpsertDeploymentAsync(deployment, ct);
            }
        }
        if (deployment is null) return;

        // Configuration writes are bounded and spaced out. A missing ACK must not cause
        // the server to pulse M51 on every fast PLC polling cycle.
        var retryDelay = TimeSpan.FromSeconds(5);
        if (deployment.AttemptCount >= 3)
        {
            deployment.Status = "timeout";
            deployment.Error = "PLC không ACK cấu hình sau 3 lần gửi.";
            deployment.UpdatedAtUtc = DateTime.UtcNow;
            await _configuration.UpsertDeploymentAsync(deployment, ct);
            await LogAsync(socket.Id, "configuration-timeout", deployment.Error, ct, "warning");
            await UpdateAssetDeploymentStatusAsync(socket, deployment, ct);
            return;
        }
        if (deployment.SentAtUtc.HasValue && deployment.SentAtUtc.Value > DateTime.UtcNow - retryDelay)
            return;

        var version = checked((ushort)(deployment.DesiredVersion & 0xffff));
        await _protocol.WriteHoldingRegisterAsync(settings.Host, settings.Port, settings.UnitId,
            Fx5uModbusTcpDeviceTransport.ToHoldingRegisterAddress(settings.DesiredVersionRegister), version, ct);
        var applyAddress = Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(settings.ApplyRequestVariable);
        await _protocol.WriteSingleCoilAsync(settings.Host, settings.Port, settings.UnitId, applyAddress, true, ct);
        await Task.Delay(100, ct);
        await _protocol.WriteSingleCoilAsync(settings.Host, settings.Port, settings.UnitId, applyAddress, false, ct);
        deployment.Status = "applying"; deployment.AttemptCount++; deployment.SentAtUtc = DateTime.UtcNow; deployment.UpdatedAtUtc = DateTime.UtcNow;
        var acknowledged = await _protocol.ReadSingleCoilAsync(settings.Host, settings.Port, settings.UnitId,
            Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(settings.ApplyAcknowledgedVariable), ct);
        var reported = await _protocol.ReadHoldingRegisterAsync(settings.Host, settings.Port, settings.UnitId,
            Fx5uModbusTcpDeviceTransport.ToHoldingRegisterAddress(settings.AppliedVersionRegister), ct);
        var configurationError = await _protocol.ReadSingleCoilAsync(settings.Host, settings.Port, settings.UnitId,
            Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(settings.ConfigurationErrorVariable), ct);
        if (configurationError)
        {
            var errorCode = await _protocol.ReadHoldingRegisterAsync(settings.Host, settings.Port, settings.UnitId,
                Fx5uModbusTcpDeviceTransport.ToHoldingRegisterAddress(settings.ErrorCodeRegister), ct);
            deployment.Status = "rejected"; deployment.Error = $"PLC từ chối cấu hình, error code D112={errorCode}.";
        }
        else if (acknowledged && reported == version)
        {
            deployment.Status = "applied"; deployment.ReportedVersion = deployment.DesiredVersion;
            deployment.AcknowledgedAtUtc = DateTime.UtcNow; socket.ReportedVersion = deployment.DesiredVersion;
        }
        else
        {
            deployment.Status = "applying"; deployment.ReportedVersion = reported;
            deployment.Error = "Đã gửi cấu hình; đang chờ PLC ACK đúng version.";
        }
        await _configuration.UpsertDeploymentAsync(deployment, ct); await _configuration.UpsertSocketAsync(socket, ct);
        await UpdateAssetDeploymentStatusAsync(socket, deployment, ct);
    }

    private async Task UpdateAssetDeploymentStatusAsync(NodeSocket socket, ConfigurationDeployment deployment, CancellationToken ct)
    {
        foreach (var sensor in (await _configuration.GetSensorsAsync(socket.PondId, ct)).Where(x => x.SocketId == socket.Id))
        {
            sensor.ConfigurationStatus = deployment.Status;
            if (deployment.Status == "applied") sensor.ReportedConfigurationVersion = deployment.DesiredVersion;
            await _configuration.UpsertSensorAsync(sensor, ct);
            _runtime.UpsertSensor(sensor);
        }
        foreach (var device in (await _configuration.GetDevicesAsync(socket.PondId, ct)).Where(x => x.SocketId == socket.Id))
        {
            device.ConfigurationStatus = deployment.Status;
            if (deployment.Status == "applied") device.ReportedConfigurationVersion = deployment.DesiredVersion;
            await _configuration.UpsertDeviceAsync(device, ct);
            _runtime.UpsertDevice(device);
        }
        if (deployment.Status == "applied")
        {
            var finalized = await _configuration.FinalizePendingRemovalsAsync(socket.Id, deployment.DesiredVersion, ct);
            foreach (var sensor in finalized.Sensors) _runtime.UpsertSensor(sensor);
            foreach (var device in finalized.Devices) _runtime.UpsertDevice(device);
            if (finalized.Sensors.Count + finalized.Devices.Count > 0)
                await LogAsync(socket.Id, "asset-removal-applied",
                    $"Đã archive {finalized.Sensors.Count} sensor và {finalized.Devices.Count} thiết bị sau PLC ACK.", ct);
        }
    }

    private Task<bool> ReadOptionalCoilAsync(ModbusNodeSocketSettings settings, string variable, bool defaultValue, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(variable) ? Task.FromResult(defaultValue) : _protocol.ReadSingleCoilAsync(settings.Host,
            settings.Port, settings.UnitId, Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(variable), ct);

    private Task LogAsync(string socketId, string eventType, string message, CancellationToken ct, string level = "information") =>
        _configuration.InsertConnectionLogAsync(new ConnectionLogEntry
        { SocketId = socketId, EventType = eventType, Message = message, Level = level }, ct);
}
