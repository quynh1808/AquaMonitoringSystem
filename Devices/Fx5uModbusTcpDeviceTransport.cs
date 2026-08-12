using AquacultureMonitoring.Configuration;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Devices;

/// <summary>
/// Sends actuator commands to an FX5U-compatible Modbus TCP endpoint and verifies both the
/// feedback and fault coils. The adapter never reports success from the write ACK alone.
/// </summary>
public sealed class Fx5uModbusTcpDeviceTransport : IDeviceCommandTransport
{
    public const string TransportName = "fx5u-modbus-tcp";
    private const ushort Fx5MDeviceModbusBase = 0x2000;
    private const int Fx5MaximumMDevice = 7679;

    private readonly ModbusTcpProtocol _protocol;
    private readonly IOptionsMonitor<DeviceControlOptions> _options;

    public Fx5uModbusTcpDeviceTransport(
        ModbusTcpProtocol protocol,
        IOptionsMonitor<DeviceControlOptions> options)
    {
        _protocol = protocol;
        _options = options;
    }

    public string Name => TransportName;

    public async Task<DeviceTransportResult> SendAsync(
        DeviceTransportRequest request, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue.Fx5u;
        var mapping = options.Devices.FirstOrDefault(x =>
            x.DeviceId.Equals(request.Device.Id, StringComparison.OrdinalIgnoreCase));
        if (mapping is null)
            return Failure("failed", $"Thiết bị {request.Device.Id} chưa có FX5U mapping.");

        var desired = request.RequestedState.Equals("on", StringComparison.OrdinalIgnoreCase);
        var commandAddress = ToModbusCoilAddress(mapping.CommandDevice);
        var feedbackAddress = ToModbusCoilAddress(mapping.FeedbackDevice);
        var faultAddress = ToModbusCoilAddress(mapping.FaultDevice);
        var sentAt = DateTime.UtcNow;

        await _protocol.WriteSingleCoilAsync(
            options.Host, options.Port, options.UnitId, commandAddress, desired, cancellationToken);

        // The PLC scan may need several cycles to update the physical feedback contact.
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await _protocol.ReadSingleCoilAsync(
                    options.Host, options.Port, options.UnitId, faultAddress, cancellationToken))
                return new(false, "failed", sentAt, null, $"PLC báo lỗi tại {mapping.FaultDevice}.");

            var feedback = await _protocol.ReadSingleCoilAsync(
                options.Host, options.Port, options.UnitId, feedbackAddress, cancellationToken);
            if (feedback == desired)
                return new(true, "completed", sentAt, DateTime.UtcNow);

            await Task.Delay(Math.Max(50, options.PollIntervalMilliseconds), cancellationToken);
        }

        return Failure("timeout", $"Không nhận được read-back từ {mapping.FeedbackDevice}.");
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

    private static DeviceTransportResult Failure(string status, string error) =>
        new(false, status, DateTime.UtcNow, null, error);
}
