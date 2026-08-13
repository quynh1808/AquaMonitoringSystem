using System.Globalization;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Builds dashboard projections only from persisted/runtime observations. Missing weather,
/// energy or growth measurements remain unavailable instead of being replaced by generated data.
/// </summary>
public sealed class DashboardAnalyticsService
{
    private readonly FarmRuntimeState _runtime;
    private readonly IOptionsMonitor<SensorThresholdCatalog> _thresholds;
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;
    private readonly IOptionsMonitor<OperationalModeOptions> _mode;

    public DashboardAnalyticsService(FarmRuntimeState runtime,
        IOptionsMonitor<SensorThresholdCatalog> thresholds,
        IOptionsMonitor<SystemBehaviorOptions> behavior,
        IOptionsMonitor<OperationalModeOptions> mode)
    {
        _runtime = runtime;
        _thresholds = thresholds;
        _behavior = behavior;
        _mode = mode;
    }

    public DashboardSnapshot BuildSnapshot(IEnumerable<string> accessiblePondIds)
    {
        var now = DateTime.Now;
        var allowed = accessiblePondIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ponds = _runtime.Ponds.Where(x => allowed.Contains(x.Id)).ToList();
        var devices = _runtime.Devices.Where(x => allowed.Contains(x.PondId) && !x.IsArchived && !x.IsPendingRemoval).ToList();
        var sensors = _runtime.Sensors.Where(x => allowed.Contains(x.PondId) && !x.IsArchived && !x.IsPendingRemoval && x.IsEnabled).ToList();
        var alerts = _runtime.Alerts.Where(x => allowed.Contains(x.PondId)).ToList();
        var cycles = _runtime.ProductionCycles.Where(x => allowed.Contains(x.PondId)).ToList();
        var openCycles = cycles.Where(x => x.IsOpen).ToList();
        var summaries = ponds.Select(x => BuildPondSummary(x, now)).ToList();
        var activeAlerts = alerts.Where(x => !x.IsResolved).ToList();
        var onlineSensors = sensors.Count(x => x.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
        var validSensors = sensors.Count(x => x.Status.Equals("online", StringComparison.OrdinalIgnoreCase) &&
            x.LastQuality.Equals("good", StringComparison.OrdinalIgnoreCase));
        var validDo = summaries.SelectMany(x => x.SensorMetrics).Where(x => x.Metric == "dissolvedOxygen" &&
            x.Status is "normal" or "warning" or "critical").Select(x => x.Value).ToList();
        var doThreshold = GetThreshold("dissolvedOxygen");
        var ages = summaries.Where(x => x.Status == "active").Select(x => x.CultureAgeDays).Order().ToList();
        var todayTasks = BuildTasks(allowed, now);
        var currentPower = devices.Where(IsDeviceRunning).Sum(x => x.PowerKw);

        return new DashboardSnapshot
        {
            GeneratedAtLocal = now,
            DataSourceLabel = $"{ModeLabel(_mode.CurrentValue.Mode)} · MQTT telemetry + FX5U Modbus TCP",
            RefreshSeconds = 8,
            HealthStableMinimum = _behavior.CurrentValue.HealthScore.StableMinimum,
            HealthMonitorMinimum = _behavior.CurrentValue.HealthScore.MonitorMinimum,
            TotalPonds = ponds.Count,
            TotalAreaM2 = ponds.Sum(x => x.Area),
            ActiveCycles = openCycles.Count,
            HarvestSoonCount = ponds.Count(x => DaysUntil(x.EstimatedHarvestDate) is >= 0 and <= 21),
            MedianCultureAgeDays = Median(ages),
            MinimumCultureAgeDays = ages.Count == 0 ? 0 : ages.Min(),
            MaximumCultureAgeDays = ages.Count == 0 ? 0 : ages.Max(),
            EstimatedBiomassKg = openCycles.Sum(x => x.EstimatedBiomassKg),
            BiomassChangeKg = openCycles.Sum(x => x.EstimatedDailyBiomassGrowthKg),
            HasBiomassGrowthData = openCycles.Any(x => x.GrowthSampleCount >= 2),
            LatestBiomassSampleAtLocal = openCycles.Where(x => x.LatestSampleAtUtc.HasValue)
                .Select(x => (DateTime?)x.LatestSampleAtUtc!.Value.ToLocalTime()).Max(),
            BiomassConfidence = openCycles.Any(x => x.LatestSampleAtUtc.HasValue) ? "sampled" : "unknown",
            RunningDevices = devices.Count(IsDeviceRunning),
            StoppedDevices = devices.Count(x => !IsDeviceRunning(x) && IsDeviceConnected(x)),
            FaultDevices = devices.Count(x => x.Status == "fault"),
            OfflineDevices = devices.Count(x => !IsDeviceConnected(x)),
            AutoDevices = devices.Count(x => x.Mode == "auto"),
            TotalDevices = devices.Count,
            CurrentPowerKw = currentPower,
            // No power meter is connected in P03, therefore cumulative energy is intentionally zero.
            PeakPowerTodayKw = currentPower,
            ActiveAlerts = activeAlerts.Count,
            CriticalAlerts = activeAlerts.Count(x => x.Severity == "critical"),
            WarningAlerts = activeAlerts.Count(x => x.Severity != "critical"),
            UnacknowledgedAlerts = activeAlerts.Count(x => !x.AcknowledgedAtUtc.HasValue),
            AverageDo = validDo.Count == 0 ? 0 : validDo.Average(),
            MinimumDo = validDo.Count == 0 ? 0 : validDo.Min(),
            PondsBelowDoWarning = validDo.Count(x => doThreshold.WarningLow.HasValue && x <= doThreshold.WarningLow.Value),
            ValidDoPonds = validDo.Count,
            FeedTodayKg = _runtime.FeedLogs.Where(x => allowed.Contains(x.PondId) &&
                x.Timestamp.ToLocalTime().Date == now.Date).Sum(x => x.AmountKg),
            FeedCycleTotalKg = openCycles.Sum(x => x.TotalFeedKg),
            OnlineSensors = onlineSensors,
            OfflineSensors = Math.Max(0, sensors.Count - onlineSensors),
            InvalidSensors = sensors.Count(x => x.LastQuality == "invalid"),
            TotalSensors = sensors.Count,
            CalibrationOverdueSensors = sensors.Count(x => x.CalibrationDueDateUtc < DateTime.UtcNow),
            DataCompletenessPercent = sensors.Count == 0 ? 0 : Math.Round(validSensors * 100d / sensors.Count, 1),
            TasksToday = todayTasks.Count,
            CompletedTasks = todayTasks.Count(x => x.Status == "completed"),
            OverdueTasks = todayTasks.Count(x => x.IsOverdue),
            Weather = UnavailableWeather(now),
            Infrastructure = BuildInfrastructure(devices),
            Ponds = summaries,
            StageDistribution = BuildStageDistribution(summaries),
            SpeciesDistribution = BuildSpeciesDistribution(summaries),
            FeedHistory = BuildFeedHistory(allowed, now),
            AlertHistory = BuildAlertHistory(alerts, now),
            SensorAvailabilityHistory = [new() { Label = now.ToString("dd/MM"), Value = sensors.Count == 0 ? 0 : onlineSensors * 100d / sensors.Count }],
            WaterQualityHeatmap = BuildHeatmapRows(summaries),
            TodayTasks = todayTasks
        };
    }

    public string GetSectionTitle(string section) => NormalizeSection(section) switch
    {
        "growth" => "Biomass và tăng trưởng", "water-quality" => "Chất lượng nước",
        "feed" => "Thức ăn", "equipment" => "Thiết bị và năng lượng",
        "risks" => "Cảnh báo và rủi ro", _ => "Ao nuôi và vụ nuôi"
    };

    public string GetSectionDescription(string section) => NormalizeSection(section) switch
    {
        "growth" => "Số liệu sinh khối và tăng trưởng chỉ lấy từ các lần lấy mẫu đã ghi nhận.",
        "water-quality" => "DO, pH, nhiệt độ, độ mặn, NH₃ và mực nước nhận qua MQTT.",
        "feed" => "Đối chiếu nhật ký thức ăn theo ao và vụ nuôi.",
        "equipment" => "Trạng thái thiết bị lấy từ coil phản hồi của PLC, không lấy từ lệnh ghi.",
        "risks" => "Cảnh báo, chất lượng dữ liệu và sức khỏe kết nối hiện tại.",
        _ => "Thông tin ao nuôi, vụ nuôi và kế hoạch thu hoạch."
    };

    public string NormalizeSection(string? section)
    {
        var value = section?.Trim().ToLowerInvariant() ?? "ponds";
        return value is "ponds" or "growth" or "water-quality" or "feed" or "equipment" or "risks" ? value : "ponds";
    }

    public string MetricStatus(string metric, double value, bool isOnline = true, string quality = "good")
    {
        if (!isOnline || !quality.Equals("good", StringComparison.OrdinalIgnoreCase)) return "no-data";
        var threshold = GetThreshold(metric);
        if ((threshold.CriticalLow.HasValue && value <= threshold.CriticalLow.Value) ||
            (threshold.CriticalHigh.HasValue && value >= threshold.CriticalHigh.Value)) return "critical";
        if ((threshold.WarningLow.HasValue && value <= threshold.WarningLow.Value) ||
            (threshold.WarningHigh.HasValue && value >= threshold.WarningHigh.Value)) return "warning";
        return "normal";
    }

    private DashboardPondSummary BuildPondSummary(Pond pond, DateTime now)
    {
        var telemetry = _runtime.GetTelemetry(pond.Id);
        var sensors = _runtime.Sensors.Where(x => x.PondId == pond.Id && x.IsEnabled && !x.IsArchived && !x.IsPendingRemoval).ToList();
        var devices = _runtime.Devices.Where(x => x.PondId == pond.Id && !x.IsArchived && !x.IsPendingRemoval).ToList();
        var cycle = _runtime.ProductionCycles.Where(x => x.PondId == pond.Id && x.IsOpen)
            .OrderByDescending(x => x.StockingDate).FirstOrDefault();
        var feeds = _runtime.FeedLogs.Where(x => x.PondId == pond.Id).ToList();
        var sensorMetrics = sensors.Select(sensor =>
        {
            var threshold = GetThreshold(sensor.Metric);
            var value = MetricValue(telemetry, sensor.Metric);
            var online = sensor.Status == "online";
            var status = MetricStatus(sensor.Metric, value, online, sensor.LastQuality);
            return new DashboardHeatmapCell
            {
                SensorId = sensor.Id, Metric = sensor.Metric, Label = sensor.Name, Value = value,
                Unit = sensor.Unit, Status = status,
                FormattedValue = status == "no-data" ? "—" : value.ToString($"N{threshold.DecimalPlaces}", CultureInfo.GetCultureInfo("vi-VN"))
            };
        }).ToList();
        var score = CalculateHealthScore(sensorMetrics, pond.Id);
        var sourceSummary = sensors.Count == 0 ? "Chưa khai báo cảm biến" :
            string.Join(", ", sensors.GroupBy(x => x.SourceType).Select(x => $"{x.Key}: {x.Count()}"));

        return new DashboardPondSummary
        {
            PondId = pond.Id, Name = pond.Name, Species = pond.Species, Status = pond.Status,
            AreaM2 = pond.Area, StockingDate = pond.StockingDate,
            EstimatedHarvestDate = pond.EstimatedHarvestDate,
            CultureAgeDays = Math.Max(0, DateOnly.FromDateTime(now).DayNumber - pond.StockingDate.DayNumber),
            StockingDensity = pond.StockingDensity,
            EstimatedBiomassKg = cycle?.EstimatedBiomassKg ?? 0,
            SurvivalRatePercent = cycle?.EstimatedSurvivalRatePercent ?? 0,
            AverageWeightGram = cycle?.LatestAverageWeightGrams ?? 0,
            FeedActualTodayKg = feeds.Where(x => x.Timestamp.ToLocalTime().Date == now.Date).Sum(x => x.AmountKg),
            FeedCycleTotalKg = cycle?.TotalFeedKg ?? feeds.Sum(x => x.AmountKg),
            Fcr = cycle?.FeedConversionRatio ?? 0,
            HealthScore = score, HealthStatus = sensors.Count == 0 ? "not-configured" : ResolveHealthStatus(score),
            ActiveAlerts = _runtime.Alerts.Count(x => x.PondId == pond.Id && !x.IsResolved),
            RunningDevices = devices.Count(IsDeviceRunning), TotalDevices = devices.Count,
            OnlineSensors = sensors.Count(x => x.Status == "online"), TotalSensors = sensors.Count,
            DataQuality = telemetry.Quality, DataSourceSummary = sourceSummary,
            LastUpdateLocal = telemetry.LastSeenAtUtc == DateTime.MinValue ? DateTime.MinValue : telemetry.LastSeenAtUtc.ToLocalTime(),
            Telemetry = telemetry, SensorMetrics = sensorMetrics
        };
    }

    private List<DashboardHeatmapRow> BuildHeatmapRows(List<DashboardPondSummary> ponds)
    {
        // Columns come from actually commissioned definitions across accessible ponds, never
        // from the six-metric catalog. Missing metrics in another pond render as no-data.
        var metrics = ponds.SelectMany(x => x.SensorMetrics).Select(x => x.Metric)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return ponds.Select(pond => new DashboardHeatmapRow
        {
            PondId = pond.PondId, PondName = pond.Name,
            Cells = metrics.Select(metric => pond.SensorMetrics.FirstOrDefault(x => x.Metric == metric) ??
                new DashboardHeatmapCell
                {
                    Metric = metric, Label = GetThreshold(metric).DisplayName,
                    Unit = GetThreshold(metric).Unit, Status = "no-data", FormattedValue = "—"
                }).ToList()
        }).ToList();
    }

    private List<DashboardInfrastructureStatus> BuildInfrastructure(List<Device> devices)
    {
        var result = _runtime.DataSources.Select(x => new DashboardInfrastructureStatus
        {
            Id = x.Id, Name = x.SourceName, Type = "telemetry",
            Status = x.IsConnected ? "online" : "offline",
            Detail = x.LastError ?? $"Lần nhận cuối: {x.LastMessageAtUtc?.ToLocalTime():dd/MM HH:mm:ss}"
        }).ToList();
        result.Add(new DashboardInfrastructureStatus
        {
            Id = "fx5u-plc", Name = "PLC Mitsubishi FX5U", Type = "plc",
            Status = devices.Count == 0 ? "not-configured" : devices.All(IsDeviceConnected) ? "online" : "offline",
            Detail = devices.Count == 0 ? "Chưa có thiết bị PLC được khai báo" : "Modbus TCP · read-back theo mapping đã commissioning"
        });
        return result;
    }

    private List<DashboardTaskItem> BuildTasks(HashSet<string> allowed, DateTime now) =>
        _runtime.MaintenanceTasks.Where(x => allowed.Contains(x.PondId) &&
            (x.DueAtUtc.ToLocalTime().Date == now.Date || (x.Status != "completed" && x.DueAtUtc < DateTime.UtcNow)))
            .Select(x => new DashboardTaskItem { Id = x.Id, PondId = x.PondId,
                PondName = _runtime.Ponds.FirstOrDefault(p => p.Id == x.PondId)?.Name ?? x.PondId,
                Title = x.Title, Category = x.AssetType, Status = x.Status,
                DueAtLocal = x.DueAtUtc.ToLocalTime(), AssignedTo = x.AssignedTo ?? string.Empty,
                IsOverdue = x.Status != "completed" && x.DueAtUtc < DateTime.UtcNow }).ToList();

    private List<DashboardSeriesPoint> BuildFeedHistory(HashSet<string> allowed, DateTime now) =>
        Enumerable.Range(0, 7).Select(offset => now.Date.AddDays(offset - 6)).Select(day => new DashboardSeriesPoint
        {
            Label = day.ToString("dd/MM"),
            Value = _runtime.FeedLogs.Where(x => allowed.Contains(x.PondId) && x.Timestamp.ToLocalTime().Date == day).Sum(x => x.AmountKg)
        }).ToList();

    private static List<DashboardSeriesPoint> BuildAlertHistory(List<AlertItem> alerts, DateTime now) =>
        Enumerable.Range(0, 7).Select(offset => now.Date.AddDays(offset - 6)).Select(day => new DashboardSeriesPoint
        {
            Label = day.ToString("dd/MM"), Value = alerts.Count(x => x.Timestamp.ToLocalTime().Date == day),
            SecondaryValue = alerts.Count(x => x.Timestamp.ToLocalTime().Date == day && x.Severity == "critical")
        }).ToList();

    private static List<DashboardCategoryValue> BuildStageDistribution(List<DashboardPondSummary> ponds) =>
        new[] { ("new", "Mới thả", 14), ("early", "Tăng trưởng sớm", 45), ("growth", "Tăng trưởng", 90), ("harvest", "Gần thu hoạch", int.MaxValue) }
            .Select((stage, index) => new DashboardCategoryValue { Key = stage.Item1, Label = stage.Item2,
                Value = ponds.Count(x => x.CultureAgeDays <= stage.Item3 && (index == 0 || x.CultureAgeDays > new[] { 0, 14, 45, 90 }[index])),
                Detail = "ao" }).ToList();

    private static List<DashboardCategoryValue> BuildSpeciesDistribution(List<DashboardPondSummary> ponds) =>
        ponds.GroupBy(x => x.Species).Select(x => new DashboardCategoryValue { Key = x.Key, Label = x.Key,
            Value = x.Count(), Detail = $"{x.Sum(p => p.AreaM2):N0} m²" }).ToList();

    private int CalculateHealthScore(List<DashboardHeatmapCell> metrics, string pondId)
    {
        var options = _behavior.CurrentValue.HealthScore;
        if (metrics.Count == 0 || metrics.All(x => x.Status == "no-data")) return options.OfflineScore;
        var checks = metrics.Count(x => x.Status == "normal");
        var critical = _runtime.Alerts.Count(x => x.PondId == pondId && !x.IsResolved && x.Severity == "critical");
        return Math.Clamp(options.InitialScore - (metrics.Count - checks) * options.MetricOutOfRangePenalty -
            critical * options.CriticalAlertPenalty, 0, 100);
    }

    private MetricThresholdOptions GetThreshold(string metric) => _thresholds.CurrentValue.Metrics[metric];
    private string ResolveHealthStatus(int score) => score >= _behavior.CurrentValue.HealthScore.StableMinimum ? "good" :
        score >= _behavior.CurrentValue.HealthScore.MonitorMinimum ? "warning" : "critical";
    private static bool IsDeviceRunning(Device device) => device.Status == "on" && IsDeviceConnected(device);
    private static bool IsDeviceConnected(Device device) => device.ConnectionStatus == "online";
    private static int DaysUntil(DateOnly date) => date.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
    private static int Median(List<int> values) => values.Count == 0 ? 0 : values.Count % 2 == 1 ? values[values.Count / 2] :
        (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2;
    private static string ModeLabel(string mode) => mode switch { "Simulation" => "SIMULATION EXTERNAL",
        "EdgeSimulation" => "EDGE SIMULATION", _ => "PRODUCTION" };
    private static double MetricValue(TelemetryData telemetry, string metric) => metric switch
    {
        "temperature" => telemetry.Temperature, "ph" => telemetry.Ph,
        "salinity" => telemetry.Salinity, "ammonia" => telemetry.Ammonia,
        "waterLevel" => telemetry.WaterLevel, _ => telemetry.DissolvedOxygen
    };

    private static DashboardWeatherSnapshot UnavailableWeather(DateTime now) => new()
    {
        StationName = "Chưa kết nối trạm thời tiết", Condition = "Không có dữ liệu",
        RiskLevel = "unknown", AlertTitle = "Chưa có dữ liệu thời tiết thực",
        AlertDetail = "Kết nối một trạm đo hoặc API nội bộ để kích hoạt chức năng này.", UpdatedAtLocal = now
    };
}
