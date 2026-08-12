using Microsoft.Extensions.Options;
using AquacultureMonitoring.Sensors;

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

public sealed class SensorConnectionOptionsValidator : IValidateOptions<SensorConnectionOptions>
{
    private static readonly HashSet<string> SupportedSources = new(StringComparer.OrdinalIgnoreCase)
        { "mqtt", "modbus-tcp", "modbus-rtu" };
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
        if (string.IsNullOrWhiteSpace(options.Fx5u.Host) || options.Fx5u.Port is <= 0 or > 65535)
            errors.Add("DeviceControl:Fx5u cần Host và Port hợp lệ.");
        if (options.Fx5u.UnitId == 0)
            errors.Add("DeviceControl:Fx5u:UnitId phải lớn hơn 0.");
        if (options.Fx5u.Devices.Count == 0 || options.Fx5u.Devices.Select(x => x.DeviceId)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Fx5u.Devices.Count)
            errors.Add("DeviceControl:Fx5u:Devices phải có DeviceId duy nhất và không được rỗng.");
        foreach (var mapping in options.Fx5u.Devices)
        {
            try
            {
                _ = AquacultureMonitoring.Devices.Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(mapping.CommandDevice);
                _ = AquacultureMonitoring.Devices.Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(mapping.FeedbackDevice);
                _ = AquacultureMonitoring.Devices.Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress(mapping.FaultDevice);
            }
            catch (FormatException ex) { errors.Add($"{mapping.DeviceId}: {ex.Message}"); }
        }
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
