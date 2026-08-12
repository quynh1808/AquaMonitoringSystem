using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AquacultureMonitoring.Data;

public sealed class MongoContext
{
    public MongoContext(IOptions<MongoDbOptions> options)
    {
        var value = options.Value;
        var settings = MongoClientSettings.FromConnectionString(value.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(value.ServerSelectionTimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(value.Username))
        {
            var authenticationDatabase = string.IsNullOrWhiteSpace(value.AuthenticationDatabase)
                ? value.DatabaseName
                : value.AuthenticationDatabase;
            settings.Credential = MongoCredential.CreateCredential(authenticationDatabase, value.Username, value.Password);
        }
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(value.DatabaseName);
    }

    public MongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<Pond> Ponds => Database.GetCollection<Pond>("ponds");
    public IMongoCollection<TelemetryData> LatestTelemetry => Database.GetCollection<TelemetryData>("telemetry_latest");
    public IMongoCollection<SensorDefinition> Sensors => Database.GetCollection<SensorDefinition>("sensors");
    public IMongoCollection<SensorReading> SensorReadings => Database.GetCollection<SensorReading>("sensor_readings");
    public IMongoCollection<Device> Devices => Database.GetCollection<Device>("devices");
    public IMongoCollection<DeviceCommand> DeviceCommands => Database.GetCollection<DeviceCommand>("device_commands");
    public IMongoCollection<AlertItem> Alerts => Database.GetCollection<AlertItem>("alerts");
    public IMongoCollection<AlertNotification> AlertNotifications => Database.GetCollection<AlertNotification>("alert_notifications");
    public IMongoCollection<AlarmRule> AlarmRules => Database.GetCollection<AlarmRule>("alarm_rules");
    public IMongoCollection<FeedLog> FeedLogs => Database.GetCollection<FeedLog>("feed_logs");
    public IMongoCollection<AutomationLog> AutomationLogs => Database.GetCollection<AutomationLog>("automation_logs");
    public IMongoCollection<CalibrationRecord> CalibrationRecords => Database.GetCollection<CalibrationRecord>("calibration_records");
    public IMongoCollection<MaintenanceTask> MaintenanceTasks => Database.GetCollection<MaintenanceTask>("maintenance_tasks");
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("audit_logs");
    public IMongoCollection<DataSourceHealth> DataSourceHealth => Database.GetCollection<DataSourceHealth>("data_source_health");
    public IMongoCollection<AppUser> Users => Database.GetCollection<AppUser>("users");
    public IMongoCollection<ProductionCycle> ProductionCycles => Database.GetCollection<ProductionCycle>("production_cycles");
    public IMongoCollection<GrowthSample> GrowthSamples => Database.GetCollection<GrowthSample>("growth_samples");
    public IMongoCollection<MortalityRecord> MortalityRecords => Database.GetCollection<MortalityRecord>("mortality_records");
    public IMongoCollection<HarvestRecord> HarvestRecords => Database.GetCollection<HarvestRecord>("harvest_records");
    // Runtime connection configuration is stored separately from application configuration so
    // an administrator can commission or replace field nodes without rebuilding the container.
    public IMongoCollection<MqttBrokerProfile> MqttBrokerProfiles => Database.GetCollection<MqttBrokerProfile>("mqtt_broker_profiles");
    public IMongoCollection<NodeSocket> NodeSockets => Database.GetCollection<NodeSocket>("node_sockets");
    public IMongoCollection<NodeCapabilitySnapshot> NodeCapabilities => Database.GetCollection<NodeCapabilitySnapshot>("node_capabilities");
    public IMongoCollection<ConfigurationDeployment> ConfigurationDeployments => Database.GetCollection<ConfigurationDeployment>("configuration_deployments");
    public IMongoCollection<ConnectionLogEntry> ConnectionLogs => Database.GetCollection<ConnectionLogEntry>("connection_logs");
    public IMongoCollection<SensorHealthSnapshot> SensorHealthSnapshots => Database.GetCollection<SensorHealthSnapshot>("sensor_health_snapshots");
    public IMongoCollection<SensorHealthEvent> SensorHealthEvents => Database.GetCollection<SensorHealthEvent>("sensor_health_events");
    public IMongoCollection<SensorInstallation> SensorInstallations => Database.GetCollection<SensorInstallation>("sensor_installations");
    public IMongoCollection<QuarantinedNodeMessage> QuarantinedNodeMessages => Database.GetCollection<QuarantinedNodeMessage>("unassigned_node_messages");
    public IMongoCollection<DeviceAutomationPolicy> AutomationPolicies => Database.GetCollection<DeviceAutomationPolicy>("automation_policies");
    public IMongoCollection<InventoryItem> InventoryItems => Database.GetCollection<InventoryItem>("inventory_items");
    public IMongoCollection<InventoryTransaction> InventoryTransactions => Database.GetCollection<InventoryTransaction>("inventory_transactions");
    public IMongoCollection<AquaticHealthEvent> HealthEvents => Database.GetCollection<AquaticHealthEvent>("health_events");
    public IMongoCollection<BiosecurityEvent> BiosecurityEvents => Database.GetCollection<BiosecurityEvent>("biosecurity_events");
    public IMongoCollection<FarmWorkOrder> WorkOrders => Database.GetCollection<FarmWorkOrder>("work_orders");
    public IMongoCollection<FarmExpense> Expenses => Database.GetCollection<FarmExpense>("expenses");
    public IMongoCollection<HarvestLot> HarvestLots => Database.GetCollection<HarvestLot>("harvest_lots");
}
