using AquacultureMonitoring.Models;

namespace AquacultureMonitoring.Devices;

/// <summary>Request passed to an actuator transport after authorization and validation.</summary>
public sealed record DeviceTransportRequest(
    Device Device,
    string RequestedState,
    string RequestedBy,
    string Reason,
    TimeSpan Timeout);

/// <summary>Normalized acknowledgement returned by a PLC or other physical adapter.</summary>
public sealed record DeviceTransportResult(
    bool Success,
    string Status,
    DateTime SentAtUtc,
    DateTime? AcknowledgedAtUtc,
    string? Error = null);

/// <summary>
/// Hardware boundary for actuator commands. A real adapter must not report success until the
/// downstream controller has acknowledged the command or its state has been read back.
/// </summary>
public interface IDeviceCommandTransport
{
    string Name { get; }
    Task<DeviceTransportResult> SendAsync(DeviceTransportRequest request, CancellationToken cancellationToken);
}
