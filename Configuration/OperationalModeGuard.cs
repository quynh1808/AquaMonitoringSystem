using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Configuration;

/// <summary>
/// Enforces the stricter production commissioning profile. Simulation and edge-simulation
/// still traverse MQTT/Modbus; the server itself never generates telemetry or device ACKs.
/// </summary>
public sealed class OperationalModeGuard
{
    private readonly OperationalModeOptions _mode;
    private readonly SensorConnectionOptions _connections;
    private readonly DeviceControlOptions _deviceControl;
    private readonly AlertNotificationOptions _notifications;

    public OperationalModeGuard(
        IOptions<OperationalModeOptions> mode,
        IOptions<SensorConnectionOptions> connections,
        IOptions<DeviceControlOptions> deviceControl,
        IOptions<AlertNotificationOptions> notifications)
    {
        _mode = mode.Value;
        _connections = connections.Value;
        _deviceControl = deviceControl.Value;
        _notifications = notifications.Value;
    }

    public void Validate()
    {
        var errors = new List<string>();
        if (!_connections.ActiveSources.Contains("mqtt", StringComparer.OrdinalIgnoreCase) ||
            !_connections.Mqtt.Enabled)
            errors.Add("MQTT telemetry phải được bật trong mọi chế độ P03");
        if (!_deviceControl.DefaultTransport.Equals("fx5u-modbus-tcp", StringComparison.OrdinalIgnoreCase))
            errors.Add("DeviceControl:DefaultTransport phải là fx5u-modbus-tcp");

        if (_mode.IsProduction)
        {
            if (!_deviceControl.HardwareControlEnabled)
                errors.Add("HardwareControlEnabled phải là true sau khi hoàn tất commissioning PLC");
            if (!_notifications.Enabled)
                errors.Add("AlertNotifications:Enabled phải là true");
        }

        if (errors.Count > 0)
            throw new OptionsValidationException(OperationalModeOptions.SectionName,
                typeof(OperationalModeOptions), errors.Select(x => $"Operational safety: {x}."));
    }
}
