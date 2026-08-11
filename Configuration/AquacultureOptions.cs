namespace AquacultureMonitoring.Configuration;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "aquaculture_monitoring";
    /// <summary>Optional credential fields avoid unsafe URI interpolation of special characters.</summary>
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AuthenticationDatabase { get; set; } = string.Empty;
    public int ServerSelectionTimeoutSeconds { get; set; } = 10;
}

public sealed class SensorConnectionOptions
{
    public const string SectionName = "SensorConnections";
    public List<string> ActiveSources { get; set; } = ["simulation"];
    public MqttConnectionOptions Mqtt { get; set; } = new();
    public List<ModbusTcpConnectionOptions> ModbusTcp { get; set; } = [];
    public List<ModbusRtuConnectionOptions> ModbusRtu { get; set; } = [];
}

public sealed class MqttConnectionOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "aquaops-server";
    public string TopicFilter { get; set; } = "aquaculture/+/+/sensors/+/telemetry";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseTls { get; set; }
    public bool AllowUntrustedCertificates { get; set; }
    public int ReconnectDelaySeconds { get; set; } = 5;
}

public sealed class ModbusTcpConnectionOptions
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 502;
    public int PollIntervalMilliseconds { get; set; } = 2000;
    public int TimeoutMilliseconds { get; set; } = 3000;
    public List<ModbusRegisterMapping> Mappings { get; set; } = [];
}

public sealed class ModbusRtuConnectionOptions
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string PortName { get; set; } = "/dev/ttyUSB0";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public int PollIntervalMilliseconds { get; set; } = 2000;
    public int TimeoutMilliseconds { get; set; } = 3000;
    public List<ModbusRegisterMapping> Mappings { get; set; } = [];
}

public sealed class ModbusRegisterMapping
{
    public string SensorId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public byte UnitId { get; set; } = 1;
    public ushort RegisterAddress { get; set; }
    public ushort RegisterCount { get; set; } = 2;
    public string DataType { get; set; } = "Float32";
    public string ByteOrder { get; set; } = "ABCD";
    public double Scale { get; set; } = 1;
    public double Offset { get; set; }
}

