namespace AquacultureMonitoring.Configuration;

/// <summary>
/// Cấu hình dữ liệu bổ sung cho Dashboard khi hệ thống chưa có đủ phần cứng hoặc phân hệ nghiệp vụ thực tế.
/// Các giá trị cảm biến thời gian thực vẫn đến từ pipeline Simulation/MQTT/Modbus hiện có.
/// </summary>
public sealed class DashboardSimulationOptions
{
    public const string SectionName = "DashboardSimulation";

    public bool Enabled { get; set; } = true;
    public DateTime ReferenceDateUtc { get; set; } = new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
    public int RefreshSeconds { get; set; } = 8;
    public double DynamicVariationPercent { get; set; } = 1.5;
    public string DataSourceLabel { get; set; } = "Dữ liệu kết hợp: cảm biến mô phỏng + nghiệp vụ mô phỏng";
    public int HarvestSoonDays { get; set; } = 21;
    public int NewCycleDays { get; set; } = 20;
    public int EarlyGrowthDays { get; set; } = 55;
    public double DefaultBiomassDensityFactor { get; set; } = 0.0045;
    public double DefaultDailyBiomassGrowthPercent { get; set; } = 0.8;
    public double DefaultFeedRatePercentOfBiomass { get; set; } = 2.7;
    public double DefaultCycleFeedMultiplier { get; set; } = 1.15;
    public double DefaultSurvivalRatePercent { get; set; } = 88;
    public double DefaultFcr { get; set; } = 1.35;
    public double DefaultAverageWeightPerDayGram { get; set; } = 0.23;
    public double MinimumAverageWeightGram { get; set; } = 3;
    public double PeakPowerFactor { get; set; } = 1.18;
    public double ExpectedOperatingHoursPerDay { get; set; } = 12;
    public double EnergyLoadFactor { get; set; } = 0.72;
    public double WeatherTemperatureAmplitudeC { get; set; } = 1.4;
    public double WeatherHumidityAmplitudePercent { get; set; } = 5;
    public double WeatherRainfallAmplitudeMm { get; set; } = 0.8;
    public double WeatherWindAmplitudeMs { get; set; } = 0.5;
    public List<DashboardPondSimulationOptions> Ponds { get; set; } = [];
    public DashboardWeatherSimulationOptions Weather { get; set; } = new();
    public List<DashboardInfrastructureSimulationOptions> Infrastructure { get; set; } = [];
    public List<DashboardTaskSimulationOptions> Tasks { get; set; } = [];
    public List<DashboardDailyHistoryOptions> DailyHistory { get; set; } = [];
}

public sealed class DashboardPondSimulationOptions
{
    public string PondId { get; set; } = string.Empty;
    public double BiomassAtReferenceKg { get; set; }
    public double BiomassDailyGrowthKg { get; set; }
    public double SurvivalRatePercent { get; set; }
    public double AverageWeightGram { get; set; }
    public double FeedPlanTodayKg { get; set; }
    public double FeedActualTodayKg { get; set; }
    public double FeedCycleTotalKg { get; set; }
    public double Fcr { get; set; }
    public DateTime LastSampleAtUtc { get; set; }
    public string BiomassConfidence { get; set; } = "medium";
}

public sealed class DashboardWeatherSimulationOptions
{
    public string StationName { get; set; } = "Trạm thời tiết mô phỏng tại trại";
    public double AirTemperatureC { get; set; } = 30.5;
    public double HumidityPercent { get; set; } = 78;
    public double RainfallMmToday { get; set; }
    public double WindSpeedMs { get; set; } = 2.8;
    public string Condition { get; set; } = "Nhiều mây";
    public string RiskLevel { get; set; } = "warning";
    public string AlertTitle { get; set; } = "Khả năng mưa dông chiều tối";
    public string AlertDetail { get; set; } = "Theo dõi pH, độ mặn và DO sau mưa; kiểm tra nguồn điện ngoài trời.";
    public DateTime UpdatedAtUtc { get; set; } = new(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
}

public sealed class DashboardInfrastructureSimulationOptions
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "online";
    public string Detail { get; set; } = string.Empty;
    public int LatencyMs { get; set; }
}

public sealed class DashboardTaskSimulationOptions
{
    public string Id { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int DayOffset { get; set; }
    public string TimeLocal { get; set; } = "08:00";
    public DateTime? DueAtUtc { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
}

public sealed class DashboardDailyHistoryOptions
{
    public int DayOffset { get; set; }
    public double BiomassKg { get; set; }
    public double FeedKg { get; set; }
    public double EnergyKwh { get; set; }
    public int WarningAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public double SensorAvailabilityPercent { get; set; }
}
