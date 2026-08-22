using System.Text.Json;
using AquacultureMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AquacultureMonitoring.Data;

/// <summary>
/// PostgreSQL unit-of-work for AMS. Human-facing identifiers remain strings because SensorId,
/// DeviceId and NodeId are protocol identities entered by an administrator; database-generated
/// integer keys would make field commissioning and troubleshooting less transparent.
/// </summary>
public sealed class AquaDbContext(DbContextOptions<AquaDbContext> options) : DbContext(options)
{
    public DbSet<Pond> Ponds => Set<Pond>();
    public DbSet<LatestSensorState> LatestSensorStates => Set<LatestSensorState>();
    public DbSet<SensorDefinition> Sensors => Set<SensorDefinition>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceCommand> DeviceCommands => Set<DeviceCommand>();
    public DbSet<AlertItem> Alerts => Set<AlertItem>();
    public DbSet<AlertNotification> AlertNotifications => Set<AlertNotification>();
    public DbSet<AlarmRule> AlarmRules => Set<AlarmRule>();
    public DbSet<FeedLog> FeedLogs => Set<FeedLog>();
    public DbSet<AutomationLog> AutomationLogs => Set<AutomationLog>();
    public DbSet<CalibrationRecord> CalibrationRecords => Set<CalibrationRecord>();
    public DbSet<MaintenanceTask> MaintenanceTasks => Set<MaintenanceTask>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DataSourceHealth> DataSourceHealth => Set<DataSourceHealth>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ProductionCycle> ProductionCycles => Set<ProductionCycle>();
    public DbSet<GrowthSample> GrowthSamples => Set<GrowthSample>();
    public DbSet<MortalityRecord> MortalityRecords => Set<MortalityRecord>();
    public DbSet<HarvestRecord> HarvestRecords => Set<HarvestRecord>();
    public DbSet<MqttBrokerProfile> MqttBrokerProfiles => Set<MqttBrokerProfile>();
    public DbSet<NodeSocket> NodeSockets => Set<NodeSocket>();
    public DbSet<NodeCapabilitySnapshot> NodeCapabilities => Set<NodeCapabilitySnapshot>();
    public DbSet<ConfigurationDeployment> ConfigurationDeployments => Set<ConfigurationDeployment>();
    public DbSet<ConnectionLogEntry> ConnectionLogs => Set<ConnectionLogEntry>();
    public DbSet<SensorHealthSnapshot> SensorHealthSnapshots => Set<SensorHealthSnapshot>();
    public DbSet<SensorHealthEvent> SensorHealthEvents => Set<SensorHealthEvent>();
    public DbSet<SensorInstallation> SensorInstallations => Set<SensorInstallation>();
    public DbSet<QuarantinedNodeMessage> QuarantinedNodeMessages => Set<QuarantinedNodeMessage>();
    public DbSet<DeviceAutomationPolicy> AutomationPolicies => Set<DeviceAutomationPolicy>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<AquaticHealthEvent> HealthEvents => Set<AquaticHealthEvent>();
    public DbSet<BiosecurityEvent> BiosecurityEvents => Set<BiosecurityEvent>();
    public DbSet<FarmWorkOrder> WorkOrders => Set<FarmWorkOrder>();
    public DbSet<FarmExpense> Expenses => Set<FarmExpense>();
    public DbSet<HarvestLot> HarvestLots => Set<HarvestLot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        Entity<Pond>(modelBuilder, "ponds");
        Entity<SensorDefinition>(modelBuilder, "sensors");
        Entity<SensorReading>(modelBuilder, "sensor_readings");
        Entity<Device>(modelBuilder, "devices");
        Entity<DeviceCommand>(modelBuilder, "device_commands");
        Entity<AlertItem>(modelBuilder, "alerts");
        Entity<AlertNotification>(modelBuilder, "alert_notifications");
        Entity<AlarmRule>(modelBuilder, "alarm_rules");
        Entity<FeedLog>(modelBuilder, "feed_logs");
        Entity<AutomationLog>(modelBuilder, "automation_logs");
        Entity<CalibrationRecord>(modelBuilder, "calibration_records");
        Entity<MaintenanceTask>(modelBuilder, "maintenance_tasks");
        Entity<AuditLog>(modelBuilder, "audit_logs");
        Entity<DataSourceHealth>(modelBuilder, "data_source_health");
        Entity<AppUser>(modelBuilder, "users");
        Entity<ProductionCycle>(modelBuilder, "production_cycles");
        Entity<GrowthSample>(modelBuilder, "growth_samples");
        Entity<MortalityRecord>(modelBuilder, "mortality_records");
        Entity<HarvestRecord>(modelBuilder, "harvest_records");
        Entity<MqttBrokerProfile>(modelBuilder, "mqtt_broker_profiles");
        Entity<NodeSocket>(modelBuilder, "node_sockets");
        Entity<NodeCapabilitySnapshot>(modelBuilder, "node_capabilities");
        Entity<ConfigurationDeployment>(modelBuilder, "configuration_deployments");
        Entity<ConnectionLogEntry>(modelBuilder, "connection_logs");
        Entity<SensorHealthSnapshot>(modelBuilder, "sensor_health_snapshots");
        Entity<SensorHealthEvent>(modelBuilder, "sensor_health_events");
        Entity<SensorInstallation>(modelBuilder, "sensor_installations");
        Entity<QuarantinedNodeMessage>(modelBuilder, "unassigned_node_messages");
        Entity<DeviceAutomationPolicy>(modelBuilder, "automation_policies");
        Entity<InventoryItem>(modelBuilder, "inventory_items");
        Entity<InventoryTransaction>(modelBuilder, "inventory_transactions");
        Entity<AquaticHealthEvent>(modelBuilder, "health_events");
        Entity<BiosecurityEvent>(modelBuilder, "biosecurity_events");
        Entity<FarmWorkOrder>(modelBuilder, "work_orders");
        Entity<FarmExpense>(modelBuilder, "expenses");
        Entity<HarvestLot>(modelBuilder, "harvest_lots");

