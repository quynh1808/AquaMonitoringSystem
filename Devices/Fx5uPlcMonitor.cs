using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Devices;

/// <summary>
/// Polls the PLC heartbeat and output feedback independently from user commands. This prevents
/// a stale seeded state from looking online when the PLC is disconnected or its scan is frozen.
/// </summary>
public sealed class Fx5uPlcMonitor : BackgroundService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly ModbusTcpProtocol _protocol;
    private readonly IOptionsMonitor<DeviceControlOptions> _options;
    private readonly ILogger<Fx5uPlcMonitor> _logger;
    private int _failures;
    private ushort? _lastHeartbeat;
    private int _unchangedHeartbeatCount;

    public Fx5uPlcMonitor(FarmRuntimeState runtime, FarmRepository repository,
        ModbusTcpProtocol protocol, IOptionsMonitor<DeviceControlOptions> options,
        ILogger<Fx5uPlcMonitor> logger)
    {
        _runtime = runtime;
        _repository = repository;
        _protocol = protocol;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!_runtime.IsInitialized && !stoppingToken.IsCancellationRequested)
            await Task.Delay(100, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue.Fx5u;
            try
            {
                using var pollTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                pollTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(2,
                    _options.CurrentValue.CommandTimeoutSeconds)));
                var heartbeat = await _protocol.ReadHoldingRegisterAsync(
                    options.Host, options.Port, options.UnitId, options.HeartbeatRegister, pollTimeout.Token);
                _unchangedHeartbeatCount = _lastHeartbeat == heartbeat ? _unchangedHeartbeatCount + 1 : 0;
                _lastHeartbeat = heartbeat;
                if (_unchangedHeartbeatCount >= Math.Max(2, options.OfflineAfterFailures))
                    throw new IOException($"PLC heartbeat D{options.HeartbeatRegister} không thay đổi.");

                foreach (var mapping in options.Devices)
                    await RefreshDeviceAsync(mapping, options, pollTimeout.Token);
                _failures = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _failures++;
                _logger.LogWarning(ex, "Không đọc được PLC FX5U tại {Host}:{Port} ({FailureCount}).",
                    options.Host, options.Port, _failures);
                if (_failures >= Math.Max(1, options.OfflineAfterFailures))
                    await MarkMappedDevicesOfflineAsync(options, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.HealthCheckIntervalSeconds)), stoppingToken);
        }
    }

    private async Task RefreshDeviceAsync(Fx5uDeviceMappingOptions mapping,
        Fx5uModbusTcpOptions options, CancellationToken ct)
    {
        var fault = await _protocol.ReadSingleCoilAsync(options.Host, options.Port, options.UnitId,
            Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(mapping.FaultDevice), ct);
        var feedback = await _protocol.ReadSingleCoilAsync(options.Host, options.Port, options.UnitId,
            Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(mapping.FeedbackDevice), ct);
        var current = _runtime.Devices.FirstOrDefault(x => x.Id == mapping.DeviceId);
        var targetStatus = fault ? "fault" : feedback ? "on" : "off";
        var shouldPersist = current is not null &&
            (current.Status != targetStatus || current.ConnectionStatus != "online" ||
             current.LastSeenAtUtc < DateTime.UtcNow.AddSeconds(-30));
        var updated = _runtime.UpdateDevice(mapping.DeviceId, device =>
        {
            device.Status = targetStatus;
            device.ConnectionStatus = "online";
            device.LastSeenAtUtc = DateTime.UtcNow;
        });
        if (updated is not null && shouldPersist) await _repository.UpsertDeviceAsync(updated, ct);
    }

    private async Task MarkMappedDevicesOfflineAsync(Fx5uModbusTcpOptions options, CancellationToken ct)
    {
        foreach (var mapping in options.Devices)
        {
            var current = _runtime.Devices.FirstOrDefault(x => x.Id == mapping.DeviceId);
            var shouldPersist = current is not null && current.ConnectionStatus != "offline";
            var updated = _runtime.UpdateDevice(mapping.DeviceId, device =>
            {
                device.Status = "unknown";
                device.ConnectionStatus = "offline";
            });
            if (updated is not null && shouldPersist) await _repository.UpsertDeviceAsync(updated, ct);
        }
    }
}
