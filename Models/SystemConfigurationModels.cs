using System.ComponentModel.DataAnnotations;

namespace AquacultureMonitoring.Models;

/// <summary>
/// A broker profile contains infrastructure-level MQTT settings. Node sockets reference a
/// profile so changing the broker address does not require editing every ESP32 connection.
/// Secrets are encrypted before this object is stored in PostgreSQL.
/// </summary>
public sealed class MqttBrokerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; } = 1883;
    public string Username { get; set; } = string.Empty;
    public string ProtectedPassword { get; set; } = string.Empty;
    public bool UseTls { get; set; }
    public bool AllowUntrustedCertificates { get; set; }
    [Range(5, 3600)] public int KeepAliveSeconds { get; set; } = 30;
    public bool IsEnabled { get; set; } = true;
    public long Version { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "system";
}

/// <summary>
/// One logical socket represents exactly one physical edge node. MQTT sockets may share a
/// broker client internally; this identity is still used for authorization, health and mapping.
/// </summary>
public sealed class NodeSocket
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = "esp32";
    [Required] public string PondId { get; set; } = string.Empty;
    public string Transport { get; set; } = "mqtt";
    public bool IsEnabled { get; set; } = true;
    public bool IsArchived { get; set; }
    public string RuntimeStatus { get; set; } = "disconnected";
    public string HealthStatus { get; set; } = "unknown";
    public string? LastError { get; set; }
    public DateTime? LastConnectedAtUtc { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double? LastLatencyMilliseconds { get; set; }
    public string Notes { get; set; } = string.Empty;
    public MqttNodeSocketSettings? Mqtt { get; set; }
    public ModbusNodeSocketSettings? ModbusTcp { get; set; }
    public long DesiredVersion { get; set; }
    public long ReportedVersion { get; set; }
    public long DocumentVersion { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "system";
}

public sealed class MqttNodeSocketSettings
{
    [Required] public string BrokerProfileId { get; set; } = string.Empty;
    public string ExpectedClientId { get; set; } = string.Empty;
    public string TopicPrefix { get; set; } = string.Empty;
    [Range(0, 2)] public int Qos { get; set; } = 1;
    [Range(5, 3600)] public int OfflineAfterSeconds { get; set; } = 90;
}

public sealed class ModbusNodeSocketSettings
{
    [Required] public string Host { get; set; } = string.Empty;
    [Range(1, 65535)] public int Port { get; set; } = 502;
    [Range(0, 255)] public byte UnitId { get; set; } = 1;
    [Range(100, 60000)] public int TimeoutMilliseconds { get; set; } = 3000;
    [Range(250, 60000)] public int PollIntervalMilliseconds { get; set; } = 2000;
    // Deliberately blank: PLC addresses belong to the commissioned socket in PostgreSQL,
    // never to a server-side source default. The simulator map is documented separately.
    public string HeartbeatRegister { get; set; } = string.Empty;
    public string ConfigRequestVariable { get; set; } = string.Empty;
    public string ApplyRequestVariable { get; set; } = string.Empty;
    public string ApplyAcknowledgedVariable { get; set; } = string.Empty;
    public string ConfigurationErrorVariable { get; set; } = string.Empty;
    public string DesiredVersionRegister { get; set; } = string.Empty;
    public string AppliedVersionRegister { get; set; } = string.Empty;
    public string ErrorCodeRegister { get; set; } = string.Empty;
}

/// <summary>Read-only hardware description reported by an edge node after boot or reconnect.</summary>
public sealed class NodeCapabilitySnapshot
{
    public string Id { get; set; } = string.Empty;
    public string SocketId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public List<SensorChannelCapability> SensorChannels { get; set; } = [];
    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SensorChannelCapability
{
    public string ChannelId { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double PhysicalMin { get; set; }
    public double PhysicalMax { get; set; }
    public string Driver { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
}

/// <summary>
/// Desired/reported deployment record. Applied is only valid after a node reports the same
/// command id and version; merely publishing an MQTT message is never treated as success.
/// </summary>
public sealed class ConfigurationDeployment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
    public string SocketId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public long DesiredVersion { get; set; }
    public long? ReportedVersion { get; set; }
    public string Status { get; set; } = "draft";
    public string DesiredJson { get; set; } = "{}";
    public string? ReportedJson { get; set; }
    public string? PreviousJson { get; set; }
    public int AttemptCount { get; set; }
    public string? Error { get; set; }
    public string RequestedBy { get; set; } = "system";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ConnectionLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SocketId { get; set; } = string.Empty;
    public string Level { get; set; } = "information";
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(30);
}

public sealed class SensorHealthSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string SensorId { get; set; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;
    public string SocketId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Status { get; set; } = "not-configured";
    public string DriverStatus { get; set; } = "unknown";
    public string BusStatus { get; set; } = "unknown";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double? ReadLatencyMilliseconds { get; set; }
    public double? PhysicalMin { get; set; }
    public double? PhysicalMax { get; set; }
    public string DriverVersion { get; set; } = string.Empty;
    public string Quality { get; set; } = "unknown";
    public DateTime? LastSampleAtUtc { get; set; }
    public DateTime? LastGoodSampleAtUtc { get; set; }
    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SensorHealthEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EventId { get; set; } = string.Empty;
    public string SensorId { get; set; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;
    public string SocketId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(90);
}

/// <summary>Preserves probe lineage while a logical SensorId remains stable across replacements.</summary>
public sealed class SensorInstallation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SensorId { get; set; } = string.Empty;
    public string SocketId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAtUtc { get; set; }
    public string InstalledBy { get; set; } = string.Empty;
    public string? RemovalReason { get; set; }
}

public sealed class QuarantinedNodeMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BrokerProfileId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(14);
}

/// <summary>A bounded rule definition; arbitrary user-provided code is intentionally unsupported.</summary>
public sealed class DeviceAutomationPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string SensorId { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string TriggerDirection { get; set; } = "low";
    public double StartThreshold { get; set; }
    public double StopThreshold { get; set; }
    [Range(0, 86400)] public int StartPersistenceSeconds { get; set; } = 60;
    [Range(0, 86400)] public int StopPersistenceSeconds { get; set; } = 180;
    [Range(0, 86400)] public int MinimumOnSeconds { get; set; } = 600;
    [Range(0, 86400)] public int MinimumOffSeconds { get; set; } = 300;
    [Range(1, 100)] public int MaximumStartsPerHour { get; set; } = 4;
    public string InputFailureAction { get; set; } = "alert-only";
    public string RuntimeState { get; set; } = "idle";
    public DateTime? ConditionSinceUtc { get; set; }
    public DateTime? LastTransitionAtUtc { get; set; }
    public List<DateTime> RecentStartTimesUtc { get; set; } = [];
    public long Version { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "system";
}
