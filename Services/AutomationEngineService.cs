using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Services;

public sealed class AutomationEngineService
{
    private readonly FarmRuntimeState _runtime;
    private readonly FarmRepository _repository;
    private readonly IOptionsMonitor<SystemBehaviorOptions> _behavior;
    private readonly DeviceCommandService _deviceCommands;

    public AutomationEngineService(FarmRuntimeState runtime, FarmRepository repository,
        IOptionsMonitor<SystemBehaviorOptions> behavior, DeviceCommandService deviceCommands)
    {
        _runtime = runtime;
        _repository = repository;
        _behavior = behavior;
        _deviceCommands = deviceCommands;
    }

    public async Task ProcessAsync(string pondId, string metric, double value, CancellationToken ct)
    {
        foreach (var policy in _behavior.CurrentValue.AutomationPolicies
                     .Where(x => x.Metric.Equals(metric, StringComparison.OrdinalIgnoreCase)))
        {
            var start = policy.TriggerDirection.Equals("low", StringComparison.OrdinalIgnoreCase)
                ? value <= policy.StartAt
                : value >= policy.StartAt;
            var stop = policy.TriggerDirection.Equals("low", StringComparison.OrdinalIgnoreCase)
                ? value >= policy.StopAt
                : value <= policy.StopAt;

            if (!start && !stop) continue;
            var desired = start ? "on" : "off";
            var devices = _runtime.Devices.Where(x => x.PondId == pondId && x.Mode == "auto" &&
                policy.DeviceTypes.Contains(x.Type, StringComparer.OrdinalIgnoreCase) && x.Status != desired).ToList();

            foreach (var device in devices)
            {
                // The dispatcher changes device state only after the configured transport ACKs.
                var command = await _deviceCommands.ExecuteAsync(
                    device.Id, desired, "automation-engine", $"policy:{metric}", ct);
                var pond = _runtime.GetPond(pondId);
                var template = start ? policy.StartMessage : policy.StopMessage;
                var log = new AutomationLog
                {
                    PondId = pondId,
                    PondName = pond?.Name ?? pondId,
                    TriggerMetric = metric,
                    DeviceCommandId = command.Id,
                    Timestamp = DateTime.UtcNow,
                    Message = command.Status == "completed"
                        ? template
                            .Replace("{value}", value.ToString("0.###"), StringComparison.OrdinalIgnoreCase)
                            .Replace("{device}", device.Name, StringComparison.OrdinalIgnoreCase)
                        : $"Không thực thi được automation cho {device.Name}: {command.Status} · {command.Error}"
                };
                _runtime.AddAutomationLog(log);
                await _repository.InsertAutomationLogAsync(log, ct);
            }
        }
    }
}
