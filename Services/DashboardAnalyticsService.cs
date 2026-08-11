using System.Globalization;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Tổng hợp dữ liệu Dashboard từ runtime thật và các tham số nghiệp vụ mô phỏng.
/// Component không cần biết dữ liệu đến từ MongoDB, MQTT, Modbus hay file mô phỏng.
/// </summary>
public sealed class DashboardAnalyticsService
{
    private static readonly string[] HeatmapMetrics =
        ["dissolvedOxygen", "ph", "temperature", "salinity", "ammonia", "waterLevel"];

    private readonly FarmRuntimeState _runtime;
    private readonly IOptionsMonitor<SensorThresholdCatalog> _thresholds;
    private readonly IOptionsMonitor<DashboardSimulationOptions> _simulation;
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;

    public DashboardAnalyticsService(
        FarmRuntimeState runtime,
        IOptionsMonitor<SensorThresholdCatalog> thresholds,
        IOptionsMonitor<DashboardSimulationOptions> simulation,
        IOptionsMonitor<SystemBehaviorOptions> behavior)
    {
        _runtime = runtime;
        _thresholds = thresholds;
        _simulation = simulation;
        _behavior = behavior;
    }

    public DashboardSnapshot BuildSnapshot()
    {
        var nowLocal = DateTime.Now;
        var today = DateOnly.FromDateTime(nowLocal);
        var options = _simulation.CurrentValue;
        var ponds = _runtime.Ponds;
        var devices = _runtime.Devices;
        var sensors = _runtime.Sensors;
        var alerts = _runtime.Alerts;
        var mappedTasks = options.Tasks.Select(x => MapTask(x, ponds, nowLocal)).ToList();
        var tasks = mappedTasks.Where(x => x.DueAtLocal.Date == nowLocal.Date || x.IsOverdue).ToList();
        var profiles = options.Ponds.ToDictionary(x => x.PondId, StringComparer.OrdinalIgnoreCase);
        var pondSummaries = ponds.Select(pond => BuildPondSummary(pond, profiles.GetValueOrDefault(pond.Id), nowLocal)).ToList();
        var ages = pondSummaries.Where(x => x.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.CultureAgeDays).OrderBy(x => x).ToList();
        var activeAlerts = alerts.Where(x => !x.IsResolved).ToList();
        var onlineSensors = sensors.Count(x => x.IsEnabled && x.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
        var invalidSensors = sensors.Count(x => x.IsEnabled && x.LastQuality.Equals("invalid", StringComparison.OrdinalIgnoreCase));
        var overdueCalibration = sensors.Count(x => x.IsEnabled && x.CalibrationDueDateUtc.HasValue && x.CalibrationDueDateUtc.Value < DateTime.UtcNow);
        var currentPower = devices.Where(IsDeviceRunning).Sum(x => x.PowerKw);
        var energyToday = EstimateEnergyToday(options, currentPower, nowLocal);
        var feedToday = pondSummaries.Sum(x => x.FeedActualTodayKg);
        var feedPlan = pondSummaries.Sum(x => x.FeedPlanTodayKg);
        var doValues = pondSummaries.Where(x => x.Telemetry.IsOnline && x.Telemetry.Quality is not "invalid" and not "no-data")
            .Select(x => x.Telemetry.DissolvedOxygen).ToList();
        var doThreshold = GetThreshold("dissolvedOxygen");
        var harvestSoonCount = ponds.Count(pond =>
        {
            var remainingDays = DaysUntil(today, pond.EstimatedHarvestDate);
            return remainingDays >= 0 && remainingDays <= options.HarvestSoonDays;
        });
        var latestEnergy = options.DailyHistory.LastOrDefault()?.EnergyKwh ?? 0d;
        var averagePowerFromHistory = latestEnergy / Math.Max(1d, options.ExpectedOperatingHoursPerDay);
        var peakPowerToday = Math.Max(currentPower, averagePowerFromHistory) * options.PeakPowerFactor;
        var enabledSensorCount = sensors.Count(x => x.IsEnabled);
        var validOnlineSensorCount = Math.Max(0, onlineSensors - invalidSensors);

        return new DashboardSnapshot
        {
            GeneratedAtLocal = nowLocal,
            IsSimulation = options.Enabled,
            DataSourceLabel = options.DataSourceLabel,
            RefreshSeconds = options.RefreshSeconds,
            HealthStableMinimum = _behavior.CurrentValue.HealthScore.StableMinimum,
            HealthMonitorMinimum = _behavior.CurrentValue.HealthScore.MonitorMinimum,
            TotalPonds = ponds.Count,
            TotalAreaM2 = ponds.Sum(x => x.Area),
            ActiveCycles = ponds.Count(x => x.Status.Equals("active", StringComparison.OrdinalIgnoreCase)),
            HarvestSoonCount = harvestSoonCount,
            MedianCultureAgeDays = Median(ages),
            MinimumCultureAgeDays = ages.Count == 0 ? 0 : ages.Min(),
            MaximumCultureAgeDays = ages.Count == 0 ? 0 : ages.Max(),
            EstimatedBiomassKg = pondSummaries.Sum(x => x.EstimatedBiomassKg),
            BiomassChangeKg = pondSummaries.Sum(x => profiles.GetValueOrDefault(x.PondId)?.BiomassDailyGrowthKg ?? 0),
            LatestBiomassSampleAtLocal = options.Ponds.Count == 0 ? null : options.Ponds.Max(x => x.LastSampleAtUtc).ToLocalTime(),
            BiomassConfidence = ResolveOverallConfidence(options.Ponds),
            RunningDevices = devices.Count(IsDeviceRunning),
            StoppedDevices = devices.Count(x => !IsDeviceRunning(x) && IsDeviceConnected(x)),
            FaultDevices = devices.Count(x => x.Status.Equals("fault", StringComparison.OrdinalIgnoreCase)),
            OfflineDevices = devices.Count(x => !IsDeviceConnected(x)),
            AutoDevices = devices.Count(x => x.Mode.Equals("auto", StringComparison.OrdinalIgnoreCase)),
            TotalDevices = devices.Count,
            CurrentPowerKw = currentPower,
            PeakPowerTodayKw = peakPowerToday,
            EnergyTodayKwh = energyToday,
            EnergyMonthKwh = options.DailyHistory.Sum(x => x.EnergyKwh) * Math.Max(1d, nowLocal.Day / (double)Math.Max(1, options.DailyHistory.Count)),
            ActiveAlerts = activeAlerts.Count,
            CriticalAlerts = activeAlerts.Count(x => x.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase)),
            WarningAlerts = activeAlerts.Count(x => !x.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase)),
            UnacknowledgedAlerts = activeAlerts.Count(x => x.AcknowledgedAtUtc is null),
            AverageDo = doValues.Count == 0 ? 0 : doValues.Average(),
            MinimumDo = doValues.Count == 0 ? 0 : doValues.Min(),
            PondsBelowDoWarning = doValues.Count(x => doThreshold.WarningLow.HasValue && x <= doThreshold.WarningLow.Value),
            ValidDoPonds = doValues.Count,
            FeedTodayKg = feedToday,
            FeedPlanTodayKg = feedPlan,
            FeedCycleTotalKg = pondSummaries.Sum(x => x.FeedCycleTotalKg),
            OnlineSensors = onlineSensors,
            OfflineSensors = Math.Max(0, enabledSensorCount - onlineSensors),
            InvalidSensors = invalidSensors,
            TotalSensors = enabledSensorCount,
            CalibrationOverdueSensors = overdueCalibration,
            DataCompletenessPercent = enabledSensorCount == 0 ? 0 : Math.Round(validOnlineSensorCount * 100d / enabledSensorCount, 1),
            TasksToday = tasks.Count,
            CompletedTasks = tasks.Count(x => x.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)),
            OverdueTasks = tasks.Count(x => x.IsOverdue),
            Weather = BuildWeather(options, nowLocal),
            Infrastructure = BuildInfrastructure(options),
            Ponds = pondSummaries,
            StageDistribution = BuildStageDistribution(pondSummaries, options),
            SpeciesDistribution = BuildSpeciesDistribution(pondSummaries),
            BiomassHistory = options.DailyHistory.OrderBy(x => x.DayOffset).Select(x => ToSeriesPoint(x, "biomass", today)).ToList(),
            FeedHistory = options.DailyHistory.OrderBy(x => x.DayOffset).Select(x => ToSeriesPoint(x, "feed", today)).ToList(),
            EnergyHistory = options.DailyHistory.OrderBy(x => x.DayOffset).Select(x => ToSeriesPoint(x, "energy", today)).ToList(),
            AlertHistory = options.DailyHistory.OrderBy(x => x.DayOffset).Select(x => ToSeriesPoint(x, "alerts", today)).ToList(),
            SensorAvailabilityHistory = options.DailyHistory.OrderBy(x => x.DayOffset).Select(x => ToSeriesPoint(x, "availability", today)).ToList(),
            WaterQualityHeatmap = pondSummaries.Select(BuildHeatmapRow).ToList(),
            TodayTasks = tasks.OrderBy(x => x.IsOverdue ? 0 : x.Status == "completed" ? 2 : 1).ThenBy(x => x.DueAtLocal).ToList()
        };
    }

    public string GetSectionTitle(string section) => NormalizeSection(section) switch
    {
        "growth" => "Biomass và tăng trưởng",
        "water-quality" => "Chất lượng nước",
        "feed" => "Thức ăn",
        "equipment" => "Thiết bị và năng lượng",
        "risks" => "Cảnh báo và rủi ro",
        _ => "Ao nuôi và vụ nuôi"
    };

    public string GetSectionDescription(string section) => NormalizeSection(section) switch
    {
        "growth" => "Theo dõi sinh khối ước tính, tốc độ tăng trưởng, trọng lượng mẫu và FCR theo từng ao.",
        "water-quality" => "So sánh trạng thái DO, pH, nhiệt độ, độ mặn, NH₃ và mực nước trên toàn trại.",
        "feed" => "Đối chiếu kế hoạch, thực tế, tổng lượng thức ăn và hiệu quả sử dụng theo ao.",
        "equipment" => "Phân tích trạng thái thiết bị, chế độ AUTO/MANUAL, công suất và điện năng tiêu thụ.",
        "risks" => "Tập trung cảnh báo đang hoạt động, chất lượng dữ liệu và sức khỏe hạ tầng kết nối.",
        _ => "Phân loại ao theo đối tượng nuôi, trạng thái vận hành, tuổi vụ và kế hoạch thu hoạch."
    };

    public string NormalizeSection(string? section)
    {
        var value = section?.Trim().ToLowerInvariant() ?? "ponds";
        return value is "ponds" or "growth" or "water-quality" or "feed" or "equipment" or "risks" ? value : "ponds";
    }

    public string MetricStatus(string metric, double value, bool isOnline = true, string quality = "good")
    {
        if (!isOnline || quality is "no-data" or "invalid") return "no-data";
        var threshold = GetThreshold(metric);
        if ((threshold.CriticalLow.HasValue && value <= threshold.CriticalLow.Value) ||
            (threshold.CriticalHigh.HasValue && value >= threshold.CriticalHigh.Value)) return "critical";
        if ((threshold.WarningLow.HasValue && value <= threshold.WarningLow.Value) ||
            (threshold.WarningHigh.HasValue && value >= threshold.WarningHigh.Value)) return "warning";
        return "normal";
    }

    private DashboardPondSummary BuildPondSummary(Pond pond, DashboardPondSimulationOptions? profile, DateTime nowLocal)
    {
        var telemetry = _runtime.GetTelemetry(pond.Id);
        var sensors = _runtime.Sensors.Where(x => x.PondId == pond.Id && x.IsEnabled).ToList();
        var devices = _runtime.Devices.Where(x => x.PondId == pond.Id).ToList();
        var activeAlerts = _runtime.Alerts.Count(x => x.PondId == pond.Id && !x.IsResolved);
        var baseBiomass = profile?.BiomassAtReferenceKg ?? Math.Max(100, pond.Area * pond.StockingDensity * _simulation.CurrentValue.DefaultBiomassDensityFactor);
        var daysFromReference = (DateTime.UtcNow.Date - _simulation.CurrentValue.ReferenceDateUtc.Date).TotalDays;
        var dynamicFactor = DynamicFactor(pond.Id, nowLocal, _simulation.CurrentValue.DynamicVariationPercent);
        var biomass = Math.Max(0, baseBiomass + (profile?.BiomassDailyGrowthKg ?? baseBiomass * (_simulation.CurrentValue.DefaultDailyBiomassGrowthPercent / 100d)) * daysFromReference);
        biomass *= dynamicFactor;
        var feedActual = Math.Max(0, (profile?.FeedActualTodayKg ?? biomass * (_simulation.CurrentValue.DefaultFeedRatePercentOfBiomass / 100d)) * DynamicFactor($"feed:{pond.Id}", nowLocal, _simulation.CurrentValue.DynamicVariationPercent));
        var realSensors = sensors.Count(x => !x.SourceType.Contains("simulation", StringComparison.OrdinalIgnoreCase));
        var simulatedSensors = sensors.Count - realSensors;
        var cultureAge = Math.Max(0, todayDays(pond.StockingDate));
        var healthScore = CalculateHealthScore(telemetry, pond.Id);

        return new DashboardPondSummary
        {
            PondId = pond.Id,
            Name = pond.Name,
            Species = pond.Species,
            Status = pond.Status,
            AreaM2 = pond.Area,
            StockingDate = pond.StockingDate,
            EstimatedHarvestDate = pond.EstimatedHarvestDate,
            CultureAgeDays = cultureAge,
            StockingDensity = pond.StockingDensity,
            EstimatedBiomassKg = biomass,
            SurvivalRatePercent = profile?.SurvivalRatePercent ?? _simulation.CurrentValue.DefaultSurvivalRatePercent,
            AverageWeightGram = profile?.AverageWeightGram ?? Math.Max(_simulation.CurrentValue.MinimumAverageWeightGram, cultureAge * _simulation.CurrentValue.DefaultAverageWeightPerDayGram),
            FeedPlanTodayKg = profile?.FeedPlanTodayKg ?? biomass * (_simulation.CurrentValue.DefaultFeedRatePercentOfBiomass / 100d),
            FeedActualTodayKg = feedActual,
            FeedCycleTotalKg = profile?.FeedCycleTotalKg ?? biomass * _simulation.CurrentValue.DefaultCycleFeedMultiplier,
            Fcr = profile?.Fcr ?? _simulation.CurrentValue.DefaultFcr,
            HealthScore = healthScore,
            HealthStatus = ResolveHealthStatus(healthScore),
            ActiveAlerts = activeAlerts,
            RunningDevices = devices.Count(IsDeviceRunning),
            TotalDevices = devices.Count,
            OnlineSensors = sensors.Count(x => x.Status.Equals("online", StringComparison.OrdinalIgnoreCase)),
            TotalSensors = sensors.Count,
            RealSensors = realSensors,
            SimulatedSensors = simulatedSensors,
            DataQuality = telemetry.Quality,
            DataSourceSummary = BuildSourceSummary(realSensors, simulatedSensors),
            LastUpdateLocal = telemetry.LastSeenAtUtc.ToLocalTime(),
            Telemetry = telemetry
        };

        int todayDays(DateOnly date) => DateOnly.FromDateTime(nowLocal).DayNumber - date.DayNumber;
    }

    private DashboardHeatmapRow BuildHeatmapRow(DashboardPondSummary pond)
    {
        var row = new DashboardHeatmapRow { PondId = pond.PondId, PondName = pond.Name };
        foreach (var metric in HeatmapMetrics)
        {
            var threshold = GetThreshold(metric);
            var value = MetricValue(pond.Telemetry, metric);
            row.Cells.Add(new DashboardHeatmapCell
            {
                Metric = metric,
                Label = threshold.DisplayName,
                Value = value,
                Unit = threshold.Unit,
                Status = MetricStatus(metric, value, pond.Telemetry.IsOnline, pond.Telemetry.Quality),
                FormattedValue = value.ToString($"N{threshold.DecimalPlaces}", CultureInfo.GetCultureInfo("vi-VN"))
            });
        }
        return row;
    }

    private DashboardWeatherSnapshot BuildWeather(DashboardSimulationOptions options, DateTime nowLocal)
    {
        var source = options.Weather;
        var cycle = Math.Sin((nowLocal.TimeOfDay.TotalHours - 6) / 24d * Math.PI * 2d);
        return new DashboardWeatherSnapshot
        {
            StationName = source.StationName,
            AirTemperatureC = source.AirTemperatureC + cycle * options.WeatherTemperatureAmplitudeC,
            HumidityPercent = Math.Clamp(source.HumidityPercent - cycle * options.WeatherHumidityAmplitudePercent, 0, 100),
            RainfallMmToday = Math.Max(0, source.RainfallMmToday + Math.Max(0, -cycle) * options.WeatherRainfallAmplitudeMm),
            WindSpeedMs = Math.Max(0, source.WindSpeedMs + Math.Sin(nowLocal.Minute / 60d * Math.PI * 2d) * options.WeatherWindAmplitudeMs),
            Condition = source.Condition,
            RiskLevel = source.RiskLevel,
            AlertTitle = source.AlertTitle,
            AlertDetail = source.AlertDetail,
            UpdatedAtLocal = nowLocal
        };
    }

    private List<DashboardInfrastructureStatus> BuildInfrastructure(DashboardSimulationOptions options)
    {
        var configured = options.Infrastructure.Select(x => new DashboardInfrastructureStatus
        {
            Id = x.Id,
            Name = x.Name,
            Type = x.Type,
            Status = x.Status,
            Detail = x.Detail,
            LatencyMs = x.LatencyMs
        }).ToList();

        foreach (var source in _runtime.DataSources)
        {
            var existing = configured.FirstOrDefault(x => x.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                configured.Add(new DashboardInfrastructureStatus
                {
                    Id = source.Id,
                    Name = source.SourceName,
                    Type = "data-source",
                    Status = source.IsConnected ? "online" : source.IsEnabled ? "offline" : "disabled",
                    Detail = source.LastError ?? $"{source.MessagesReceived:N0} bản tin đã nhận",
                    LatencyMs = 0
                });
            }
            else
            {
                existing.Status = source.IsConnected ? "online" : source.IsEnabled ? "offline" : existing.Status;
                if (!string.IsNullOrWhiteSpace(source.LastError)) existing.Detail = source.LastError;
            }
        }
        return configured;
    }

    private static DashboardTaskItem MapTask(DashboardTaskSimulationOptions task, IReadOnlyList<Pond> ponds, DateTime nowLocal)
    {
        var dueLocal = ResolveTaskDueLocal(task, nowLocal);
        return new DashboardTaskItem
        {
            Id = task.Id,
            PondId = task.PondId,
            PondName = ponds.FirstOrDefault(x => x.Id == task.PondId)?.Name ?? "Toàn trại",
            Title = task.Title,
            Category = task.Category,
            Status = task.Status,
            DueAtLocal = dueLocal,
            AssignedTo = task.AssignedTo,
            IsOverdue = !task.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) && dueLocal < nowLocal
        };
    }

    private static DateTime ResolveTaskDueLocal(DashboardTaskSimulationOptions task, DateTime nowLocal)
    {
        if (task.DueAtUtc.HasValue) return task.DueAtUtc.Value.ToLocalTime();
        var date = nowLocal.Date.AddDays(task.DayOffset);
        return TimeOnly.TryParseExact(task.TimeLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? date.Add(time.ToTimeSpan())
            : date.AddHours(8);
    }

    private List<DashboardCategoryValue> BuildStageDistribution(IReadOnlyList<DashboardPondSummary> ponds, DashboardSimulationOptions options)
    {
        var values = new Dictionary<string, (string Label, int Count)>
        {
            ["preparing"] = ("Chuẩn bị ao", 0),
            ["new"] = ("Mới thả", 0),
            ["growth"] = ("Sinh trưởng", 0),
            ["late-growth"] = ("Tăng trưởng cuối", 0),
            ["harvest-soon"] = ("Sắp thu hoạch", 0),
            ["inactive"] = ("Dừng/Bảo trì", 0)
        };

        foreach (var pond in ponds)
        {
            string key;
            if (!pond.Status.Equals("active", StringComparison.OrdinalIgnoreCase)) key = pond.Status.Equals("preparing", StringComparison.OrdinalIgnoreCase) ? "preparing" : "inactive";
            else
            {
                var remaining = pond.EstimatedHarvestDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
                key = remaining <= options.HarvestSoonDays
                    ? "harvest-soon"
                    : pond.CultureAgeDays <= options.NewCycleDays
                        ? "new"
                        : pond.CultureAgeDays <= options.EarlyGrowthDays ? "growth" : "late-growth";
            }
            var current = values[key];
            values[key] = (current.Label, current.Count + 1);
        }

        return values.Select(x => new DashboardCategoryValue { Key = x.Key, Label = x.Value.Label, Value = x.Value.Count, Detail = $"{x.Value.Count} ao" }).ToList();
    }

    private static List<DashboardCategoryValue> BuildSpeciesDistribution(IReadOnlyList<DashboardPondSummary> ponds) =>
        ponds.GroupBy(x => x.Species)
            .OrderByDescending(x => x.Count())
            .Select(x => new DashboardCategoryValue
            {
                Key = x.Key,
                Label = x.Key,
                Value = x.Count(),
                Detail = $"{x.Sum(p => p.EstimatedBiomassKg) / 1000d:N1} tấn Biomass"
            }).ToList();

    private static DashboardSeriesPoint ToSeriesPoint(DashboardDailyHistoryOptions item, string kind, DateOnly today)
    {
        var label = today.AddDays(item.DayOffset).ToString("dd/MM");
        return kind switch
        {
            "biomass" => new DashboardSeriesPoint { Label = label, Value = item.BiomassKg },
            "feed" => new DashboardSeriesPoint { Label = label, Value = item.FeedKg },
            "energy" => new DashboardSeriesPoint { Label = label, Value = item.EnergyKwh },
            "alerts" => new DashboardSeriesPoint { Label = label, Value = item.WarningAlerts, SecondaryValue = item.CriticalAlerts },
            _ => new DashboardSeriesPoint { Label = label, Value = item.SensorAvailabilityPercent }
        };
    }

    private int CalculateHealthScore(TelemetryData telemetry, string pondId)
    {
        var options = _behavior.CurrentValue.HealthScore;
        if (!telemetry.IsOnline || telemetry.Quality is "invalid" or "no-data") return options.OfflineScore;
        var unsafeCount = HeatmapMetrics.Count(metric => MetricStatus(metric, MetricValue(telemetry, metric), telemetry.IsOnline, telemetry.Quality) is "warning" or "critical");
        var criticalCount = _runtime.Alerts.Count(x => x.PondId == pondId && !x.IsResolved && x.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
        return Math.Clamp(options.InitialScore - unsafeCount * options.MetricOutOfRangePenalty - criticalCount * options.CriticalAlertPenalty, 0, 100);
    }

    private string ResolveHealthStatus(int score)
    {
        var health = _behavior.CurrentValue.HealthScore;
        return score >= health.StableMinimum ? "good" : score >= health.MonitorMinimum ? "warning" : "critical";
    }

    private MetricThresholdOptions GetThreshold(string metric)
    {
        if (_thresholds.CurrentValue.Metrics.TryGetValue(metric, out var threshold)) return threshold;
        throw new KeyNotFoundException($"Metric '{metric}' chưa được cấu hình trong sensor-thresholds.json.");
    }

    private static double MetricValue(TelemetryData telemetry, string metric) => metric switch
    {
        "temperature" => telemetry.Temperature,
        "ph" => telemetry.Ph,
        "salinity" => telemetry.Salinity,
        "ammonia" => telemetry.Ammonia,
        "waterLevel" => telemetry.WaterLevel,
        _ => telemetry.DissolvedOxygen
    };

    private static bool IsDeviceRunning(Device device) => device.Status.Equals("on", StringComparison.OrdinalIgnoreCase) || device.Status.Equals("running", StringComparison.OrdinalIgnoreCase);
    private static bool IsDeviceConnected(Device device) => !device.ConnectionStatus.Equals("offline", StringComparison.OrdinalIgnoreCase) && !device.Status.Equals("offline", StringComparison.OrdinalIgnoreCase);
    private static int DaysUntil(DateOnly today, DateOnly target) => target.DayNumber - today.DayNumber;
    private static int Median(IReadOnlyList<int> sorted) => sorted.Count == 0 ? 0 : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (int)Math.Round((sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2d);

    private static string BuildSourceSummary(int realSensors, int simulatedSensors)
    {
        if (realSensors > 0 && simulatedSensors > 0) return $"{realSensors} thực · {simulatedSensors} mô phỏng";
        if (realSensors > 0) return $"{realSensors} cảm biến thực";
        return $"{simulatedSensors} cảm biến mô phỏng";
    }

    private static string ResolveOverallConfidence(IEnumerable<DashboardPondSimulationOptions> ponds)
    {
        var values = ponds.Select(x => x.BiomassConfidence.ToLowerInvariant()).ToList();
        if (values.Count == 0) return "unknown";
        if (values.Contains("low")) return "low";
        if (values.Contains("medium")) return "medium";
        return "high";
    }

    private static double DynamicFactor(string key, DateTime nowLocal, double variationPercent)
    {
        var stableHash = key.Aggregate(17, (current, c) => unchecked(current * 31 + c));
        var phase = Math.Abs(stableHash % 360) * Math.PI / 180d;
        var wave = Math.Sin(nowLocal.TimeOfDay.TotalMinutes / 17d + phase);
        return 1d + wave * variationPercent / 100d;
    }

    private static double EstimateEnergyToday(DashboardSimulationOptions options, double currentPower, DateTime nowLocal)
    {
        var configured = options.DailyHistory.LastOrDefault()?.EnergyKwh ?? 0;
        var elapsedRatio = Math.Clamp(nowLocal.TimeOfDay.TotalHours / 24d, 0.03, 1);
        var estimateFromLoad = currentPower * Math.Max(1, nowLocal.TimeOfDay.TotalHours) * options.EnergyLoadFactor;
        return Math.Max(estimateFromLoad, configured * elapsedRatio);
    }
}