public sealed class SensorThresholdCatalog
{
    public const string SectionName = "SensorThresholds";
    public Dictionary<string, MetricThresholdOptions> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MetricThresholdOptions
{
    public string DisplayName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 2;
    public double ValidMin { get; set; }
    public double ValidMax { get; set; }
    public double? WarningLow { get; set; }
    public double? WarningHigh { get; set; }
    public double? CriticalLow { get; set; }
    public double? CriticalHigh { get; set; }
    public double Hysteresis { get; set; }
    public int DelaySeconds { get; set; }
    public int CooldownSeconds { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
    public string CriticalMessage { get; set; } = string.Empty;
    public string ProposedSolution { get; set; } = string.Empty;
}

public sealed class SimulationProfileOptions
{
    public const string SectionName = "SimulationProfile";
    public bool Enabled { get; set; } = true;
    public bool AllowSimulationOverrideRealSensors { get; set; } = false;
    public int IntervalMilliseconds { get; set; } = 3000;
    public int RandomSeed { get; set; } = 20260725;
    public int DefaultBatteryLevel { get; set; } = 100;
    public int DefaultSignalStrength { get; set; } = -45;
    public string FirmwareVersion { get; set; } = "sim-1.0";
    public int ManualBatteryLevel { get; set; } = 100;
    public int ManualSignalStrength { get; set; } = -40;
    public string ManualFirmwareVersion { get; set; } = "manual-sim-1.0";
    public Dictionary<string, SimulationMetricOptions> Metrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, double>> PondMetricOffsets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SimulationMetricOptions
{
    public double BaseValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double MaxStep { get; set; }
    public double TargetCorrectionFactor { get; set; } = 0.12;
    public double DailyAmplitude { get; set; }
    public int DecimalPlaces { get; set; } = 2;
    public double AnomalyProbability { get; set; }
    public double AnomalyAmplitude { get; set; }
}

public sealed class SystemBehaviorOptions
{
    public const string SectionName = "SystemBehavior";
    public int SensorOfflineAfterSeconds { get; set; } = 30;
    public int SensorHealthCheckIntervalSeconds { get; set; } = 10;
    public int ReadingRetentionDays { get; set; } = 365;
    public int RuntimeReadingLimit { get; set; } = 20000;
    public int DashboardHistoryPointLimit { get; set; } = 240;
    public int DefaultShrimpGrowDays { get; set; } = 105;
    public int DefaultFishGrowDays { get; set; } = 180;
    public int DefaultCalibrationIntervalDays { get; set; } = 30;
    public int CalibrationReminderDaysBefore { get; set; } = 7;
    public int MaintenanceCheckIntervalMinutes { get; set; } = 15;
    public HealthScoreOptions HealthScore { get; set; } = new();
    public List<AutomationPolicyOptions> AutomationPolicies { get; set; } = [];
    public List<DefaultDeviceTemplateOptions> DefaultDevices { get; set; } = [];
}


public sealed class HealthScoreOptions
{
    public int OfflineScore { get; set; } = 25;
    public int InitialScore { get; set; } = 100;
    public int MetricOutOfRangePenalty { get; set; } = 12;
    public int CriticalAlertPenalty { get; set; } = 8;
    public int StableMinimum { get; set; } = 85;
    public int MonitorMinimum { get; set; } = 65;
    public int DashboardHealthyMinimum { get; set; } = 80;
}

public sealed class DefaultDeviceTemplateOptions
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "off";
    public string Mode { get; set; } = "manual";
    public double PowerKw { get; set; }
}

public sealed class AutomationPolicyOptions
{
    public string Metric { get; set; } = string.Empty;
    public string TriggerDirection { get; set; } = "low";
    public double StartAt { get; set; }
    public double StopAt { get; set; }
    public List<string> DeviceTypes { get; set; } = [];
    public string StartMessage { get; set; } = string.Empty;
    public string StopMessage { get; set; } = string.Empty;
}

public sealed class FarmSeedOptions
{
    public string FarmId { get; set; } = "farm_main";
    public List<PondSeedItem> Ponds { get; set; } = [];
    public List<DeviceSeedItem> Devices { get; set; } = [];
    public List<FeedSeedItem> FeedLogs { get; set; } = [];
}

public sealed class PondSeedItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public DateOnly StockingDate { get; set; }
    public double StockingDensity { get; set; }
    public double Area { get; set; }
    public DateOnly EstimatedHarvestDate { get; set; }
    public string Status { get; set; } = "active";
}

public sealed class DeviceSeedItem
{
    public string Id { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "off";
    public string Mode { get; set; } = "manual";
    public double PowerKw { get; set; }
}

public sealed class FeedSeedItem
{
    public string Id { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string FeedType { get; set; } = string.Empty;
    public double AmountKg { get; set; }
    public string Technician { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}


public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public string BootstrapAdminUsername { get; set; } = "admin";
    public string BootstrapAdminPassword { get; set; } = "AquaOps@2026";
    public string BootstrapAdminDisplayName { get; set; } = "Quản trị hệ thống";
    public int PasswordIterations { get; set; } = 210000;
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int CookieHours { get; set; } = 8;
}

public sealed class ProductionCycleSimulationOptions
{
    public const string SectionName = "ProductionCycleSimulation";
    public bool Enabled { get; set; } = true;
    public int RefreshHours { get; set; } = 6;
    public int SampleEveryDays { get; set; } = 7;
    public int RandomSeed { get; set; } = 20260726;
    public Dictionary<string, CycleSpeciesSimulationOptions> Species { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CycleSpeciesSimulationOptions
{
    public double InitialWeightGrams { get; set; } = 0.02;
    public double DailyGrowthGrams { get; set; } = 0.25;
    public double DailyGrowthVariationPercent { get; set; } = 8;
    public double DailySurvivalLossPercent { get; set; } = 0.02;
    public double ExpectedFcr { get; set; } = 1.35;
    public int DefaultGrowDays { get; set; } = 110;
    public int DefaultSampleSize { get; set; } = 50;
}