        modelBuilder.Entity<LatestSensorState>(entity =>
        {
            entity.ToTable("latest_sensor_states");
            entity.HasKey(x => x.SensorId);
            entity.HasIndex(x => new { x.PondId, x.Metric });
        });

        // Metadata is the transport object. Its forwarding scalar properties are the relational
        // columns, allowing useful indexes without coupling MQTT payload shape to PostgreSQL.
        modelBuilder.Entity<SensorReading>().Ignore(x => x.Metadata);
        modelBuilder.Entity<SensorReading>().HasIndex(x => x.EventId).IsUnique()
            .HasFilter("\"EventId\" <> ''");
        modelBuilder.Entity<SensorReading>().HasIndex(x => new { x.SensorId, x.Timestamp });
        modelBuilder.Entity<SensorReading>().HasIndex(x => new { x.PondId, x.Metric, x.Timestamp });
        modelBuilder.Entity<SensorReading>().HasIndex(x => x.Timestamp).HasMethod("brin");

        modelBuilder.Entity<SensorDefinition>().HasIndex(x => new { x.SocketId, x.ChannelId }).IsUnique()
            .HasFilter("\"IsArchived\" = FALSE AND \"SocketId\" <> '' AND \"ChannelId\" <> ''");
        modelBuilder.Entity<NodeSocket>().HasIndex(x => new { x.NodeId, x.Transport }).IsUnique()
            .HasFilter("\"IsArchived\" = FALSE");
        modelBuilder.Entity<ProductionCycle>().HasIndex(x => x.PondId).IsUnique()
            .HasFilter("\"IsOpen\" = TRUE");
        modelBuilder.Entity<AppUser>().HasIndex(x => x.NormalizedUsername).IsUnique();
        modelBuilder.Entity<HarvestLot>().HasIndex(x => x.LotCode).IsUnique();
        modelBuilder.Entity<InventoryItem>().HasIndex(x => new { x.FarmId, x.LotNumber }).IsUnique();
        modelBuilder.Entity<AlarmRule>().HasIndex(x => new { x.PondId, x.Metric }).IsUnique();
        modelBuilder.Entity<SensorHealthEvent>().HasIndex(x => x.EventId).IsUnique()
            .HasFilter("\"EventId\" <> ''");
        modelBuilder.Entity<ConfigurationDeployment>().HasIndex(x => x.CommandId).IsUnique();
        modelBuilder.Entity<ConnectionLogEntry>().HasIndex(x => new { x.SocketId, x.TimestampUtc });
        modelBuilder.Entity<QuarantinedNodeMessage>().HasIndex(x => x.ExpiresAtUtc);

