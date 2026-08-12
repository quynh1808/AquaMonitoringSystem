using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Configuration;

public sealed class OperationalModeOptionsValidator : IValidateOptions<OperationalModeOptions>
{
    private static readonly string[] Modes = ["Simulation", "EdgeSimulation", "Production"];
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
