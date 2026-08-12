namespace AquacultureMonitoring.Models;

public sealed class DashboardSnapshot
{
    public DateTime GeneratedAtLocal { get; set; } = DateTime.Now;
    public string DataSourceLabel { get; set; } = string.Empty;
    public int RefreshSeconds { get; set; } = 8;
    public int HealthStableMinimum { get; set; } = 85;
    public int HealthMonitorMinimum { get; set; } = 65;

    public int TotalPonds { get; set; }
    public double TotalAreaM2 { get; set; }
    public int ActiveCycles { get; set; }
    public int HarvestSoonCount { get; set; }
    public int MedianCultureAgeDays { get; set; }
    public int MinimumCultureAgeDays { get; set; }
    public int MaximumCultureAgeDays { get; set; }

    public double EstimatedBiomassKg { get; set; }
    public double BiomassChangeKg { get; set; }
    public bool HasBiomassGrowthData { get; set; }
    public DateTime? LatestBiomassSampleAtLocal { get; set; }
    public string BiomassConfidence { get; set; } = "medium";

    public int RunningDevices { get; set; }
    public int StoppedDevices { get; set; }
    public int FaultDevices { get; set; }
    public int OfflineDevices { get; set; }
    public int AutoDevices { get; set; }
    public int TotalDevices { get; set; }

    public double CurrentPowerKw { get; set; }
    public double PeakPowerTodayKw { get; set; }
    public double EnergyTodayKwh { get; set; }
    public double EnergyMonthKwh { get; set; }

    public int ActiveAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int WarningAlerts { get; set; }
    public int UnacknowledgedAlerts { get; set; }

    public double AverageDo { get; set; }
    public double MinimumDo { get; set; }
    public int PondsBelowDoWarning { get; set; }
    public int ValidDoPonds { get; set; }

    public double FeedTodayKg { get; set; }
    public double FeedPlanTodayKg { get; set; }
    public double FeedCycleTotalKg { get; set; }

    public int OnlineSensors { get; set; }
    public int OfflineSensors { get; set; }
    public int InvalidSensors { get; set; }
    public int TotalSensors { get; set; }
    public int CalibrationOverdueSensors { get; set; }
    public double DataCompletenessPercent { get; set; }

    public int TasksToday { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }

    public DashboardWeatherSnapshot Weather { get; set; } = new();
    public List<DashboardInfrastructureStatus> Infrastructure { get; set; } = [];
    public List<DashboardPondSummary> Ponds { get; set; } = [];
    public List<DashboardCategoryValue> StageDistribution { get; set; } = [];
    public List<DashboardCategoryValue> SpeciesDistribution { get; set; } = [];
    public List<DashboardSeriesPoint> BiomassHistory { get; set; } = [];
    public List<DashboardSeriesPoint> FeedHistory { get; set; } = [];
    public List<DashboardSeriesPoint> EnergyHistory { get; set; } = [];
    public List<DashboardSeriesPoint> AlertHistory { get; set; } = [];
    public List<DashboardSeriesPoint> SensorAvailabilityHistory { get; set; } = [];
    public List<DashboardHeatmapRow> WaterQualityHeatmap { get; set; } = [];
    public List<DashboardTaskItem> TodayTasks { get; set; } = [];
}

public sealed class DashboardPondSummary
{
    public string PondId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double AreaM2 { get; set; }
    public DateOnly StockingDate { get; set; }
    public DateOnly EstimatedHarvestDate { get; set; }
    public int CultureAgeDays { get; set; }
    public double StockingDensity { get; set; }
    public double EstimatedBiomassKg { get; set; }
    public double SurvivalRatePercent { get; set; }
    public double AverageWeightGram { get; set; }
    public double FeedPlanTodayKg { get; set; }
    public double FeedActualTodayKg { get; set; }
    public double FeedCycleTotalKg { get; set; }
    public double Fcr { get; set; }
    public int HealthScore { get; set; }
    public string HealthStatus { get; set; } = "good";
    public int ActiveAlerts { get; set; }
    public int RunningDevices { get; set; }
    public int TotalDevices { get; set; }
    public int OnlineSensors { get; set; }
    public int TotalSensors { get; set; }
    public string DataQuality { get; set; } = "unknown";
    public string DataSourceSummary { get; set; } = string.Empty;
    public DateTime LastUpdateLocal { get; set; }
    public TelemetryData Telemetry { get; set; } = new();
}

public sealed class DashboardWeatherSnapshot
{
    public string StationName { get; set; } = string.Empty;
    public double AirTemperatureC { get; set; }
    public double HumidityPercent { get; set; }
    public double RainfallMmToday { get; set; }
    public double WindSpeedMs { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "normal";
    public string AlertTitle { get; set; } = string.Empty;
    public string AlertDetail { get; set; } = string.Empty;
    public DateTime UpdatedAtLocal { get; set; }
}

public sealed class DashboardInfrastructureStatus
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "online";
    public string Detail { get; set; } = string.Empty;
    public int LatencyMs { get; set; }
}

public sealed class DashboardTaskItem
{
    public string Id { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string PondName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime DueAtLocal { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
}

public sealed class DashboardCategoryValue
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class DashboardSeriesPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public double SecondaryValue { get; set; }
}

public sealed class DashboardHeatmapRow
{
    public string PondId { get; set; } = string.Empty;
    public string PondName { get; set; } = string.Empty;
    public List<DashboardHeatmapCell> Cells { get; set; } = [];
}

public sealed class DashboardHeatmapCell
{
    public string Metric { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = "normal";
    public string FormattedValue { get; set; } = string.Empty;
}
