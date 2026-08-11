namespace AquacultureMonitoring.Devices;

/// <summary>
/// Deterministic command transport used by development and training environments. It implements
/// the same acknowledgement contract as future MQTT/Modbus transports without touching hardware.
/// </summary>
public sealed class SimulatedDeviceCommandTransport : IDeviceCommandTransport
{
    public string Name => "simulation";

    public async Task<DeviceTransportResult> SendAsync(DeviceTransportRequest request, CancellationToken cancellationToken)
    {
        var sentAt = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken);
        return new DeviceTransportResult(true, "completed", sentAt, DateTime.UtcNow);
    }
}

