using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Configuration;

/// <summary>Fails closed before hosted services can write synthetic data to a production farm.</summary>
public sealed class OperationalModeGuard
{
    private readonly OperationalModeOptions _mode;
    private readonly SimulationProfileOptions _sensorSimulation;
    private readonly DashboardSimulationOptions _dashboardSimulation;
    private readonly ProductionCycleSimulationOptions _cycleSimulation;
    private readonly SensorConnectionOptions _connections;
    private readonly DeviceControlOptions _deviceControl;
    private readonly AlertNotificationOptions _notifications;

    public OperationalModeGuard(IOptions<OperationalModeOptions> mode,
        IOptions<SimulationProfileOptions> sensorSimulation,
        IOptions<DashboardSimulationOptions> dashboardSimulation,
        IOptions<ProductionCycleSimulationOptions> cycleSimulation,
        IOptions<SensorConnectionOptions> connections,
        IOptions<DeviceControlOptions> deviceControl,
        IOptions<AlertNotificationOptions> notifications)
    {
        _mode = mode.Value; _sensorSimulation = sensorSimulation.Value;
        _dashboardSimulation = dashboardSimulation.Value; _cycleSimulation = cycleSimulation.Value;
        _connections = connections.Value; _deviceControl = deviceControl.Value; _notifications = notifications.Value;
    }

    public void Validate()
    {
        if (!_mode.IsProduction) return;
        var errors = new List<string>();
        if (_sensorSimulation.Enabled) errors.Add("SimulationProfile:Enabled phải là false");
        if (_dashboardSimulation.Enabled) errors.Add("DashboardSimulation:Enabled phải là false");
        if (_cycleSimulation.Enabled) errors.Add("ProductionCycleSimulation:Enabled phải là false");
        if (_deviceControl.SimulationEnabled) errors.Add("DeviceControl:SimulationEnabled phải là false");
        if (_connections.ActiveSources.Contains("simulation", StringComparer.OrdinalIgnoreCase))
            errors.Add("SensorConnections:ActiveSources không được chứa simulation");
        if (!_connections.ActiveSources.Any(x => !x.Equals("simulation", StringComparison.OrdinalIgnoreCase)))
            errors.Add("cần ít nhất một nguồn telemetry thật");
        if (!_notifications.Enabled) errors.Add("AlertNotifications:Enabled phải là true");
        if (errors.Count > 0)
            throw new OptionsValidationException(OperationalModeOptions.SectionName, typeof(OperationalModeOptions),
                errors.Select(x => $"Production safety: {x}."));
    }
}
