namespace AquacultureMonitoring.Sensors;

public sealed class SensorEnvelope
{
    public string SensorId { get; set; } = string.Empty;
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
}

public interface ISensorDataSource
{
    string SourceName { get; }
    bool IsEnabled { get; }
    Task RunAsync(CancellationToken cancellationToken);
}
