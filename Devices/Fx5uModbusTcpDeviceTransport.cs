using AquacultureMonitoring.Data;

namespace AquacultureMonitoring.Devices;

/// <summary>
/// Sends actuator commands to an FX5U-compatible Modbus TCP endpoint and verifies both the
/// feedback and fault coils. The adapter never reports success from the write ACK alone.
/// </summary>
public sealed class Fx5uModbusTcpDeviceTransport : IDeviceCommandTransport, IDeviceModeTransport
{
    public const string TransportName = "fx5u-modbus-tcp";
    private const ushort Fx5MDeviceModbusBase = 0x2000;
    private const int Fx5MaximumMDevice = 7679;

    private readonly ModbusTcpProtocol _protocol;
    private readonly SystemConfigurationRepository _repository;

    public Fx5uModbusTcpDeviceTransport(
        ModbusTcpProtocol protocol,
        SystemConfigurationRepository repository)
    {
        _protocol = protocol;
        _repository = repository;
    }

    public string Name => TransportName;

    public async Task<DeviceTransportResult> SendAsync(
        DeviceTransportRequest request, CancellationToken cancellationToken)
    {
        var socket = await _repository.GetSocketAsync(request.Device.SocketId, cancellationToken);
        var options = socket?.ModbusTcp;
        if (socket is null || !socket.IsEnabled || socket.IsArchived || socket.Transport != "modbus-tcp" || options is null)
            return Failure("failed", $"Thiết bị {request.Device.Id} chưa có Modbus socket đang hoạt động.");
        if (string.IsNullOrWhiteSpace(request.Device.ManualStartRequestVariable) ||
            string.IsNullOrWhiteSpace(request.Device.ManualStopRequestVariable) ||
            string.IsNullOrWhiteSpace(request.Device.RunFeedbackVariable) ||
            string.IsNullOrWhiteSpace(request.Device.FaultVariable))
            return Failure("failed", "Thiết bị chưa khai báo đủ biến START/STOP/feedback/fault.");
        var desired = request.RequestedState.Equals("on", StringComparison.OrdinalIgnoreCase);
        var commandVariable = desired ? request.Device.ManualStartRequestVariable : request.Device.ManualStopRequestVariable;
        var commandAddress = ToModbusCoilAddress(commandVariable);
        var feedbackAddress = ToModbusCoilAddress(request.Device.RunFeedbackVariable);
        var faultAddress = ToModbusCoilAddress(request.Device.FaultVariable);
        var sentAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Device.LocalOverrideVariable) && await _protocol.ReadSingleCoilAsync(
                options.Host, options.Port, options.UnitId, ToModbusCoilAddress(request.Device.LocalOverrideVariable), cancellationToken))
            return Failure("blocked-by-local-override", "PLC đang ở Physical/Local Manual; web chỉ được giám sát.");
        if (!string.IsNullOrWhiteSpace(request.Device.RemoteAllowedVariable) && !await _protocol.ReadSingleCoilAsync(
                options.Host, options.Port, options.UnitId, ToModbusCoilAddress(request.Device.RemoteAllowedVariable), cancellationToken))
            return Failure("blocked", "PLC không cho phép điều khiển từ xa.");
        if (await _protocol.ReadSingleCoilAsync(options.Host, options.Port, options.UnitId, faultAddress, cancellationToken))
            return Failure("failed", $"PLC báo lỗi tại {request.Device.FaultVariable}.");

        // START/STOP are momentary request pulses. Holding a command coil can retrigger a PLC
        // sequence after reconnect or scan restart, so it is always reset in finally.
        await _protocol.WriteSingleCoilAsync(options.Host, options.Port, options.UnitId, commandAddress, true, cancellationToken);
        try { await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken); }
        finally
        {
            using var resetTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _protocol.WriteSingleCoilAsync(options.Host, options.Port, options.UnitId, commandAddress, false, resetTimeout.Token);
        }

        // The PLC scan may need several cycles to update the physical feedback contact.
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await _protocol.ReadSingleCoilAsync(
                    options.Host, options.Port, options.UnitId, faultAddress, cancellationToken))
                return new(false, "failed", sentAt, null, $"PLC báo lỗi tại {request.Device.FaultVariable}.");

            var feedback = await _protocol.ReadSingleCoilAsync(
                options.Host, options.Port, options.UnitId, feedbackAddress, cancellationToken);
            if (feedback == desired)
                return new(true, "completed", sentAt, DateTime.UtcNow);

            await Task.Delay(Math.Max(50, options.PollIntervalMilliseconds), cancellationToken);
        }

        return Failure("timeout", $"Không nhận được read-back từ {request.Device.RunFeedbackVariable}.");
    }

    public async Task<DeviceTransportResult> SetModeAsync(DeviceTransportRequest request, CancellationToken cancellationToken)
    {
        var socket = await _repository.GetSocketAsync(request.Device.SocketId, cancellationToken);
        var options = socket?.ModbusTcp;
        if (socket is null || !socket.IsEnabled || options is null) return Failure("failed", "Modbus socket không hoạt động.");
        if (string.IsNullOrWhiteSpace(request.Device.AutoRequestVariable) || string.IsNullOrWhiteSpace(request.Device.AutoFeedbackVariable))
            return Failure("failed", "Thiết bị chưa khai báo Auto request/feedback variables.");
        if (!string.IsNullOrWhiteSpace(request.Device.LocalOverrideVariable) && await _protocol.ReadSingleCoilAsync(options.Host,
                options.Port, options.UnitId, ToModbusCoilAddress(request.Device.LocalOverrideVariable), cancellationToken))
            return Failure("blocked-by-local-override", "Physical/Local Manual đang hoạt động.");
        var auto = request.RequestedState == "auto"; var sentAt = DateTime.UtcNow;
        await _protocol.WriteSingleCoilAsync(options.Host, options.Port, options.UnitId,
            ToModbusCoilAddress(request.Device.AutoRequestVariable), auto, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var feedback = await _protocol.ReadSingleCoilAsync(options.Host, options.Port, options.UnitId,
                ToModbusCoilAddress(request.Device.AutoFeedbackVariable), cancellationToken);
            if (feedback == auto) return new(true, "completed", sentAt, DateTime.UtcNow);
            await Task.Delay(Math.Max(50, options.PollIntervalMilliseconds), cancellationToken);
        }
        return Failure("timeout", "Không nhận được mode feedback từ PLC.");
    }

    /// <summary>
    /// Converts Mitsubishi M-device notation to the FX5 Modbus coil address. The official FX5
    /// map starts M0 at 0x2000; for example M100 becomes 0x2064.
    /// </summary>
    public static ushort ToModbusCoilAddress(string mDevice)
    {
        if (string.IsNullOrWhiteSpace(mDevice) || mDevice.Length < 2 ||
            char.ToUpperInvariant(mDevice[0]) != 'M' ||
            !int.TryParse(mDevice.AsSpan(1), out var number) || number is < 0 or > Fx5MaximumMDevice)
            throw new FormatException($"Địa chỉ '{mDevice}' không phải M0–M{Fx5MaximumMDevice} hợp lệ.");
        return checked((ushort)(Fx5MDeviceModbusBase + number));
    }

    public static ushort ToHoldingRegisterAddress(string dDevice)
    {
        if (string.IsNullOrWhiteSpace(dDevice) || dDevice.Length < 2 ||
            char.ToUpperInvariant(dDevice[0]) != 'D' || !ushort.TryParse(dDevice.AsSpan(1), out var number))
            throw new FormatException($"Địa chỉ '{dDevice}' không phải D0–D65535 hợp lệ.");
        return number;
    }

    private static DeviceTransportResult Failure(string status, string error) =>
        new(false, status, DateTime.UtcNow, null, error);
}
