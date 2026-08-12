using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

/// <summary>Builds pond-scoped dashboard projections from real or explicitly enabled simulation data.</summary>
public sealed class DashboardAnalyticsService
{
	private static readonly string[] HeatmapMetrics = new string[6] { "dissolvedOxygen", "ph", "temperature", "salinity", "ammonia", "waterLevel" };

	private readonly FarmRuntimeState _runtime;

	private readonly IOptionsMonitor<SensorThresholdCatalog> _thresholds;

	private readonly IOptionsMonitor<DashboardSimulationOptions> _simulation;

	private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;

	public DashboardAnalyticsService(FarmRuntimeState runtime, IOptionsMonitor<SensorThresholdCatalog> thresholds, IOptionsMonitor<DashboardSimulationOptions> simulation, IOptionsMonitor<SystemBehaviorOptions> behavior)
	{
		_runtime = runtime;
		_thresholds = thresholds;
		_simulation = simulation;
		_behavior = behavior;
	}

	public DashboardSnapshot BuildSnapshot(IEnumerable<string> accessiblePondIds)
	{
		DateTime nowLocal = DateTime.Now;
		DateOnly today = DateOnly.FromDateTime(nowLocal);
		DashboardSimulationOptions options = _simulation.CurrentValue;
		HashSet<string> allowed = accessiblePondIds.ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<Pond> ponds = _runtime.Ponds.Where((Pond x) => allowed.Contains(x.Id)).ToList();
		List<Device> list = _runtime.Devices.Where((Device x) => allowed.Contains(x.PondId)).ToList();
		List<SensorDefinition> source = _runtime.Sensors.Where((SensorDefinition x) => allowed.Contains(x.PondId)).ToList();
		List<AlertItem> source2 = _runtime.Alerts.Where((AlertItem x) => allowed.Contains(x.PondId)).ToList();
		List<ProductionCycle> source3 = _runtime.ProductionCycles.Where((ProductionCycle x) => allowed.Contains(x.PondId)).ToList();
		List<ProductionCycle> list2 = source3.Where((ProductionCycle x) => x.IsOpen).ToList();
		List<DashboardTaskItem> source4 = (options.Enabled ? (from x in options.Tasks
			where string.IsNullOrWhiteSpace(x.PondId) || allowed.Contains(x.PondId)
			select MapTask(x, ponds, nowLocal)).ToList() : new List<DashboardTaskItem>());
		List<DashboardTaskItem> list3 = source4.Where((DashboardTaskItem x) => x.DueAtLocal.Date == nowLocal.Date || x.IsOverdue).ToList();
		Dictionary<string, DashboardPondSimulationOptions> profiles = options.Ponds.ToDictionary<DashboardPondSimulationOptions, string>((DashboardPondSimulationOptions x) => x.PondId, StringComparer.OrdinalIgnoreCase);
		List<DashboardPondSummary> list4 = ponds.Select((Pond pond) => BuildPondSummary(pond, profiles.GetValueOrDefault(pond.Id), nowLocal)).ToList();
		List<int> list5 = (from x in list4
			where x.Status.Equals("active", StringComparison.OrdinalIgnoreCase)
			select x.CultureAgeDays into x
			orderby x
			select x).ToList();
		List<AlertItem> list6 = source2.Where((AlertItem x) => !x.IsResolved).ToList();
		int num = source.Count((SensorDefinition x) => x.IsEnabled && x.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
		int num2 = source.Count((SensorDefinition x) => x.IsEnabled && x.LastQuality.Equals("invalid", StringComparison.OrdinalIgnoreCase));
		int calibrationOverdueSensors = source.Count((SensorDefinition x) => x.IsEnabled && x.CalibrationDueDateUtc.HasValue && x.CalibrationDueDateUtc.Value < DateTime.UtcNow);
		double num3 = list.Where(IsDeviceRunning).Sum((Device x) => x.PowerKw);
		double energyTodayKwh = (options.Enabled ? EstimateEnergyToday(options, num3, nowLocal) : 0.0);
		double feedTodayKg = list4.Sum((DashboardPondSummary x) => x.FeedActualTodayKg);
		double feedPlanTodayKg = list4.Sum((DashboardPondSummary x) => x.FeedPlanTodayKg);
		List<double> list7 = (from x in list4.Where(delegate(DashboardPondSummary x)
			{
				if (x.Telemetry.IsOnline)
				{
					string quality = x.Telemetry.Quality;
					if (!(quality == "invalid") && !(quality == "no-data"))
					{
						return !(quality == "suspect");
					}
					return false;
				}
				return false;
			})
			select x.Telemetry.DissolvedOxygen).ToList();
		MetricThresholdOptions doThreshold = GetThreshold("dissolvedOxygen");
		int harvestSoonCount = ponds.Count(delegate(Pond pond)
		{
			int num7 = DaysUntil(today, pond.EstimatedHarvestDate);
			return num7 >= 0 && num7 <= options.HarvestSoonDays;
		});
		double num4 = ((!options.Enabled) ? 0.0 : (options.DailyHistory.LastOrDefault()?.EnergyKwh ?? 0.0));
		double val = num4 / Math.Max(1.0, options.ExpectedOperatingHoursPerDay);
		double peakPowerTodayKw = Math.Max(num3, val) * options.PeakPowerFactor;
		int num5 = source.Count((SensorDefinition x) => x.IsEnabled);
		int num6 = Math.Max(0, num - num2);
		DashboardSnapshot dashboardSnapshot = new DashboardSnapshot();
		dashboardSnapshot.GeneratedAtLocal = nowLocal;
		dashboardSnapshot.IsSimulation = options.Enabled;
		dashboardSnapshot.DataSourceLabel = (options.Enabled ? options.DataSourceLabel : "Dữ liệu runtime và nghiệp vụ thực");
		dashboardSnapshot.RefreshSeconds = options.RefreshSeconds;
		dashboardSnapshot.HealthStableMinimum = _behavior.CurrentValue.HealthScore.StableMinimum;
		dashboardSnapshot.HealthMonitorMinimum = _behavior.CurrentValue.HealthScore.MonitorMinimum;
		dashboardSnapshot.TotalPonds = ponds.Count;
		dashboardSnapshot.TotalAreaM2 = ponds.Sum((Pond x) => x.Area);
		dashboardSnapshot.ActiveCycles = list2.Count;
		dashboardSnapshot.HarvestSoonCount = harvestSoonCount;
		dashboardSnapshot.MedianCultureAgeDays = Median(list5);
		dashboardSnapshot.MinimumCultureAgeDays = ((list5.Count != 0) ? list5.Min() : 0);
		dashboardSnapshot.MaximumCultureAgeDays = ((list5.Count != 0) ? list5.Max() : 0);
		dashboardSnapshot.EstimatedBiomassKg = list4.Sum((DashboardPondSummary x) => x.EstimatedBiomassKg);
		dashboardSnapshot.BiomassChangeKg = (options.Enabled ? list4.Sum((DashboardPondSummary x) => profiles.GetValueOrDefault(x.PondId)?.BiomassDailyGrowthKg ?? 0.0) : list2.Sum((ProductionCycle x) => x.EstimatedDailyBiomassGrowthKg));
		dashboardSnapshot.HasBiomassGrowthData = options.Enabled || list2.Any((ProductionCycle x) => x.GrowthSampleCount >= 2);
		dashboardSnapshot.LatestBiomassSampleAtLocal = options.Enabled && options.Ponds.Count > 0
			? options.Ponds.Max(x => x.LastSampleAtUtc).ToLocalTime()
			: list2.Select(x => x.LatestSampleAtUtc?.ToLocalTime()).Max();
		dashboardSnapshot.BiomassConfidence = (options.Enabled ? ResolveOverallConfidence(options.Ponds) : (list2.Any((ProductionCycle x) => x.LatestSampleAtUtc.HasValue) ? "sampled" : "calculated"));
		dashboardSnapshot.RunningDevices = list.Count(IsDeviceRunning);
		dashboardSnapshot.StoppedDevices = list.Count((Device x) => !IsDeviceRunning(x) && IsDeviceConnected(x));
		dashboardSnapshot.FaultDevices = list.Count((Device x) => x.Status.Equals("fault", StringComparison.OrdinalIgnoreCase));
		dashboardSnapshot.OfflineDevices = list.Count((Device x) => !IsDeviceConnected(x));
		dashboardSnapshot.AutoDevices = list.Count((Device x) => x.Mode.Equals("auto", StringComparison.OrdinalIgnoreCase));
		dashboardSnapshot.TotalDevices = list.Count;
		dashboardSnapshot.CurrentPowerKw = num3;
		dashboardSnapshot.PeakPowerTodayKw = peakPowerTodayKw;
		dashboardSnapshot.EnergyTodayKwh = energyTodayKwh;
		dashboardSnapshot.EnergyMonthKwh = ((options.Enabled && options.DailyHistory.Count > 0) ? (options.DailyHistory.Sum((DashboardDailyHistoryOptions x) => x.EnergyKwh) * Math.Max(1.0, (double)nowLocal.Day / (double)options.DailyHistory.Count)) : 0.0);
		dashboardSnapshot.ActiveAlerts = list6.Count;
		dashboardSnapshot.CriticalAlerts = list6.Count((AlertItem x) => x.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
		dashboardSnapshot.WarningAlerts = list6.Count((AlertItem x) => !x.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
		dashboardSnapshot.UnacknowledgedAlerts = list6.Count((AlertItem x) => !x.AcknowledgedAtUtc.HasValue);
		dashboardSnapshot.AverageDo = ((list7.Count == 0) ? 0.0 : list7.Average());
		dashboardSnapshot.MinimumDo = ((list7.Count == 0) ? 0.0 : list7.Min());
		dashboardSnapshot.PondsBelowDoWarning = list7.Count((double x) => doThreshold.WarningLow.HasValue && x <= doThreshold.WarningLow.Value);
		dashboardSnapshot.ValidDoPonds = list7.Count;
		dashboardSnapshot.FeedTodayKg = feedTodayKg;
		dashboardSnapshot.FeedPlanTodayKg = feedPlanTodayKg;
		dashboardSnapshot.FeedCycleTotalKg = list4.Sum((DashboardPondSummary x) => x.FeedCycleTotalKg);
		dashboardSnapshot.OnlineSensors = num;
		dashboardSnapshot.OfflineSensors = Math.Max(0, num5 - num);
		dashboardSnapshot.InvalidSensors = num2;
		dashboardSnapshot.TotalSensors = num5;
		dashboardSnapshot.CalibrationOverdueSensors = calibrationOverdueSensors;
		dashboardSnapshot.DataCompletenessPercent = ((num5 == 0) ? 0.0 : Math.Round((double)num6 * 100.0 / (double)num5, 1));
		dashboardSnapshot.TasksToday = list3.Count;
		dashboardSnapshot.CompletedTasks = list3.Count((DashboardTaskItem x) => x.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
		dashboardSnapshot.OverdueTasks = list3.Count((DashboardTaskItem x) => x.IsOverdue);
		dashboardSnapshot.Weather = (options.Enabled ? BuildWeather(options, nowLocal) : UnavailableWeather());
		dashboardSnapshot.Infrastructure = (options.Enabled ? BuildInfrastructure(options) : BuildRuntimeInfrastructure());
		dashboardSnapshot.Ponds = list4;
		dashboardSnapshot.StageDistribution = BuildStageDistribution(list4, options);
		dashboardSnapshot.SpeciesDistribution = BuildSpeciesDistribution(list4);
		dashboardSnapshot.BiomassHistory = (options.Enabled ? (from x in options.DailyHistory
			orderby x.DayOffset
			select ToSeriesPoint(x, "biomass", today)).ToList() : new List<DashboardSeriesPoint>());
		dashboardSnapshot.FeedHistory = (options.Enabled ? (from x in options.DailyHistory
			orderby x.DayOffset
			select ToSeriesPoint(x, "feed", today)).ToList() : new List<DashboardSeriesPoint>());
		dashboardSnapshot.EnergyHistory = (options.Enabled ? (from x in options.DailyHistory
			orderby x.DayOffset
			select ToSeriesPoint(x, "energy", today)).ToList() : new List<DashboardSeriesPoint>());
		dashboardSnapshot.AlertHistory = (options.Enabled ? (from x in options.DailyHistory
			orderby x.DayOffset
			select ToSeriesPoint(x, "alerts", today)).ToList() : new List<DashboardSeriesPoint>());
		dashboardSnapshot.SensorAvailabilityHistory = (options.Enabled ? (from x in options.DailyHistory
			orderby x.DayOffset
			select ToSeriesPoint(x, "availability", today)).ToList() : new List<DashboardSeriesPoint>());
		dashboardSnapshot.WaterQualityHeatmap = list4.Select(BuildHeatmapRow).ToList();
		dashboardSnapshot.TodayTasks = (from x in list3
			orderby (!x.IsOverdue) ? ((!(x.Status == "completed")) ? 1 : 2) : 0, x.DueAtLocal
			select x).ToList();
		return dashboardSnapshot;
	}

	public string GetSectionTitle(string section)
	{
		return NormalizeSection(section) switch
		{
			"growth" => "Biomass và tăng trưởng",
			"water-quality" => "Chất lượng nước",
			"feed" => "Thức ăn",
			"equipment" => "Thiết bị và năng lượng",
			"risks" => "Cảnh báo và rủi ro",
			_ => "Ao nuôi và vụ nuôi",
		};
	}

	public string GetSectionDescription(string section)
	{
		return NormalizeSection(section) switch
		{
			"growth" => "Theo dõi sinh khối ước tính, tốc độ tăng trưởng, trọng lượng mẫu và FCR theo từng ao.",
			"water-quality" => "So sánh trạng thái DO, pH, nhiệt độ, độ mặn, NH₃ và mực nước trên toàn trại.",
			"feed" => "Đối chiếu kế hoạch, thực tế, tổng lượng thức ăn và hiệu quả sử dụng theo ao.",
			"equipment" => "Phân tích trạng thái thiết bị, chế độ AUTO/MANUAL, công suất và điện năng tiêu thụ.",
			"risks" => "Tập trung cảnh báo đang hoạt động, chất lượng dữ liệu và sức khỏe hạ tầng kết nối.",
			_ => "Phân loại ao theo đối tượng nuôi, trạng thái vận hành, tuổi vụ và kế hoạch thu hoạch.",
		};
	}

	public string NormalizeSection(string? section)
	{
		string text = section?.Trim().ToLowerInvariant() ?? "ponds";
		bool flag;
		switch (text)
		{
		case "ponds":
		case "growth":
		case "water-quality":
		case "feed":
		case "equipment":
		case "risks":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "ponds";
		}
		return text;
	}

	public string MetricStatus(string metric, double value, bool isOnline = true, string quality = "good")
	{
		bool flag = !isOnline;
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3;
			switch (quality)
			{
			case "no-data":
			case "invalid":
			case "suspect":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			flag2 = flag3;
		}
		if (flag2)
		{
			return "no-data";
		}
		MetricThresholdOptions threshold = GetThreshold(metric);
		if ((threshold.CriticalLow.HasValue && value <= threshold.CriticalLow.Value) || (threshold.CriticalHigh.HasValue && value >= threshold.CriticalHigh.Value))
		{
			return "critical";
		}
		if ((threshold.WarningLow.HasValue && value <= threshold.WarningLow.Value) || (threshold.WarningHigh.HasValue && value >= threshold.WarningHigh.Value))
		{
			return "warning";
		}
		return "normal";
	}

	private DashboardPondSummary BuildPondSummary(Pond pond, DashboardPondSimulationOptions? profile, DateTime nowLocal)
	{
		TelemetryData telemetry = _runtime.GetTelemetry(pond.Id);
		List<SensorDefinition> list = _runtime.Sensors.Where((SensorDefinition x) => x.PondId == pond.Id && x.IsEnabled).ToList();
		List<Device> list2 = _runtime.Devices.Where((Device x) => x.PondId == pond.Id).ToList();
		int activeAlerts = _runtime.Alerts.Count((AlertItem x) => x.PondId == pond.Id && !x.IsResolved);
		bool enabled = _simulation.CurrentValue.Enabled;
		ProductionCycle? productionCycle = (from x in _runtime.ProductionCycles
			where x.PondId == pond.Id && x.IsOpen
			orderby x.StockingDate descending
			select x).FirstOrDefault();
		double num = ((!enabled) ? (productionCycle?.EstimatedBiomassKg ?? 0.0) : (profile?.BiomassAtReferenceKg ?? Math.Max(100.0, pond.Area * pond.StockingDensity * _simulation.CurrentValue.DefaultBiomassDensityFactor)));
		double totalDays = (DateTime.UtcNow.Date - _simulation.CurrentValue.ReferenceDateUtc.Date).TotalDays;
		double num2 = DynamicFactor(pond.Id, nowLocal, _simulation.CurrentValue.DynamicVariationPercent);
		double num3 = (enabled ? (Math.Max(0.0, num + (profile?.BiomassDailyGrowthKg ?? (num * (_simulation.CurrentValue.DefaultDailyBiomassGrowthPercent / 100.0))) * totalDays) * num2) : num);
		List<FeedLog> source = _runtime.FeedLogs.Where((FeedLog x) => x.PondId == pond.Id).ToList();
		double feedActualTodayKg = (enabled ? Math.Max(0.0, (profile?.FeedActualTodayKg ?? (num3 * (_simulation.CurrentValue.DefaultFeedRatePercentOfBiomass / 100.0))) * DynamicFactor("feed:" + pond.Id, nowLocal, _simulation.CurrentValue.DynamicVariationPercent)) : source.Where((FeedLog x) => x.Timestamp.ToLocalTime().Date == nowLocal.Date).Sum((FeedLog x) => x.AmountKg));
		int num4 = list.Count((SensorDefinition x) => !x.SourceType.Contains("simulation", StringComparison.OrdinalIgnoreCase));
		int simulatedSensors = list.Count - num4;
		int num5 = Math.Max(0, todayDays(pond.StockingDate));
		int num6 = CalculateHealthScore(telemetry, pond.Id);
		DashboardPondSummary dashboardPondSummary = new DashboardPondSummary();
		dashboardPondSummary.PondId = pond.Id;
		dashboardPondSummary.Name = pond.Name;
		dashboardPondSummary.Species = pond.Species;
		dashboardPondSummary.Status = pond.Status;
		dashboardPondSummary.AreaM2 = pond.Area;
		dashboardPondSummary.StockingDate = pond.StockingDate;
		dashboardPondSummary.EstimatedHarvestDate = pond.EstimatedHarvestDate;
		dashboardPondSummary.CultureAgeDays = num5;
		dashboardPondSummary.StockingDensity = pond.StockingDensity;
		dashboardPondSummary.EstimatedBiomassKg = num3;
		dashboardPondSummary.SurvivalRatePercent = ((!enabled) ? (productionCycle?.EstimatedSurvivalRatePercent ?? 0.0) : (profile?.SurvivalRatePercent ?? _simulation.CurrentValue.DefaultSurvivalRatePercent));
		dashboardPondSummary.AverageWeightGram = ((!enabled) ? (productionCycle?.LatestAverageWeightGrams ?? 0.0) : (profile?.AverageWeightGram ?? Math.Max(_simulation.CurrentValue.MinimumAverageWeightGram, (double)num5 * _simulation.CurrentValue.DefaultAverageWeightPerDayGram)));
		dashboardPondSummary.FeedPlanTodayKg = ((!enabled) ? 0.0 : (profile?.FeedPlanTodayKg ?? (num3 * (_simulation.CurrentValue.DefaultFeedRatePercentOfBiomass / 100.0))));
		dashboardPondSummary.FeedActualTodayKg = feedActualTodayKg;
		dashboardPondSummary.FeedCycleTotalKg = ((!enabled) ? (productionCycle?.TotalFeedKg ?? source.Sum((FeedLog x) => x.AmountKg)) : (profile?.FeedCycleTotalKg ?? (num3 * _simulation.CurrentValue.DefaultCycleFeedMultiplier)));
		dashboardPondSummary.Fcr = ((!enabled) ? (productionCycle?.FeedConversionRatio ?? 0.0) : (profile?.Fcr ?? _simulation.CurrentValue.DefaultFcr));
		dashboardPondSummary.HealthScore = num6;
		dashboardPondSummary.HealthStatus = ResolveHealthStatus(num6);
		dashboardPondSummary.ActiveAlerts = activeAlerts;
		dashboardPondSummary.RunningDevices = list2.Count(IsDeviceRunning);
		dashboardPondSummary.TotalDevices = list2.Count;
		dashboardPondSummary.OnlineSensors = list.Count((SensorDefinition x) => x.Status.Equals("online", StringComparison.OrdinalIgnoreCase));
		dashboardPondSummary.TotalSensors = list.Count;
		dashboardPondSummary.RealSensors = num4;
		dashboardPondSummary.SimulatedSensors = simulatedSensors;
		dashboardPondSummary.DataQuality = telemetry.Quality;
		dashboardPondSummary.DataSourceSummary = BuildSourceSummary(num4, simulatedSensors);
		dashboardPondSummary.LastUpdateLocal = telemetry.LastSeenAtUtc.ToLocalTime();
		dashboardPondSummary.Telemetry = telemetry;
		return dashboardPondSummary;
		int todayDays(DateOnly date)
		{
			return DateOnly.FromDateTime(nowLocal).DayNumber - date.DayNumber;
		}
	}

	private DashboardHeatmapRow BuildHeatmapRow(DashboardPondSummary pond)
	{
		DashboardHeatmapRow dashboardHeatmapRow = new DashboardHeatmapRow
		{
			PondId = pond.PondId,
			PondName = pond.Name
		};
		string[] heatmapMetrics = HeatmapMetrics;
		foreach (string metric in heatmapMetrics)
		{
			MetricThresholdOptions threshold = GetThreshold(metric);
			double value = MetricValue(pond.Telemetry, metric);
			dashboardHeatmapRow.Cells.Add(new DashboardHeatmapCell
			{
				Metric = metric,
				Label = threshold.DisplayName,
				Value = value,
				Unit = threshold.Unit,
				Status = MetricStatus(metric, value, pond.Telemetry.IsOnline, pond.Telemetry.Quality),
				FormattedValue = value.ToString($"N{threshold.DecimalPlaces}", CultureInfo.GetCultureInfo("vi-VN"))
			});
		}
		return dashboardHeatmapRow;
	}

	private DashboardWeatherSnapshot BuildWeather(DashboardSimulationOptions options, DateTime nowLocal)
	{
		DashboardWeatherSimulationOptions weather = options.Weather;
		double num = Math.Sin((nowLocal.TimeOfDay.TotalHours - 6.0) / 24.0 * Math.PI * 2.0);
		return new DashboardWeatherSnapshot
		{
			StationName = weather.StationName,
			AirTemperatureC = weather.AirTemperatureC + num * options.WeatherTemperatureAmplitudeC,
			HumidityPercent = Math.Clamp(weather.HumidityPercent - num * options.WeatherHumidityAmplitudePercent, 0.0, 100.0),
			RainfallMmToday = Math.Max(0.0, weather.RainfallMmToday + Math.Max(0.0, 0.0 - num) * options.WeatherRainfallAmplitudeMm),
			WindSpeedMs = Math.Max(0.0, weather.WindSpeedMs + Math.Sin((double)nowLocal.Minute / 60.0 * Math.PI * 2.0) * options.WeatherWindAmplitudeMs),
			Condition = weather.Condition,
			RiskLevel = weather.RiskLevel,
			AlertTitle = weather.AlertTitle,
			AlertDetail = weather.AlertDetail,
			UpdatedAtLocal = nowLocal
		};
	}

	private static DashboardWeatherSnapshot UnavailableWeather()
	{
		return new DashboardWeatherSnapshot
		{
			StationName = "Chưa cấu hình trạm thời tiết",
			Condition = "Không có dữ liệu",
			RiskLevel = "no-data",
			AlertTitle = "Weather simulation đã tắt",
			AlertDetail = "Kết nối trạm thời tiết thật trước khi sử dụng dữ liệu này để ra quyết định."
		};
	}

	private List<DashboardInfrastructureStatus> BuildRuntimeInfrastructure()
	{
		return _runtime.DataSources.Select((DataSourceHealth x) => new DashboardInfrastructureStatus
		{
			Id = x.Id,
			Name = x.SourceName,
			Type = "telemetry-source",
			Status = (x.IsConnected ? "online" : (x.IsEnabled ? "warning" : "disabled")),
			Detail = (x.LastError ?? (x.LastMessageAtUtc.HasValue ? $"Bản tin cuối {x.LastMessageAtUtc.Value.ToLocalTime():dd/MM HH:mm:ss}" : "Chưa nhận dữ liệu"))
		}).ToList();
	}

	private List<DashboardInfrastructureStatus> BuildInfrastructure(DashboardSimulationOptions options)
	{
		List<DashboardInfrastructureStatus> list = options.Infrastructure.Select((DashboardInfrastructureSimulationOptions x) => new DashboardInfrastructureStatus
		{
			Id = x.Id,
			Name = x.Name,
			Type = x.Type,
			Status = x.Status,
			Detail = x.Detail,
			LatencyMs = x.LatencyMs
		}).ToList();
		foreach (DataSourceHealth source in _runtime.DataSources)
		{
			DashboardInfrastructureStatus? dashboardInfrastructureStatus = list.FirstOrDefault((DashboardInfrastructureStatus x) => x.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase));
			if (dashboardInfrastructureStatus == null)
			{
				list.Add(new DashboardInfrastructureStatus
				{
					Id = source.Id,
					Name = source.SourceName,
					Type = "data-source",
					Status = (source.IsConnected ? "online" : (source.IsEnabled ? "offline" : "disabled")),
					Detail = (source.LastError ?? $"{source.MessagesReceived:N0} bản tin đã nhận"),
					LatencyMs = 0
				});
			}
			else
			{
				dashboardInfrastructureStatus.Status = (source.IsConnected ? "online" : (source.IsEnabled ? "offline" : dashboardInfrastructureStatus.Status));
				if (!string.IsNullOrWhiteSpace(source.LastError))
				{
					dashboardInfrastructureStatus.Detail = source.LastError;
				}
			}
		}
		return list;
	}

	private static DashboardTaskItem MapTask(DashboardTaskSimulationOptions task, IReadOnlyList<Pond> ponds, DateTime nowLocal)
	{
		DateTime dateTime = ResolveTaskDueLocal(task, nowLocal);
		return new DashboardTaskItem
		{
			Id = task.Id,
			PondId = task.PondId,
			PondName = (ponds.FirstOrDefault((Pond x) => x.Id == task.PondId)?.Name ?? "Toàn trại"),
			Title = task.Title,
			Category = task.Category,
			Status = task.Status,
			DueAtLocal = dateTime,
			AssignedTo = task.AssignedTo,
			IsOverdue = (!task.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) && dateTime < nowLocal)
		};
	}

	private static DateTime ResolveTaskDueLocal(DashboardTaskSimulationOptions task, DateTime nowLocal)
	{
		if (task.DueAtUtc.HasValue)
		{
			return task.DueAtUtc.Value.ToLocalTime();
		}
		DateTime dateTime = nowLocal.Date.AddDays(task.DayOffset);
		if (!TimeOnly.TryParseExact(task.TimeLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return dateTime.AddHours(8.0);
		}
		return dateTime.Add(result.ToTimeSpan());
	}

	private List<DashboardCategoryValue> BuildStageDistribution(IReadOnlyList<DashboardPondSummary> ponds, DashboardSimulationOptions options)
	{
		Dictionary<string, (string, int)> dictionary = new Dictionary<string, (string, int)>
		{
			["preparing"] = ("Chuẩn bị ao", 0),
			["new"] = ("Mới thả", 0),
			["growth"] = ("Sinh trưởng", 0),
			["late-growth"] = ("Tăng trưởng cuối", 0),
			["harvest-soon"] = ("Sắp thu hoạch", 0),
			["inactive"] = ("Dừng/Bảo trì", 0)
		};
		foreach (DashboardPondSummary pond in ponds)
		{
			string key;
			if (!pond.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
			{
				key = (pond.Status.Equals("preparing", StringComparison.OrdinalIgnoreCase) ? "preparing" : "inactive");
			}
			else
			{
				int num = pond.EstimatedHarvestDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
				key = ((num <= options.HarvestSoonDays) ? "harvest-soon" : ((pond.CultureAgeDays <= options.NewCycleDays) ? "new" : ((pond.CultureAgeDays <= options.EarlyGrowthDays) ? "growth" : "late-growth")));
			}
			(string, int) tuple = dictionary[key];
			dictionary[key] = (tuple.Item1, tuple.Item2 + 1);
		}
		return dictionary.Select<KeyValuePair<string, (string, int)>, DashboardCategoryValue>((KeyValuePair<string, (string Label, int Count)> x) => new DashboardCategoryValue
		{
			Key = x.Key,
			Label = x.Value.Label,
			Value = x.Value.Count,
			Detail = $"{x.Value.Count} ao"
		}).ToList();
	}

	private static List<DashboardCategoryValue> BuildSpeciesDistribution(IReadOnlyList<DashboardPondSummary> ponds)
	{
		return (from x in ponds
			group x by x.Species into x
			orderby x.Count() descending
			select new DashboardCategoryValue
			{
				Key = x.Key,
				Label = x.Key,
				Value = x.Count(),
				Detail = $"{x.Sum((DashboardPondSummary p) => p.EstimatedBiomassKg) / 1000.0:N1} tấn Biomass"
			}).ToList();
	}

	private static DashboardSeriesPoint ToSeriesPoint(DashboardDailyHistoryOptions item, string kind, DateOnly today)
	{
		string label = today.AddDays(item.DayOffset).ToString("dd/MM");
		return kind switch
		{
			"biomass" => new DashboardSeriesPoint
			{
				Label = label,
				Value = item.BiomassKg
			},
			"feed" => new DashboardSeriesPoint
			{
				Label = label,
				Value = item.FeedKg
			},
			"energy" => new DashboardSeriesPoint
			{
				Label = label,
				Value = item.EnergyKwh
			},
			"alerts" => new DashboardSeriesPoint
			{
				Label = label,
				Value = item.WarningAlerts,
				SecondaryValue = item.CriticalAlerts
			},
			_ => new DashboardSeriesPoint
			{
				Label = label,
				Value = item.SensorAvailabilityPercent
			},
		};
	}

	private int CalculateHealthScore(TelemetryData telemetry, string pondId)
	{
		HealthScoreOptions healthScore = _behavior.CurrentValue.HealthScore;
		bool flag = !telemetry.IsOnline;
		bool flag2 = flag;
		if (!flag2)
		{
			string quality = telemetry.Quality;
			bool flag3 = ((quality == "invalid" || quality == "no-data") ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			return healthScore.OfflineScore;
		}
		int num = HeatmapMetrics.Count(delegate(string metric)
		{
			string text = MetricStatus(metric, MetricValue(telemetry, metric), telemetry.IsOnline, telemetry.Quality);
			return (text == "warning" || text == "critical") ? true : false;
		});
		int num2 = _runtime.Alerts.Count((AlertItem x) => x.PondId == pondId && !x.IsResolved && x.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase));
		return Math.Clamp(healthScore.InitialScore - num * healthScore.MetricOutOfRangePenalty - num2 * healthScore.CriticalAlertPenalty, 0, 100);
	}

	private string ResolveHealthStatus(int score)
	{
		HealthScoreOptions healthScore = _behavior.CurrentValue.HealthScore;
		if (score < healthScore.StableMinimum)
		{
			if (score < healthScore.MonitorMinimum)
			{
				return "critical";
			}
			return "warning";
		}
		return "good";
	}

	private MetricThresholdOptions GetThreshold(string metric)
	{
		if (_thresholds.CurrentValue.Metrics.TryGetValue(metric, out var value) && value is not null)
		{
			return value;
		}
		throw new KeyNotFoundException("Metric '" + metric + "' chưa được cấu hình trong sensor-thresholds.json.");
	}

	private static double MetricValue(TelemetryData telemetry, string metric)
	{
		return metric switch
		{
			"temperature" => telemetry.Temperature,
			"ph" => telemetry.Ph,
			"salinity" => telemetry.Salinity,
			"ammonia" => telemetry.Ammonia,
			"waterLevel" => telemetry.WaterLevel,
			_ => telemetry.DissolvedOxygen,
		};
	}

	private static bool IsDeviceRunning(Device device)
	{
		if (!device.Status.Equals("on", StringComparison.OrdinalIgnoreCase))
		{
			return device.Status.Equals("running", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsDeviceConnected(Device device)
	{
		if (!device.ConnectionStatus.Equals("offline", StringComparison.OrdinalIgnoreCase))
		{
			return !device.Status.Equals("offline", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static int DaysUntil(DateOnly today, DateOnly target)
	{
		return target.DayNumber - today.DayNumber;
	}

	private static int Median(IReadOnlyList<int> sorted)
	{
		if (sorted.Count != 0)
		{
			if (sorted.Count % 2 != 1)
			{
				return (int)Math.Round((double)(sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0);
			}
			return sorted[sorted.Count / 2];
		}
		return 0;
	}

	private static string BuildSourceSummary(int realSensors, int simulatedSensors)
	{
		if (realSensors > 0 && simulatedSensors > 0)
		{
			return $"{realSensors} thực · {simulatedSensors} mô phỏng";
		}
		if (realSensors <= 0)
		{
			return $"{simulatedSensors} cảm biến mô phỏng";
		}
		return $"{realSensors} cảm biến thực";
	}

	private static string ResolveOverallConfidence(IEnumerable<DashboardPondSimulationOptions> ponds)
	{
		List<string> list = ponds.Select((DashboardPondSimulationOptions x) => x.BiomassConfidence.ToLowerInvariant()).ToList();
		if (list.Count == 0)
		{
			return "unknown";
		}
		if (list.Contains("low"))
		{
			return "low";
		}
		if (list.Contains("medium"))
		{
			return "medium";
		}
		return "high";
	}

	private static double DynamicFactor(string key, DateTime nowLocal, double variationPercent)
	{
		int num = key.Aggregate(17, (int current, char c) => current * 31 + c);
		double num2 = (double)Math.Abs(num % 360) * Math.PI / 180.0;
		double num3 = Math.Sin(nowLocal.TimeOfDay.TotalMinutes / 17.0 + num2);
		return 1.0 + num3 * variationPercent / 100.0;
	}

	private static double EstimateEnergyToday(DashboardSimulationOptions options, double currentPower, DateTime nowLocal)
	{
		double num = options.DailyHistory.LastOrDefault()?.EnergyKwh ?? 0.0;
		double num2 = Math.Clamp(nowLocal.TimeOfDay.TotalHours / 24.0, 0.03, 1.0);
		double val = currentPower * Math.Max(1.0, nowLocal.TimeOfDay.TotalHours) * options.EnergyLoadFactor;
		return Math.Max(val, num * num2);
	}
}