        ConfigureJson(modelBuilder.Entity<AppUser>().Property(x => x.Roles));
        ConfigureJson(modelBuilder.Entity<AppUser>().Property(x => x.AllowedPondIds));
        ConfigureJson(modelBuilder.Entity<NodeSocket>().Property(x => x.Mqtt));
        ConfigureJson(modelBuilder.Entity<NodeSocket>().Property(x => x.ModbusTcp));
        ConfigureJson(modelBuilder.Entity<NodeCapabilitySnapshot>().Property(x => x.SensorChannels));
        ConfigureJson(modelBuilder.Entity<DeviceAutomationPolicy>().Property(x => x.RecentStartTimesUtc));
        modelBuilder.Entity<ConfigurationDeployment>().Property(x => x.DesiredJson).HasColumnType("jsonb");
        modelBuilder.Entity<ConfigurationDeployment>().Property(x => x.ReportedJson).HasColumnType("jsonb");
        modelBuilder.Entity<ConfigurationDeployment>().Property(x => x.PreviousJson).HasColumnType("jsonb");
        modelBuilder.Entity<FarmExpense>().Property(x => x.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<InventoryTransaction>().Property(x => x.UnitCost).HasPrecision(18, 2);

        // Core ownership relationships are database-enforced. Restrict keeps operational history
        // intact; domain services archive commissioned assets instead of physically deleting them.
        RequiredPond<SensorDefinition>(modelBuilder);
        RequiredPond<Device>(modelBuilder);
        RequiredPond<ProductionCycle>(modelBuilder);
        RequiredPond<LatestSensorState>(modelBuilder);
        RequiredPond<SensorReading>(modelBuilder);
        modelBuilder.Entity<LatestSensorState>().HasOne<SensorDefinition>().WithMany()
            .HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SensorReading>().HasOne<SensorDefinition>().WithMany()
            .HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SensorInstallation>().HasOne<SensorDefinition>().WithMany()
            .HasForeignKey(x => x.SensorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GrowthSample>().HasOne<ProductionCycle>().WithMany()
            .HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MortalityRecord>().HasOne<ProductionCycle>().WithMany()
            .HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<HarvestRecord>().HasOne<ProductionCycle>().WithMany()
            .HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void Entity<TEntity>(ModelBuilder builder, string tableName) where TEntity : class
    {
        builder.Entity<TEntity>().ToTable(tableName);
        builder.Entity<TEntity>().HasKey("Id");
    }

    private static void RequiredPond<TEntity>(ModelBuilder builder) where TEntity : class
    {
        builder.Entity<TEntity>().HasOne<Pond>().WithMany().HasForeignKey("PondId")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void ConfigureJson<T>(PropertyBuilder<T> property)
    {
        var comparer = new ValueComparer<T>(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(StringComparison.Ordinal),
            value => Deserialize<T>(Serialize(value))!);
        property.HasConversion(value => Serialize(value), value => Deserialize<T>(value)!)
            .HasColumnType("jsonb");
        property.Metadata.SetValueComparer(comparer);
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
}

/// <summary>Shared safe upsert used by repositories that own disconnected EF entities.</summary>
internal static class DbContextUpsertExtensions
{
    public static async Task UpsertAsync<TEntity>(this AquaDbContext db, TEntity item, string id,
        CancellationToken ct) where TEntity : class
    {
        var current = await db.Set<TEntity>().FindAsync([id], ct);
        if (current is null) db.Set<TEntity>().Add(item);
        else db.Entry(current).CurrentValues.SetValues(item);
        await db.SaveChangesAsync(ct);
    }
}
