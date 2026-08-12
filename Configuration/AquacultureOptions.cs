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

/// <summary>Selects the safety profile independently from ASP.NET hosting environment.</summary>
public sealed class OperationalModeOptions
{
    public const string SectionName = "OperationalMode";
    /// <summary>
    /// Declares where field signals originate. All modes still use the same MQTT/Modbus
    /// interfaces; only deployment endpoints and production safety validation differ.
    /// </summary>
    public string Mode { get; set; } = "EdgeSimulation";
    public bool IsProduction => Mode.Equals("Production", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Generic outbound alert webhook configuration.</summary>
public sealed class AlertNotificationOptions
{
    public const string SectionName = "AlertNotifications";
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;
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
    public int MaxFutureClockSkewSeconds { get; set; } = 60;
    public int MaxTelemetryAgeMinutes { get; set; } = 30;
    public HealthScoreOptions HealthScore { get; set; } = new();
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
    public string ControlTransport { get; set; } = "fx5u-modbus-tcp";
    public string ExternalAddress { get; set; } = string.Empty;
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
