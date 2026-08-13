using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Evaluates bounded MongoDB policies with hysteresis, persistence, minimum ON/OFF time and a
/// starts-per-hour lockout. PLC interlocks remain the final safety authority.
/// </summary>
public sealed class AutomationEngineService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _farm;
    private readonly SystemConfigurationRepository _configuration;
    private readonly DeviceCommandService _commands;

    public AutomationEngineService(FarmRuntimeState runtime, FarmRepository farm,
        SystemConfigurationRepository configuration, DeviceCommandService commands)
    {
        _runtime = runtime; _farm = farm; _configuration = configuration; _commands = commands;
    }

    public async Task ProcessAsync(SensorDefinition sensor, SensorReading reading, CancellationToken ct)
    {
        if (!sensor.IsEnabled || sensor.IsArchived || sensor.IsPendingRemoval || sensor.Status != "online" || reading.Quality != "good") return;
        var policies = (await _configuration.GetAutomationPoliciesAsync(null, ct)).Where(x => x.IsEnabled &&
            x.PondId == sensor.PondId && x.Metric.Equals(sensor.Metric, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(x.SensorId) || x.SensorId == sensor.Id ||
             (x.InputFailureAction == "use-backup" && !sensor.IsPrimary && _runtime.GetSensor(x.SensorId)?.Status != "online"))).ToList();
        foreach (var policy in policies) await EvaluateAsync(policy, reading.Value, ct);
    }

    /// <summary>Applies the configured safe action when a policy input is offline or invalid.</summary>
    public async Task HandleInputFailureAsync(SensorDefinition sensor, string reason, CancellationToken ct)
    {
        var policies = (await _configuration.GetAutomationPoliciesAsync(null, ct)).Where(x => x.IsEnabled &&
            x.PondId == sensor.PondId && x.SensorId == sensor.Id).ToList();
        foreach (var policy in policies)
        {
            var device = _runtime.Devices.FirstOrDefault(x => x.Id == policy.DeviceId);
            if (device is null) continue;
            switch (policy.InputFailureAction)
            {
                case "safe-off":
                case "safe-on":
                    var desired = policy.InputFailureAction == "safe-on" ? "on" : "off";
                    if (device.Status != desired && device.Mode == "auto")
                        await _commands.ExecuteAsync(device.Id, desired, "automation-engine", $"input-failure:{reason}", ct);
                    await SetStateAsync(policy, $"input-unhealthy-{policy.InputFailureAction}", null, ct);
                    break;
                case "switch-to-remote-manual":
                    // Mode is a PLC state, not a dashboard-only flag. Persist an intent and
                    // wait for AutoFeedback to turn off before reporting Remote Manual.
                    var modeCommand = device.Mode == "auto"
                        ? await _commands.SetModeAsync(device.Id, "remote-manual", "automation-engine",
                            $"input-failure:{reason}", ct)
                        : null;
                    await SetStateAsync(policy,
                        modeCommand is null or { Status: "completed" }
                            ? "input-unhealthy-remote-manual"
                            : $"input-unhealthy-mode-{modeCommand.Status}", null, ct);
                    break;
                case "use-backup":
                    await SetStateAsync(policy, "waiting-for-backup", null, ct);
                    break;
                default:
                    await SetStateAsync(policy, "input-unhealthy", null, ct);
                    break;
            }
        }
    }

    private async Task EvaluateAsync(DeviceAutomationPolicy policy, double value, CancellationToken ct)
    {
        var device = _runtime.Devices.FirstOrDefault(x => x.Id == policy.DeviceId);
        if (device is null || !device.IsEnabled || device.IsArchived || device.IsPendingRemoval || device.ConfigurationStatus != "applied") return;
        if (device.IsLocalOverrideActive || device.ReportedMode == "local-manual")
        {
            await SetStateAsync(policy, "suppressed-by-local-override", null, ct); return;
        }
        if (device.Mode != "auto")
        {
            await SetStateAsync(policy, "suppressed-by-remote-manual", null, ct); return;
        }
        if (device.Status == "fault") { await SetStateAsync(policy, "fault", null, ct); return; }

        var now = DateTime.UtcNow;
        policy.RecentStartTimesUtc = policy.RecentStartTimesUtc.Where(x => x >= now.AddHours(-1)).ToList();
        var low = policy.TriggerDirection.Equals("low", StringComparison.OrdinalIgnoreCase);
        var startCondition = low ? value <= policy.StartThreshold : value >= policy.StartThreshold;
        var stopCondition = low ? value >= policy.StopThreshold : value <= policy.StopThreshold;
        var desired = device.Status == "on" ? "off" : "on";
        var condition = desired == "on" ? startCondition : stopCondition;
        if (!condition)
        {
            var stable = device.Status == "on" ? "running" : "idle";
            if (policy.RuntimeState != stable || policy.ConditionSinceUtc.HasValue)
                await SetStateAsync(policy, stable, null, ct);
            return;
        }

        var pendingState = desired == "on" ? "start-pending" : "stop-pending";
        if (policy.RuntimeState != pendingState || !policy.ConditionSinceUtc.HasValue)
        {
            await SetStateAsync(policy, pendingState, now, ct); return;
        }
        var persistence = TimeSpan.FromSeconds(desired == "on" ? policy.StartPersistenceSeconds : policy.StopPersistenceSeconds);
        if (now - policy.ConditionSinceUtc.Value < persistence) return;

        var sinceTransition = now - (policy.LastTransitionAtUtc ?? DateTime.MinValue);
        if (desired == "on" && sinceTransition < TimeSpan.FromSeconds(policy.MinimumOffSeconds))
        { await SetStateAsync(policy, "minimum-off-time", policy.ConditionSinceUtc, ct); return; }
        if (desired == "off" && sinceTransition < TimeSpan.FromSeconds(policy.MinimumOnSeconds))
        { await SetStateAsync(policy, "minimum-on-time", policy.ConditionSinceUtc, ct); return; }
        if (desired == "on" && policy.RecentStartTimesUtc.Count >= policy.MaximumStartsPerHour)
        { await SetStateAsync(policy, "lockout", policy.ConditionSinceUtc, ct); return; }

        policy.RuntimeState = desired == "on" ? "starting" : "stopping";
        await _configuration.UpsertAutomationPolicyAsync(policy, ct);
        var command = await _commands.ExecuteAsync(device.Id, desired, "automation-engine", $"policy:{policy.Id}", ct);
        if (command.Status == "completed")
        {
            policy.RuntimeState = desired == "on" ? "running-min-time" : "idle";
            policy.LastTransitionAtUtc = DateTime.UtcNow; policy.ConditionSinceUtc = null;
            if (desired == "on") policy.RecentStartTimesUtc.Add(DateTime.UtcNow);
        }
        else policy.RuntimeState = command.Status == "blocked-by-local-override" ? "suppressed-by-local-override" : "feedback-mismatch";
        await _configuration.UpsertAutomationPolicyAsync(policy, ct);
        var log = new AutomationLog
        {
            PondId = policy.PondId, PondName = _runtime.GetPond(policy.PondId)?.Name ?? policy.PondId,
            TriggerMetric = policy.Metric, DeviceCommandId = command.Id, Timestamp = DateTime.UtcNow,
            Message = command.Status == "completed"
                ? $"Policy {policy.Name}: {device.Name} {(desired == "on" ? "START" : "STOP")} tại {value:0.###}."
                : $"Policy {policy.Name} không thực thi: {command.Status} · {command.Error}"
        };
        _runtime.AddAutomationLog(log); await _farm.InsertAutomationLogAsync(log, ct);
    }

    private async Task SetStateAsync(DeviceAutomationPolicy policy, string state, DateTime? conditionSince, CancellationToken ct)
    {
        if (policy.RuntimeState == state && policy.ConditionSinceUtc == conditionSince) return;
        policy.RuntimeState = state; policy.ConditionSinceUtc = conditionSince; policy.UpdatedAtUtc = DateTime.UtcNow;
        await _configuration.UpsertAutomationPolicyAsync(policy, ct);
    }
}
