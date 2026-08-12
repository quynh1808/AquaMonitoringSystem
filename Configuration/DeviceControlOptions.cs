namespace AquacultureMonitoring.Configuration;

/// <summary>
/// Controls how web-originated device commands are dispatched. Sensor ingestion and
/// device control are deliberately configured separately because production telemetry
/// may be enabled before the farm is ready to accept remote actuator commands.
/// </summary>
public sealed class DeviceControlOptions
{
    public const string SectionName = "DeviceControl";

    /// <summary>The transport used when a device does not declare one explicitly.</summary>
    public string DefaultTransport { get; set; } = "fx5u-modbus-tcp";

    /// <summary>
    /// Safety switch for all actuator commands. Keep this false until relay/PLC mappings,
    /// local interlocks and emergency-stop behavior have been commissioned.
    /// </summary>
    public bool HardwareControlEnabled { get; set; }

    /// <summary>Maximum time a real transport may wait for an acknowledgement.</summary>
    public int CommandTimeoutSeconds { get; set; } = 10;

    /// <summary>Connection and device map for the single Mitsubishi FX5U-compatible PLC.</summary>
    public Fx5uModbusTcpOptions Fx5u { get; set; } = new();
}

/// <summary>
/// Defines the Modbus TCP endpoint and the Mitsubishi M-device addresses used by AquaOps.
/// Keeping the map in configuration lets the ESP32 simulator be replaced by an FX5U without
/// changing application code.
/// </summary>
public sealed class Fx5uModbusTcpOptions
{
    public string Host { get; set; } = "192.168.100.81";
    public int Port { get; set; } = 1502;
    public byte UnitId { get; set; } = 1;
    public int PollIntervalMilliseconds { get; set; } = 250;
    public int HealthCheckIntervalSeconds { get; set; } = 5;
    public int OfflineAfterFailures { get; set; } = 3;
    public ushort HeartbeatRegister { get; set; } = 100;
    public List<Fx5uDeviceMappingOptions> Devices { get; set; } = [];
}

/// <summary>Maps one AquaOps device to command, feedback and fault M devices in the PLC.</summary>
public sealed class Fx5uDeviceMappingOptions
{
    public string DeviceId { get; set; } = string.Empty;
    public string CommandDevice { get; set; } = string.Empty;
    public string FeedbackDevice { get; set; } = string.Empty;
    public string FaultDevice { get; set; } = string.Empty;
}
