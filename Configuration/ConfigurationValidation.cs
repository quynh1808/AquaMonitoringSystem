using Microsoft.Extensions.Options;
using AquacultureMonitoring.Sensors;

namespace AquacultureMonitoring.Configuration;

public sealed class OperationalModeOptionsValidator : IValidateOptions<OperationalModeOptions>
{
    private static readonly string[] Modes = ["Simulation", "Shadow", "Monitoring", "Production"];
    public ValidateOptionsResult Validate(string? name, OperationalModeOptions options) =>
        Modes.Contains(options.Mode, StringComparer.OrdinalIgnoreCase)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"OperationalMode:Mode phải là một trong: {string.Join(", ", Modes)}.");
}

public sealed class AlertNotificationOptionsValidator : IValidateOptions<AlertNotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, AlertNotificationOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        if (!Uri.TryCreate(options.WebhookUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return ValidateOptionsResult.Fail("AlertNotifications:WebhookUrl phải là HTTP/HTTPS URL hợp lệ.");
        return options.TimeoutSeconds is >= 1 and <= 60
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("AlertNotifications:TimeoutSeconds phải trong khoảng 1–60.");
    }
}

public sealed class MongoDbOptionsValidator : IValidateOptions<MongoDbOptions>
{
    public ValidateOptionsResult Validate(string? name, MongoDbOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionString)) errors.Add("MongoDb:ConnectionString không được để trống.");
        if (string.IsNullOrWhiteSpace(options.DatabaseName)) errors.Add("MongoDb:DatabaseName không được để trống.");
        if (string.IsNullOrWhiteSpace(options.Username) != string.IsNullOrWhiteSpace(options.Password))
            errors.Add("MongoDb:Username và MongoDb:Password phải được cấu hình cùng nhau.");
        if (options.ServerSelectionTimeoutSeconds <= 0) errors.Add("MongoDb:ServerSelectionTimeoutSeconds phải lớn hơn 0.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class SensorThresholdCatalogValidator : IValidateOptions<SensorThresholdCatalog>
{
    private static readonly string[] RequiredMetrics =
        ["temperature", "ph", "dissolvedOxygen", "salinity", "ammonia", "waterLevel"];

    public ValidateOptionsResult Validate(string? name, SensorThresholdCatalog options)
    {
        var errors = new List<string>();
        foreach (var metric in RequiredMetrics)
        {
            if (!options.Metrics.TryGetValue(metric, out var threshold))
            {
                errors.Add($"SensorThresholds:Metrics thiếu '{metric}'.");
                continue;
            }

            if (threshold.ValidMin >= threshold.ValidMax)
                errors.Add($"{metric}: ValidMin phải nhỏ hơn ValidMax.");
            if (threshold.WarningLow is not null && threshold.WarningLow < threshold.ValidMin)
                errors.Add($"{metric}: WarningLow nằm ngoài miền hợp lệ.");
            if (threshold.WarningHigh is not null && threshold.WarningHigh > threshold.ValidMax)
                errors.Add($"{metric}: WarningHigh nằm ngoài miền hợp lệ.");
            if (threshold.CriticalLow is not null && threshold.WarningLow is not null && threshold.CriticalLow > threshold.WarningLow)
                errors.Add($"{metric}: CriticalLow phải nhỏ hơn hoặc bằng WarningLow.");
            if (threshold.CriticalHigh is not null && threshold.WarningHigh is not null && threshold.CriticalHigh < threshold.WarningHigh)
                errors.Add($"{metric}: CriticalHigh phải lớn hơn hoặc bằng WarningHigh.");
            if (threshold.DelaySeconds < 0 || threshold.CooldownSeconds < 0 || threshold.Hysteresis < 0)
                errors.Add($"{metric}: Delay/Cooldown/Hysteresis không được âm.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class SimulationProfileValidator : IValidateOptions<SimulationProfileOptions>
{
    private static readonly string[] RequiredMetrics =
        ["temperature", "ph", "dissolvedOxygen", "salinity", "ammonia", "waterLevel"];

    public ValidateOptionsResult Validate(string? name, SimulationProfileOptions options)
    {
        var errors = new List<string>();
        if (options.IntervalMilliseconds < 250) errors.Add("SimulationProfile:IntervalMilliseconds tối thiểu là 250 ms.");
        foreach (var metric in RequiredMetrics)
            if (!options.Metrics.ContainsKey(metric)) errors.Add($"SimulationProfile:Metrics thiếu '{metric}'.");
        foreach (var (metric, profile) in options.Metrics)
        {
            if (profile.MinValue >= profile.MaxValue) errors.Add($"SimulationProfile:{metric}: MinValue phải nhỏ hơn MaxValue.");
            if (profile.BaseValue < profile.MinValue || profile.BaseValue > profile.MaxValue)
                errors.Add($"SimulationProfile:{metric}: BaseValue phải nằm trong MinValue–MaxValue.");
            if (profile.MaxStep < 0 || profile.DailyAmplitude < 0 || profile.AnomalyAmplitude < 0)
                errors.Add($"SimulationProfile:{metric}: biên độ không được âm.");
            if (profile.TargetCorrectionFactor is < 0 or > 1)
                errors.Add($"SimulationProfile:{metric}: TargetCorrectionFactor phải trong khoảng 0–1.");
            if (profile.AnomalyProbability is < 0 or > 1)
                errors.Add($"SimulationProfile:{metric}: AnomalyProbability phải trong khoảng 0–1.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class SensorConnectionOptionsValidator : IValidateOptions<SensorConnectionOptions>
{
    private static readonly HashSet<string> SupportedSources = new(StringComparer.OrdinalIgnoreCase)
        { "simulation", "mqtt", "modbus-tcp", "modbus-rtu" };
    private static readonly HashSet<string> SupportedDataTypes = new(StringComparer.OrdinalIgnoreCase)
        { "UInt16", "Int16", "UInt32", "Int32", "Float32", "Float64" };
    private static readonly HashSet<string> SupportedMetrics = new(StringComparer.OrdinalIgnoreCase)
        { "temperature", "ph", "dissolvedOxygen", "salinity", "ammonia", "waterLevel" };
    private static readonly Dictionary<string, ushort> RequiredRegisterCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UInt16"] = 1, ["Int16"] = 1, ["UInt32"] = 2, ["Int32"] = 2,
        ["Float32"] = 2, ["Float64"] = 4
    };

    public ValidateOptionsResult Validate(string? name, SensorConnectionOptions options)
    {
        var errors = new List<string>();
        foreach (var source in options.ActiveSources.Where(x => !SupportedSources.Contains(x)))
            errors.Add($"SensorConnections: ActiveSources chứa nguồn không hỗ trợ '{source}'.");

        if (options.Mqtt.Enabled && (string.IsNullOrWhiteSpace(options.Mqtt.Host) || options.Mqtt.Port is <= 0 or > 65535 || string.IsNullOrWhiteSpace(options.Mqtt.TopicFilter)))
            errors.Add("SensorConnections:Mqtt cần Host, Port và TopicFilter hợp lệ.");
        if (options.ActiveSources.Contains("mqtt", StringComparer.OrdinalIgnoreCase) && !options.Mqtt.Enabled)
            errors.Add("ActiveSources có 'mqtt' nhưng SensorConnections:Mqtt:Enabled đang false.");

        var connectionIds = options.ModbusTcp.Select(x => x.Id).Concat(options.ModbusRtu.Select(x => x.Id)).ToList();
        if (connectionIds.Any(string.IsNullOrWhiteSpace) || connectionIds.Count != connectionIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            errors.Add("Mỗi kết nối Modbus phải có Id duy nhất và không được để trống.");

        foreach (var tcp in options.ModbusTcp)
        {
            if (tcp.Enabled && (string.IsNullOrWhiteSpace(tcp.Host) || tcp.Port is <= 0 or > 65535))
                errors.Add($"Modbus TCP '{tcp.Id}' cần Host và Port hợp lệ.");
            if (tcp.PollIntervalMilliseconds < 100 || tcp.TimeoutMilliseconds <= 0)
                errors.Add($"Modbus TCP '{tcp.Id}' có PollInterval/Timeout không hợp lệ.");
        }
        foreach (var rtu in options.ModbusRtu)
        {
            if (rtu.Enabled && string.IsNullOrWhiteSpace(rtu.PortName)) errors.Add($"Modbus RTU '{rtu.Id}' thiếu PortName.");
            if (!Enum.TryParse<System.IO.Ports.Parity>(rtu.Parity, true, out _)) errors.Add($"Modbus RTU '{rtu.Id}' có Parity không hợp lệ.");
            if (!Enum.TryParse<System.IO.Ports.StopBits>(rtu.StopBits, true, out _)) errors.Add($"Modbus RTU '{rtu.Id}' có StopBits không hợp lệ.");
            if (rtu.BaudRate <= 0 || rtu.DataBits is < 5 or > 8 || rtu.PollIntervalMilliseconds < 100 || rtu.TimeoutMilliseconds <= 0)
                errors.Add($"Modbus RTU '{rtu.Id}' có thông số serial/polling không hợp lệ.");
        }

        var mappings = options.ModbusTcp.SelectMany(x => x.Mappings)
            .Concat(options.ModbusRtu.SelectMany(x => x.Mappings)).ToList();
        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.SensorId) || string.IsNullOrWhiteSpace(mapping.PondId) || string.IsNullOrWhiteSpace(mapping.Metric))
                errors.Add("Mỗi Modbus mapping phải có SensorId, PondId và Metric.");
            if (!SupportedMetrics.Contains(mapping.Metric))
                errors.Add($"{mapping.SensorId}: Metric '{mapping.Metric}' không được hỗ trợ.");
            if (!SupportedDataTypes.Contains(mapping.DataType))
                errors.Add($"{mapping.SensorId}: DataType '{mapping.DataType}' không được hỗ trợ.");
            else if (RequiredRegisterCounts[mapping.DataType] != mapping.RegisterCount)
                errors.Add($"{mapping.SensorId}: {mapping.DataType} cần RegisterCount={RequiredRegisterCounts[mapping.DataType]}.");
            else if (!ModbusCodec.IsValidByteOrder(mapping.DataType, mapping.ByteOrder))
                errors.Add($"{mapping.SensorId}: ByteOrder '{mapping.ByteOrder}' không hợp lệ cho {mapping.DataType}.");
            if (mapping.Scale == 0) errors.Add($"{mapping.SensorId}: Scale không được bằng 0.");
        }
        if (mappings.Select(x => x.SensorId).Count() != mappings.Select(x => x.SensorId).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            errors.Add("SensorId trong các Modbus mapping phải là duy nhất.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

public sealed class SystemBehaviorOptionsValidator : IValidateOptions<SystemBehaviorOptions>
{
    public ValidateOptionsResult Validate(string? name, SystemBehaviorOptions options)
    {
        var errors = new List<string>();
        if (options.SensorOfflineAfterSeconds <= 0) errors.Add("SensorOfflineAfterSeconds phải lớn hơn 0.");
        if (options.ReadingRetentionDays <= 0) errors.Add("ReadingRetentionDays phải lớn hơn 0.");
        if (options.RuntimeReadingLimit <= 0) errors.Add("RuntimeReadingLimit phải lớn hơn 0.");
        if (options.DefaultCalibrationIntervalDays <= 0) errors.Add("DefaultCalibrationIntervalDays phải lớn hơn 0.");
        if (options.CalibrationReminderDaysBefore < 0) errors.Add("CalibrationReminderDaysBefore không được âm.");
        if (options.MaintenanceCheckIntervalMinutes <= 0) errors.Add("MaintenanceCheckIntervalMinutes phải lớn hơn 0.");
        if (options.MaxFutureClockSkewSeconds < 0 || options.MaxTelemetryAgeMinutes <= 0)
            errors.Add("Giới hạn timestamp telemetry không hợp lệ.");
        if (options.HealthScore.MonitorMinimum >= options.HealthScore.StableMinimum)
            errors.Add("HealthScore: MonitorMinimum phải nhỏ hơn StableMinimum.");
        foreach (var policy in options.AutomationPolicies)
        {
            if (string.IsNullOrWhiteSpace(policy.Metric) || policy.DeviceTypes.Count == 0)
                errors.Add("Mỗi AutomationPolicy cần Metric và ít nhất một DeviceType.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}


public sealed class DashboardSimulationOptionsValidator : IValidateOptions<DashboardSimulationOptions>
{
    public ValidateOptionsResult Validate(string? name, DashboardSimulationOptions options)
    {
        var errors = new List<string>();
        if (options.RefreshSeconds <= 0) errors.Add("DashboardSimulation:RefreshSeconds phải lớn hơn 0.");
        if (options.DynamicVariationPercent < 0) errors.Add("DashboardSimulation:DynamicVariationPercent không được âm.");
        if (options.HarvestSoonDays < 0 || options.NewCycleDays < 0 || options.EarlyGrowthDays < options.NewCycleDays)
            errors.Add("DashboardSimulation: các mốc giai đoạn nuôi không hợp lệ.");
        if (options.DefaultBiomassDensityFactor <= 0 || options.DefaultDailyBiomassGrowthPercent < 0)
            errors.Add("DashboardSimulation: hệ số Biomass phải hợp lệ.");
        if (options.DefaultFeedRatePercentOfBiomass < 0 || options.DefaultCycleFeedMultiplier < 0 || options.DefaultFcr <= 0)
            errors.Add("DashboardSimulation: tham số thức ăn/FCR phải hợp lệ.");
        if (options.ExpectedOperatingHoursPerDay <= 0 || options.PeakPowerFactor <= 0 || options.EnergyLoadFactor <= 0)
            errors.Add("DashboardSimulation: tham số năng lượng phải lớn hơn 0.");
        if (options.Ponds.Any(x => string.IsNullOrWhiteSpace(x.PondId))) errors.Add("DashboardSimulation:Ponds có PondId trống.");
        if (options.Ponds.Select(x => x.PondId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Ponds.Count)
            errors.Add("DashboardSimulation:Ponds phải có PondId duy nhất.");
        foreach (var pond in options.Ponds)
        {
            if (pond.BiomassAtReferenceKg < 0 || pond.BiomassDailyGrowthKg < 0 || pond.FeedPlanTodayKg < 0 || pond.FeedActualTodayKg < 0)
                errors.Add($"DashboardSimulation:Ponds:{pond.PondId} có giá trị âm không hợp lệ.");
            if (pond.SurvivalRatePercent is < 0 or > 100)
                errors.Add($"DashboardSimulation:Ponds:{pond.PondId}: SurvivalRatePercent phải trong 0–100.");
        }
        foreach (var task in options.Tasks)
        {
            if (!task.DueAtUtc.HasValue && !TimeOnly.TryParseExact(task.TimeLocal, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                errors.Add($"DashboardSimulation:Tasks:{task.Id}: TimeLocal phải có định dạng HH:mm.");
        }
        if (options.DailyHistory.Count == 0) errors.Add("DashboardSimulation:DailyHistory cần ít nhất một điểm.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

/// <summary>Fails startup when actuator safety configuration is ambiguous.</summary>
public sealed class DeviceControlOptionsValidator : IValidateOptions<DeviceControlOptions>
{
    public ValidateOptionsResult Validate(string? name, DeviceControlOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.DefaultTransport))
            errors.Add("DeviceControl:DefaultTransport không được để trống.");
        if (options.CommandTimeoutSeconds is < 1 or > 300)
            errors.Add("DeviceControl:CommandTimeoutSeconds phải trong khoảng 1–300 giây.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

/// <summary>Validates authentication settings before a bootstrap account can be created.</summary>
public sealed class AuthenticationOptionsValidator : IValidateOptions<AuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthenticationOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.BootstrapAdminUsername) || options.BootstrapAdminUsername.Length > 100)
            errors.Add("Authentication:BootstrapAdminUsername phải có từ 1 đến 100 ký tự.");
        if (string.IsNullOrWhiteSpace(options.BootstrapAdminPassword) || options.BootstrapAdminPassword.Length is < 12 or > 256)
            errors.Add("Authentication:BootstrapAdminPassword phải có từ 12 đến 256 ký tự.");
        if (options.PasswordIterations < 100_000) errors.Add("Authentication:PasswordIterations tối thiểu là 100000.");
        if (options.MaxFailedLoginAttempts <= 0 || options.LockoutMinutes <= 0 || options.CookieHours <= 0)
            errors.Add("Authentication: các giá trị lockout/cookie phải lớn hơn 0.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

/// <summary>Validates production-cycle simulation without requiring it to be enabled.</summary>
public sealed class ProductionCycleSimulationOptionsValidator : IValidateOptions<ProductionCycleSimulationOptions>
{
    public ValidateOptionsResult Validate(string? name, ProductionCycleSimulationOptions options)
    {
        var errors = new List<string>();
        if (options.RefreshHours <= 0 || options.SampleEveryDays <= 0)
            errors.Add("ProductionCycleSimulation: RefreshHours và SampleEveryDays phải lớn hơn 0.");
        foreach (var (species, profile) in options.Species)
        {
            if (string.IsNullOrWhiteSpace(species)) errors.Add("ProductionCycleSimulation có tên loài trống.");
            if (profile.InitialWeightGrams <= 0 || profile.DailyGrowthGrams < 0 || profile.DefaultGrowDays <= 0 || profile.DefaultSampleSize <= 0)
                errors.Add($"ProductionCycleSimulation:{species} có tham số không hợp lệ.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
