using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Devices;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Validates and dispatches actuator commands, records their audit trail and updates runtime
/// state only after the selected transport reports a successful acknowledgement.
/// </summary>
public sealed class DeviceCommandService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly IReadOnlyDictionary<string, IDeviceCommandTransport> _transports;
    private readonly IOptionsMonitor<DeviceControlOptions> _options;
    private readonly ILogger<DeviceCommandService> _logger;

    public DeviceCommandService(
        FarmRuntimeState runtime,
        FarmRepository repository,
        IEnumerable<IDeviceCommandTransport> transports,
        IOptionsMonitor<DeviceControlOptions> options,
        ILogger<DeviceCommandService> logger)
    {
        _runtime = runtime;
        _repository = repository;
        _transports = transports.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        _options = options;
        _logger = logger;
    }

    public async Task<DeviceCommand> ExecuteAsync(
        string deviceId,
        string requestedState,
        string requestedBy,
        string reason,
        CancellationToken ct = default)
    {
        if (requestedState is not ("on" or "off"))
            throw new ArgumentOutOfRangeException(nameof(requestedState), "Trạng thái thiết bị chỉ nhận 'on' hoặc 'off'.");

        var device = _runtime.Devices.FirstOrDefault(x => x.Id == deviceId)
            ?? throw new InvalidOperationException("Không tìm thấy thiết bị cần điều khiển.");
        var options = _options.CurrentValue;
        var transportName = string.IsNullOrWhiteSpace(device.ControlTransport)
            ? options.DefaultTransport
            : device.ControlTransport;

        var command = new DeviceCommand
        {
            PondId = device.PondId,
            DeviceId = device.Id,
            RequestedState = requestedState,
            PreviousState = device.Status,
            Status = "pending",
            Transport = transportName,
            RequestedBy = requestedBy,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!device.IsEnabled || device.IsArchived || device.IsPendingRemoval)
        {
            command.Status = "blocked";
            command.Error = "Thiết bị đã disable, đang chờ xóa hoặc đã archive.";
            await CompleteAsync(command, device, reason, null, ct);
            return command;
        }
        if (!device.ConfigurationStatus.Equals("applied", StringComparison.OrdinalIgnoreCase))
        {
            command.Status = "blocked";
            command.Error = $"Cấu hình thiết bị đang ở trạng thái {device.ConfigurationStatus}; phải Apply và nhận PLC ACK trước.";
            await CompleteAsync(command, device, reason, null, ct);
            return command;
        }

        var isAutomation = requestedBy.Equals("automation-engine", StringComparison.OrdinalIgnoreCase);
        if (device.IsLocalOverrideActive || device.ReportedMode == "local-manual")
        {
            command.Status = "blocked-by-local-override";
            command.Error = "Thiết bị đang ở Physical/Local Manual; AquaOps chỉ được giám sát.";
            await CompleteAsync(command, device, reason, null, ct);
            return command;
        }
        if (isAutomation && device.Mode != "auto")
        {
            command.Status = "suppressed"; command.Error = "Automation bị chặn vì thiết bị không ở AUTO.";
            await CompleteAsync(command, device, reason, null, ct); return command;
        }
        if (!isAutomation && device.Mode is not ("manual" or "remote-manual"))
        {
            command.Status = "blocked"; command.Error = "START/STOP từ web chỉ khả dụng ở Remote Manual.";
            await CompleteAsync(command, device, reason, null, ct); return command;
        }
        if (!device.IsRemoteControlAllowed && device.LastSeenAtUtc.HasValue)
        {
            command.Status = "blocked"; command.Error = "PLC đang chặn quyền điều khiển từ xa.";
            await CompleteAsync(command, device, reason, null, ct); return command;
        }

        if (!_transports.TryGetValue(transportName, out var transport))
        {
            command.Status = "failed";
            command.Error = $"Chưa đăng ký transport '{transportName}'.";
            await CompleteAsync(command, device, reason, null, ct);
            return command;
        }

        if (!options.HardwareControlEnabled)
        {
            command.Status = "blocked";
            command.Error = "HardwareControlEnabled=false; lệnh phần cứng đã bị chặn bởi safety switch.";
            await CompleteAsync(command, device, reason, null, ct);
            return command;
        }

        // A command intent must be durable before any physical output is changed. If PostgreSQL
        // cannot accept the pending record, fail closed and do not contact the PLC/relay.
        try
        {
            await _repository.UpsertDeviceCommandAsync(command, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể lưu command intent cho {DeviceId}; lệnh bị chặn.", device.Id);
            command.Status = "blocked";
            command.Error = "Không thể lưu command intent; không gửi lệnh tới phần cứng.";
            _runtime.AddDeviceCommand(command);
            return command;
        }

        DeviceTransportResult result;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.CommandTimeoutSeconds)));
            result = await transport.SendAsync(new DeviceTransportRequest(
                device,
                requestedState,
                requestedBy,
                reason,
                TimeSpan.FromSeconds(Math.Max(1, options.CommandTimeoutSeconds))), timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            command.Status = "timeout";
            command.Error = "Transport không ACK trong thời gian cấu hình.";
            await CompleteAsync(command, device, reason, null, ct);
            return command;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transport {Transport} không thực thi được lệnh cho {DeviceId}.", transportName, device.Id);
            command.Status = "failed";
            command.Error = ex.Message;
            await CompleteAsync(command, device, reason, null, ct);
            return command;
        }

        command.Status = result.Success ? "completed" : result.Status;
        command.SentAtUtc = result.SentAtUtc;
        command.AcknowledgedAtUtc = result.AcknowledgedAtUtc;
        command.CompletedAtUtc = result.Success ? result.AcknowledgedAtUtc ?? DateTime.UtcNow : null;
        command.Error = result.Error;

        Device? updated = null;
        if (result.Success)
        {
            updated = _runtime.UpdateDevice(device.Id, x =>
            {
                x.Status = requestedState;
                x.ConnectionStatus = "online";
                x.LastSeenAtUtc = DateTime.UtcNow;
            });
        }

        await CompleteAsync(command, device, reason, updated, ct);
        return command;
    }

    /// <summary>Changes AUTO/Remote Manual only after PLC feedback confirms the requested mode.</summary>
    public async Task<DeviceCommand> SetModeAsync(string deviceId, string requestedMode, string requestedBy,
        string reason, CancellationToken ct = default)
    {
        if (requestedMode is not ("auto" or "remote-manual")) throw new ArgumentOutOfRangeException(nameof(requestedMode));
        var device = _runtime.Devices.FirstOrDefault(x => x.Id == deviceId)
            ?? throw new InvalidOperationException("Không tìm thấy thiết bị cần đổi mode.");
        var transportName = string.IsNullOrWhiteSpace(device.ControlTransport) ? _options.CurrentValue.DefaultTransport : device.ControlTransport;
        var command = new DeviceCommand
        {
            PondId = device.PondId, DeviceId = device.Id, RequestedState = $"mode:{requestedMode}",
            PreviousState = device.Mode,
            Status = "pending", Transport = transportName, RequestedBy = requestedBy, CreatedAtUtc = DateTime.UtcNow
        };
        if (!device.IsEnabled || device.IsArchived || device.IsPendingRemoval ||
            !device.ConfigurationStatus.Equals("applied", StringComparison.OrdinalIgnoreCase))
        { command.Status = "blocked"; command.Error = "Thiết bị chưa Applied hoặc đang ngừng sử dụng."; await CompleteAsync(command, device, reason, null, ct); return command; }
        if (device.IsLocalOverrideActive || device.ReportedMode == "local-manual")
        { command.Status = "blocked-by-local-override"; command.Error = "Physical/Local Manual đang hoạt động."; await CompleteAsync(command, device, reason, null, ct); return command; }
        if (!_options.CurrentValue.HardwareControlEnabled)
        { command.Status = "blocked"; command.Error = "HardwareControlEnabled=false."; await CompleteAsync(command, device, reason, null, ct); return command; }
        if (!_transports.TryGetValue(transportName, out var raw) || raw is not IDeviceModeTransport transport)
        { command.Status = "failed"; command.Error = $"Transport '{transportName}' không hỗ trợ mode ACK."; await CompleteAsync(command, device, reason, null, ct); return command; }
        try { await _repository.UpsertDeviceCommandAsync(command, ct); }
        catch (Exception ex) { _logger.LogError(ex, "Không lưu được mode intent {CommandId}.", command.Id); command.Status = "blocked"; command.Error = "Không lưu được intent; không gửi PLC."; _runtime.AddDeviceCommand(command); return command; }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.CommandTimeoutSeconds)));
            var result = await transport.SetModeAsync(new DeviceTransportRequest(device, requestedMode, requestedBy, reason,
                TimeSpan.FromSeconds(_options.CurrentValue.CommandTimeoutSeconds)), timeout.Token);
            command.Status = result.Success ? "completed" : result.Status; command.SentAtUtc = result.SentAtUtc;
            command.AcknowledgedAtUtc = result.AcknowledgedAtUtc; command.CompletedAtUtc = result.Success ? result.AcknowledgedAtUtc : null; command.Error = result.Error;
            Device? updated = null;
            if (result.Success) updated = _runtime.UpdateDevice(device.Id, x => { x.Mode = requestedMode; x.ReportedMode = requestedMode; x.LastSeenAtUtc = DateTime.UtcNow; });
            await CompleteAsync(command, device, reason, updated, ct); return command;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { command.Status = "timeout"; command.Error = "PLC không ACK mode trong thời gian cấu hình."; await CompleteAsync(command, device, reason, null, ct); return command; }
        catch (Exception ex)
        { command.Status = "failed"; command.Error = ex.Message; await CompleteAsync(command, device, reason, null, ct); return command; }
    }

    private async Task CompleteAsync(DeviceCommand command, Device device, string reason, Device? updated, CancellationToken ct)
    {
        _runtime.AddDeviceCommand(command);
        try
        {
            var writes = new List<Task>
            {
                _repository.UpsertDeviceCommandAsync(command, ct),
                _repository.InsertAuditLogAsync(CreateAudit(command, device, reason), ct)
            };
            if (updated is not null) writes.Add(_repository.UpsertDeviceAsync(updated, ct));
            await Task.WhenAll(writes);
        }
        catch (Exception ex)
        {
            // Do not retry an actuator command after it may already have changed physical state.
            // The existing pending row and transport logs are the reconciliation evidence.
            _logger.LogCritical(ex, "Không thể lưu kết quả command {CommandId}; cần đối soát trạng thái {DeviceId}.", command.Id, device.Id);
            command.Error = string.IsNullOrWhiteSpace(command.Error)
                ? "Đã gửi lệnh nhưng chưa lưu được kết quả; cần đối soát thiết bị."
                : $"{command.Error} Chưa lưu được kết quả; cần đối soát thiết bị.";
        }
    }

    private static AuditLog CreateAudit(DeviceCommand command, Device device, string reason) => new()
    {
        Actor = command.RequestedBy,
        Action = "device-command",
        EntityType = "device",
        EntityId = device.Id,
        PondId = device.PondId,
        PreviousValue = command.PreviousState,
        // Keep the command intent in the audit entry without coupling AuditLog to a transport schema.
        NewValue = string.IsNullOrWhiteSpace(reason) ? command.RequestedState : $"{command.RequestedState} · {reason}",
        Result = command.Status
    };
}
