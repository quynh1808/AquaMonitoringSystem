namespace AquacultureMonitoring.Sensors;

public sealed class SensorEnvelope
{
    public string SensorId { get; set; } = string.Empty;
    public string SocketId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string InstallationId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceAddress { get; set; } = string.Empty;
    public string Quality { get; set; } = "good";
    public int? BatteryLevel { get; set; }
    public int? SignalStrength { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? RawPayload { get; set; }
    public string EventId { get; set; } = string.Empty;
    public long? SequenceNumber { get; set; }
    public bool IsBackfill { get; set; }
    public string TimeQuality { get; set; } = "synchronized";
    [System.Text.Json.Serialization.JsonIgnore]
    internal TaskCompletionSource<SensorIngestionResult>? Completion { get; set; }
}

public sealed record SensorIngestionResult(bool Persisted, string? Error = null);

public interface ISensorDataSource
{
    string SourceName { get; }
    bool IsEnabled { get; }
    Task RunAsync(CancellationToken cancellationToken);
}
