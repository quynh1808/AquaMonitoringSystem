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
    public string DefaultTransport { get; set; } = "simulation";

    /// <summary>
    /// Safety switch for all actuator commands. Keep this false until relay/PLC mappings,
    /// local interlocks and emergency-stop behavior have been commissioned.
    /// </summary>
    public bool HardwareControlEnabled { get; set; }

    /// <summary>Allows the simulation transport to acknowledge commands during development.</summary>
    public bool SimulationEnabled { get; set; } = true;

    /// <summary>Maximum time a real transport may wait for an acknowledgement.</summary>
    public int CommandTimeoutSeconds { get; set; } = 10;
}

