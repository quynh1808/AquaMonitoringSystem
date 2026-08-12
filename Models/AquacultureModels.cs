using System.ComponentModel.DataAnnotations;

namespace AquacultureMonitoring.Models;

public sealed class Pond
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FarmId { get; set; } = "farm_main";
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public DateOnly StockingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public double StockingDensity { get; set; }
    public double Area { get; set; }
    public DateOnly EstimatedHarvestDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(3));
    public string Status { get; set; } = "active";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TelemetryData
{
    public string Id { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Ph { get; set; }
    public double DissolvedOxygen { get; set; }
    public double Salinity { get; set; }
    public double Ammonia { get; set; }
    public double WaterLevel { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; } = true;
    public string Quality { get; set; } = "good";
    public string SourceType { get; set; } = "simulation";
}

public sealed class SensorDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string SourceType { get; set; } = "simulation";
    public string ExternalAddress { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Status { get; set; } = "online";
    public DateTime? LastSeenAtUtc { get; set; }
    public int? BatteryLevel { get; set; }
    public int? SignalStrength { get; set; }
    public string FirmwareVersion { get; set; } = "sim-1.0";
    public DateTime? CalibrationDateUtc { get; set; }
    public DateTime? CalibrationDueDateUtc { get; set; }
    public double MinValidValue { get; set; }
    public double MaxValidValue { get; set; }
    public string LastQuality { get; set; } = "unknown";
    public string? LastError { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SensorReadingMetadata
{
    public string PondId { get; set; } = string.Empty;
    public string SensorId { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
}

public sealed class SensorReading
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public SensorReadingMetadata Metadata { get; set; } = new();
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Quality { get; set; } = "good";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public int? BatteryLevel { get; set; }
    public int? SignalStrength { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? RawPayload { get; set; }
    public string EventId { get; set; } = string.Empty;
    public long? SequenceNumber { get; set; }
}

public sealed class Device
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "paddle_wheel";
    public string Status { get; set; } = "off";
    public string Mode { get; set; } = "manual";
    public double PowerKw { get; set; }
    /// <summary>
    /// Name of the command transport registered in dependency injection. Use
    /// "simulation" until an explicit PLC/relay mapping has been commissioned.
    /// </summary>
    public string ControlTransport { get; set; } = "simulation";
    /// <summary>Transport-specific address such as an MQTT topic or PLC coil mapping ID.</summary>
    public string ExternalAddress { get; set; } = string.Empty;
    /// <summary>State the local controller must prefer after communication or power failure.</summary>
    public string FailSafeState { get; set; } = "off";
    public string ConnectionStatus { get; set; } = "online";
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DeviceCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string RequestedState { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string Transport { get; set; } = "simulation";
    public string RequestedBy { get; set; } = "system";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Error { get; set; }
}

public sealed class AlertItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    public string PondName { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string SensorId { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Threshold { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Status { get; set; } = "new";
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? ResolvedBy { get; set; }
    public string ProposedSolution { get; set; } = string.Empty;
    public string Source { get; set; } = "threshold-engine";
    public int OccurrenceCount { get; set; } = 1;
    public string? AssignedTo { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}

public sealed class AlertNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AlertId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string Channel { get; set; } = "webhook";
    public string Recipient { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public string? Error { get; set; }
}

public sealed class AlarmRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = "*";
    public string Metric { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public double? WarningLow { get; set; }
    public double? WarningHigh { get; set; }
    public double? CriticalLow { get; set; }
    public double? CriticalHigh { get; set; }
    public double Hysteresis { get; set; }
    public int DelaySeconds { get; set; }
    public int CooldownSeconds { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FeedLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    public string PondName { get; set; } = string.Empty;
    public string CycleId { get; set; } = string.Empty;
    public string FeedLotId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string FeedType { get; set; } = string.Empty;
    public double AmountKg { get; set; }
    public string Technician { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class AutomationLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PondId { get; set; } = string.Empty;
    public string PondName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TriggerMetric { get; set; } = string.Empty;
    public string? DeviceCommandId { get; set; }
}

public sealed class CalibrationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SensorId { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime NextDueAtUtc { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public double? ValueBefore { get; set; }
    public double? ValueAfter { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class MaintenanceTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string AssetType { get; set; } = "sensor";
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public DateTime DueAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? AssignedTo { get; set; }
}

public sealed class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "system";
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string Result { get; set; } = "success";
}

public sealed class DataSourceHealth
{
    public string Id { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? LastConnectedAtUtc { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public string? LastError { get; set; }
    public long MessagesReceived { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class SystemRoles
{
    public const string Administrator = "Administrator";
    public const string FarmManager = "FarmManager";
    public const string Technician = "Technician";
    public const string Viewer = "Viewer";
    public static readonly string[] All = [Administrator, FarmManager, Technician, Viewer];
}

public sealed class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public int PasswordIterations { get; set; } = 210000;
    public List<string> Roles { get; set; } = [SystemRoles.Viewer];
    public List<string> AllowedPondIds { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }
    /// <summary>Incrementing this invalidates cookies issued before an access change.</summary>
    public int SessionVersion { get; set; } = 1;
}

public sealed class ProductionCycle
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FarmId { get; set; } = "farm_main";
    public string PondId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string SeedBatch { get; set; } = string.Empty;
    public DateOnly PreparationStartedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly StockingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public long InitialQuantity { get; set; }
    public double InitialAverageWeightGrams { get; set; }
    public double StockingDensityPerM2 { get; set; }
    public DateOnly ExpectedHarvestDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(4));
    public DateOnly? ActualHarvestDate { get; set; }
    public string Status { get; set; } = "preparing";
    public double EstimatedSurvivalRatePercent { get; set; } = 100;
    public double LatestAverageWeightGrams { get; set; }
    public double EstimatedBiomassKg { get; set; }
    public DateTime? LatestSampleAtUtc { get; set; }
    public int GrowthSampleCount { get; set; }
    public double EstimatedDailyBiomassGrowthKg { get; set; }
    public double TotalFeedKg { get; set; }
    public double MortalityQuantity { get; set; }
    public double HarvestQuantityKg { get; set; }
    public long HarvestedAnimalQuantity { get; set; }
    public double FeedConversionRatio { get; set; }
    /// <summary>Backed by a partial unique index: at most one open cycle per pond.</summary>
    public bool IsOpen { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GrowthSample
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CycleId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public DateTime SampledAtUtc { get; set; } = DateTime.UtcNow;
    public int SampleSize { get; set; }
    public double AverageWeightGrams { get; set; }
    public double EstimatedSurvivalRatePercent { get; set; }
    public double EstimatedBiomassKg { get; set; }
    public string SampledBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Source { get; set; } = "manual";
}

public sealed class MortalityRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CycleId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public long Quantity { get; set; }
    public string SuspectedCause { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class HarvestRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CycleId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public DateTime HarvestedAtUtc { get; set; } = DateTime.UtcNow;
    public double QuantityKg { get; set; }
    public long AnimalQuantity { get; set; }
    public double AverageWeightGrams { get; set; }
    public string HarvestType { get; set; } = "partial";
    public string RecordedBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class ProductionCycleSnapshot
{
    public required ProductionCycle Cycle { get; init; }
    public required Pond Pond { get; init; }
    public List<GrowthSample> Samples { get; init; } = [];
    public List<MortalityRecord> MortalityRecords { get; init; } = [];
    public List<HarvestRecord> HarvestRecords { get; init; } = [];
}

public sealed class CreateProductionCycleRequest
{
    [Required] public string PondId { get; set; } = string.Empty;
    [Required] public string Code { get; set; } = string.Empty;
    [Required] public string Species { get; set; } = string.Empty;
    public string SeedBatch { get; set; } = string.Empty;
    public DateOnly PreparationStartedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly StockingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Range(1, long.MaxValue)] public long InitialQuantity { get; set; } = 100000;
    [Range(0.001, 10000)] public double InitialAverageWeightGrams { get; set; } = 0.02;
    [Range(0.1, 5000)] public double StockingDensityPerM2 { get; set; } = 100;
    public DateOnly ExpectedHarvestDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddMonths(4));
    public string Status { get; set; } = "preparing";
    public string Notes { get; set; } = string.Empty;
}

public sealed class AddPondRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên ao.")]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Species { get; set; } = "Tôm thẻ chân trắng";

    [Range(1, 100000, ErrorMessage = "Diện tích phải lớn hơn 0.")]
    public double Area { get; set; } = 3000;

    [Range(1, 1000, ErrorMessage = "Mật độ phải lớn hơn 0.")]
    public double StockingDensity { get; set; } = 120;

    public DateOnly StockingDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public sealed class AddDeviceRequest
{
    public string PondId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên thiết bị.")]
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "paddle_wheel";

    [Range(0.1, 100, ErrorMessage = "Công suất phải từ 0,1 đến 100 kW.")]
    public double PowerKw { get; set; } = 2.2;

    /// <summary>Name registered by an IDeviceCommandTransport implementation.</summary>
    [Required]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Transport chỉ gồm chữ thường, số và dấu gạch ngang.")]
    public string ControlTransport { get; set; } = "simulation";

    /// <summary>Stable mapping id/topic/coil alias understood by the selected transport.</summary>
    public string ExternalAddress { get; set; } = string.Empty;

    [RegularExpression("^(on|off)$")]
    public string FailSafeState { get; set; } = "off";
}

public sealed class AddFeedRequest
{
    public string PondId { get; set; } = string.Empty;

    public string CycleId { get; set; } = string.Empty;

    public string FeedLotId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập loại thức ăn.")]
    public string FeedType { get; set; } = "Thức ăn tăng trọng cỡ 1,5 mm";

    [Range(0.1, 10000, ErrorMessage = "Khối lượng phải lớn hơn 0.")]
    public double AmountKg { get; set; } = 20;

    public string? Notes { get; set; }
}

public sealed class CalibrationRequest
{
    public string SensorId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập người hiệu chuẩn.")]
    public string PerformedBy { get; set; } = string.Empty;

    public double? ValueBefore { get; set; }
    public double? ValueAfter { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class SimulationRequest
{
    public double Temperature { get; set; }
    public double Ph { get; set; }
    public double DissolvedOxygen { get; set; }
    public double Salinity { get; set; }
    public double Ammonia { get; set; }
    public double WaterLevel { get; set; }
}

public sealed class TrendPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public sealed class FarmSnapshot
{
    public List<Pond> Ponds { get; set; } = [];
    public Dictionary<string, TelemetryData> Telemetry { get; set; } = [];
    public List<SensorDefinition> Sensors { get; set; } = [];
    public List<SensorReading> RecentReadings { get; set; } = [];
    public List<Device> Devices { get; set; } = [];
    public List<DeviceCommand> DeviceCommands { get; set; } = [];
    public List<AlertItem> Alerts { get; set; } = [];
    public List<FeedLog> FeedLogs { get; set; } = [];
    public List<AutomationLog> AutomationLogs { get; set; } = [];
    public List<CalibrationRecord> CalibrationRecords { get; set; } = [];
    public List<MaintenanceTask> MaintenanceTasks { get; set; } = [];
    public List<DataSourceHealth> DataSources { get; set; } = [];
    public List<ProductionCycle> ProductionCycles { get; set; } = [];
}
