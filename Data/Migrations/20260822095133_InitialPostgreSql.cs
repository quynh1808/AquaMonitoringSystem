using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquacultureMonitoring.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alarm_rules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WarningLow = table.Column<double>(type: "double precision", nullable: true),
                    WarningHigh = table.Column<double>(type: "double precision", nullable: true),
                    CriticalLow = table.Column<double>(type: "double precision", nullable: true),
                    CriticalHigh = table.Column<double>(type: "double precision", nullable: true),
                    Hysteresis = table.Column<double>(type: "double precision", nullable: false),
                    DelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alarm_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "alert_notifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AlertId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Recipient = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    PondName = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Threshold = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "text", nullable: true),
                    ResolvedBy = table.Column<string>(type: "text", nullable: true),
                    ProposedSolution = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Result = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    PondName = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    TriggerMetric = table.Column<string>(type: "text", nullable: false),
                    DeviceCommandId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_policies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerDirection = table.Column<string>(type: "text", nullable: false),
                    StartThreshold = table.Column<double>(type: "double precision", nullable: false),
                    StopThreshold = table.Column<double>(type: "double precision", nullable: false),
                    StartPersistenceSeconds = table.Column<int>(type: "integer", nullable: false),
                    StopPersistenceSeconds = table.Column<int>(type: "integer", nullable: false),
                    MinimumOnSeconds = table.Column<int>(type: "integer", nullable: false),
                    MinimumOffSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaximumStartsPerHour = table.Column<int>(type: "integer", nullable: false),
                    InputFailureAction = table.Column<string>(type: "text", nullable: false),
                    RuntimeState = table.Column<string>(type: "text", nullable: false),
                    ConditionSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTransitionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecentStartTimesUtc = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "biosecurity_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Counterparty = table.Column<string>(type: "text", nullable: false),
                    EvidenceReference = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_biosecurity_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "calibration_records",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextDueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedBy = table.Column<string>(type: "text", nullable: false),
                    ValueBefore = table.Column<double>(type: "double precision", nullable: true),
                    ValueAfter = table.Column<double>(type: "double precision", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calibration_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "configuration_deployments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CommandId = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    DesiredVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReportedVersion = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DesiredJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReportedJson = table.Column<string>(type: "jsonb", nullable: true),
                    PreviousJson = table.Column<string>(type: "jsonb", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_deployments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "connection_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "data_source_health",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    LastConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    MessagesReceived = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_source_health", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_commands",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    RequestedState = table.Column<string>(type: "text", nullable: false),
                    PreviousState = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Transport = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_commands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Supplier = table.Column<string>(type: "text", nullable: false),
                    InvoiceReference = table.Column<string>(type: "text", nullable: false),
                    IncurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "feed_logs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    PondName = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    FeedLotId = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FeedType = table.Column<string>(type: "text", nullable: false),
                    AmountKg = table.Column<double>(type: "double precision", nullable: false),
                    Technician = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "harvest_lots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    LotCode = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    HarvestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QuantityKg = table.Column<double>(type: "double precision", nullable: false),
                    Grade = table.Column<string>(type: "text", nullable: false),
                    Customer = table.Column<string>(type: "text", nullable: false),
                    TransportReference = table.Column<string>(type: "text", nullable: false),
                    StorageTemperatureC = table.Column<double>(type: "double precision", nullable: true),
                    RecordedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_harvest_lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "health_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    SignsOrDiagnosis = table.Column<string>(type: "text", nullable: false),
                    Treatment = table.Column<string>(type: "text", nullable: false),
                    MedicineLot = table.Column<string>(type: "text", nullable: false),
                    LaboratoryReference = table.Column<string>(type: "text", nullable: false),
                    WithdrawalUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FarmId = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LotNumber = table.Column<string>(type: "text", nullable: false),
                    Supplier = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    QuantityOnHand = table.Column<double>(type: "double precision", nullable: false),
                    ReorderLevel = table.Column<double>(type: "double precision", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    InventoryItemId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<double>(type: "double precision", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_tasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<string>(type: "text", nullable: false),
                    AssetType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedTo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mqtt_broker_profiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    ProtectedPassword = table.Column<string>(type: "text", nullable: false),
                    UseTls = table.Column<bool>(type: "boolean", nullable: false),
                    AllowUntrustedCertificates = table.Column<bool>(type: "boolean", nullable: false),
                    KeepAliveSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mqtt_broker_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "node_capabilities",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "text", nullable: false),
                    SensorChannels = table.Column<string>(type: "jsonb", nullable: false),
                    ReportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_capabilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "node_sockets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Transport = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    RuntimeStatus = table.Column<string>(type: "text", nullable: false),
                    HealthStatus = table.Column<string>(type: "text", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    LastConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    LastLatencyMilliseconds = table.Column<double>(type: "double precision", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Mqtt = table.Column<string>(type: "jsonb", nullable: true),
                    ModbusTcp = table.Column<string>(type: "jsonb", nullable: true),
                    DesiredVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReportedVersion = table.Column<long>(type: "bigint", nullable: false),
                    DocumentVersion = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_sockets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ponds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FarmId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Species = table.Column<string>(type: "text", nullable: false),
                    StockingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StockingDensity = table.Column<double>(type: "double precision", nullable: false),
                    Area = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedHarvestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ponds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sensor_health_events",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    InstallationId = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_health_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sensor_health_snapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    InstallationId = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DriverStatus = table.Column<string>(type: "text", nullable: false),
                    BusStatus = table.Column<string>(type: "text", nullable: false),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    ReadLatencyMilliseconds = table.Column<double>(type: "double precision", nullable: true),
                    PhysicalMin = table.Column<double>(type: "double precision", nullable: true),
                    PhysicalMax = table.Column<double>(type: "double precision", nullable: true),
                    DriverVersion = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    LastSampleAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastGoodSampleAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_health_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unassigned_node_messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BrokerProfileId = table.Column<string>(type: "text", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unassigned_node_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    NormalizedUsername = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PasswordSalt = table.Column<string>(type: "text", nullable: false),
                    PasswordIterations = table.Column<int>(type: "integer", nullable: false),
                    Roles = table.Column<string>(type: "jsonb", nullable: false),
                    AllowedPondIds = table.Column<string>(type: "jsonb", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SessionVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SopReference = table.Column<string>(type: "text", nullable: false),
                    CompletionEvidence = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    PowerKw = table.Column<double>(type: "double precision", nullable: false),
                    ControlTransport = table.Column<string>(type: "text", nullable: false),
                    ExternalAddress = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationStatus = table.Column<string>(type: "text", nullable: false),
                    DesiredConfigurationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReportedConfigurationVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsPendingRemoval = table.Column<bool>(type: "boolean", nullable: false),
                    RemovalRequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemovalRequestedBy = table.Column<string>(type: "text", nullable: true),
                    RemovalReason = table.Column<string>(type: "text", nullable: true),
                    ReportedMode = table.Column<string>(type: "text", nullable: false),
                    AutoRequestVariable = table.Column<string>(type: "text", nullable: false),
                    AutoFeedbackVariable = table.Column<string>(type: "text", nullable: false),
                    ManualStartRequestVariable = table.Column<string>(type: "text", nullable: false),
                    ManualStopRequestVariable = table.Column<string>(type: "text", nullable: false),
                    LocalOverrideVariable = table.Column<string>(type: "text", nullable: false),
                    RemoteAllowedVariable = table.Column<string>(type: "text", nullable: false),
                    RunFeedbackVariable = table.Column<string>(type: "text", nullable: false),
                    FaultVariable = table.Column<string>(type: "text", nullable: false),
                    IsLocalOverrideActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsRemoteControlAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    LastCommandError = table.Column<string>(type: "text", nullable: true),
                    FailSafeState = table.Column<string>(type: "text", nullable: false),
                    ConnectionStatus = table.Column<string>(type: "text", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_ponds_PondId",
                        column: x => x.PondId,
                        principalTable: "ponds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_cycles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FarmId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Species = table.Column<string>(type: "text", nullable: false),
                    SeedBatch = table.Column<string>(type: "text", nullable: false),
                    PreparationStartedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StockingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InitialQuantity = table.Column<long>(type: "bigint", nullable: false),
                    InitialAverageWeightGrams = table.Column<double>(type: "double precision", nullable: false),
                    StockingDensityPerM2 = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedHarvestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualHarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EstimatedSurvivalRatePercent = table.Column<double>(type: "double precision", nullable: false),
                    LatestAverageWeightGrams = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedBiomassKg = table.Column<double>(type: "double precision", nullable: false),
                    LatestSampleAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GrowthSampleCount = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDailyBiomassGrowthKg = table.Column<double>(type: "double precision", nullable: false),
                    TotalFeedKg = table.Column<double>(type: "double precision", nullable: false),
                    MortalityQuantity = table.Column<double>(type: "double precision", nullable: false),
                    HarvestQuantityKg = table.Column<double>(type: "double precision", nullable: false),
                    HarvestedAnimalQuantity = table.Column<long>(type: "bigint", nullable: false),
                    FeedConversionRatio = table.Column<double>(type: "double precision", nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_cycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_production_cycles_ponds_PondId",
                        column: x => x.PondId,
                        principalTable: "ponds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    ExternalAddress = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    ChannelId = table.Column<string>(type: "text", nullable: false),
                    InstallationId = table.Column<string>(type: "text", nullable: false),
                    PublishIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    OfflineTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    SchedulingMode = table.Column<string>(type: "text", nullable: false),
                    ConfigurationStatus = table.Column<string>(type: "text", nullable: false),
                    DesiredConfigurationVersion = table.Column<long>(type: "bigint", nullable: false),
                    ReportedConfigurationVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsPendingRemoval = table.Column<bool>(type: "boolean", nullable: false),
                    RemovalRequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RemovalRequestedBy = table.Column<string>(type: "text", nullable: true),
                    RemovalReason = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BatteryLevel = table.Column<int>(type: "integer", nullable: true),
                    SignalStrength = table.Column<int>(type: "integer", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "text", nullable: false),
                    CalibrationDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CalibrationDueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MinValidValue = table.Column<double>(type: "double precision", nullable: false),
                    MaxValidValue = table.Column<double>(type: "double precision", nullable: false),
                    LastQuality = table.Column<string>(type: "text", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensors_ponds_PondId",
                        column: x => x.PondId,
                        principalTable: "ponds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "growth_samples",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    SampledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    AverageWeightGrams = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedSurvivalRatePercent = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedBiomassKg = table.Column<double>(type: "double precision", nullable: false),
                    SampledBy = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_growth_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_growth_samples_production_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "production_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "harvest_records",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    HarvestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QuantityKg = table.Column<double>(type: "double precision", nullable: false),
                    AnimalQuantity = table.Column<long>(type: "bigint", nullable: false),
                    AverageWeightGrams = table.Column<double>(type: "double precision", nullable: false),
                    HarvestType = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_harvest_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_harvest_records_production_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "production_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mortality_records",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CycleId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    SuspectedCause = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mortality_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mortality_records_production_cycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "production_cycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "latest_sensor_states",
                columns: table => new
                {
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    MeasuredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InstallationId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_latest_sensor_states", x => x.SensorId);
                    table.ForeignKey(
                        name: "FK_latest_sensor_states_ponds_PondId",
                        column: x => x.PondId,
                        principalTable: "ponds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_latest_sensor_states_sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_installations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    ChannelId = table.Column<string>(type: "text", nullable: false),
                    SerialNumber = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Manufacturer = table.Column<string>(type: "text", nullable: false),
                    Driver = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    InstalledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InstalledBy = table.Column<string>(type: "text", nullable: false),
                    RemovalReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_installations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_installations_sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_readings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PondId = table.Column<string>(type: "text", nullable: false),
                    SensorId = table.Column<string>(type: "text", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SocketId = table.Column<string>(type: "text", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    InstallationId = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BatteryLevel = table.Column<int>(type: "integer", nullable: true),
                    SignalStrength = table.Column<int>(type: "integer", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "text", nullable: true),
                    RawPayload = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: true),
                    IsBackfill = table.Column<bool>(type: "boolean", nullable: false),
                    TimeQuality = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_readings_ponds_PondId",
                        column: x => x.PondId,
                        principalTable: "ponds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_readings_sensors_SensorId",
                        column: x => x.SensorId,
                        principalTable: "sensors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alarm_rules_PondId_Metric",
                table: "alarm_rules",
                columns: new[] { "PondId", "Metric" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_configuration_deployments_CommandId",
                table: "configuration_deployments",
                column: "CommandId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connection_logs_SocketId_TimestampUtc",
                table: "connection_logs",
                columns: new[] { "SocketId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_devices_PondId",
                table: "devices",
                column: "PondId");

            migrationBuilder.CreateIndex(
                name: "IX_growth_samples_CycleId",
                table: "growth_samples",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_harvest_lots_LotCode",
                table: "harvest_lots",
                column: "LotCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_harvest_records_CycleId",
                table: "harvest_records",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_FarmId_LotNumber",
                table: "inventory_items",
                columns: new[] { "FarmId", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_latest_sensor_states_PondId_Metric",
                table: "latest_sensor_states",
                columns: new[] { "PondId", "Metric" });

            migrationBuilder.CreateIndex(
                name: "IX_mortality_records_CycleId",
                table: "mortality_records",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_node_sockets_NodeId_Transport",
                table: "node_sockets",
                columns: new[] { "NodeId", "Transport" },
                unique: true,
                filter: "\"IsArchived\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_production_cycles_PondId",
                table: "production_cycles",
                column: "PondId",
                unique: true,
                filter: "\"IsOpen\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_health_events_EventId",
                table: "sensor_health_events",
                column: "EventId",
                unique: true,
                filter: "\"EventId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_installations_SensorId",
                table: "sensor_installations",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_EventId",
                table: "sensor_readings",
                column: "EventId",
                unique: true,
                filter: "\"EventId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_PondId_Metric_Timestamp",
                table: "sensor_readings",
                columns: new[] { "PondId", "Metric", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_SensorId_Timestamp",
                table: "sensor_readings",
                columns: new[] { "SensorId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_Timestamp",
                table: "sensor_readings",
                column: "Timestamp")
                .Annotation("Npgsql:IndexMethod", "brin");

            migrationBuilder.CreateIndex(
                name: "IX_sensors_PondId",
                table: "sensors",
                column: "PondId");

            migrationBuilder.CreateIndex(
                name: "IX_sensors_SocketId_ChannelId",
                table: "sensors",
                columns: new[] { "SocketId", "ChannelId" },
                unique: true,
                filter: "\"IsArchived\" = FALSE AND \"SocketId\" <> '' AND \"ChannelId\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_unassigned_node_messages_ExpiresAtUtc",
                table: "unassigned_node_messages",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_users_NormalizedUsername",
                table: "users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alarm_rules");

            migrationBuilder.DropTable(
                name: "alert_notifications");

            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "automation_logs");

            migrationBuilder.DropTable(
                name: "automation_policies");

            migrationBuilder.DropTable(
                name: "biosecurity_events");

            migrationBuilder.DropTable(
                name: "calibration_records");

            migrationBuilder.DropTable(
                name: "configuration_deployments");

            migrationBuilder.DropTable(
                name: "connection_logs");

            migrationBuilder.DropTable(
                name: "data_source_health");

            migrationBuilder.DropTable(
                name: "device_commands");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "feed_logs");

            migrationBuilder.DropTable(
                name: "growth_samples");

            migrationBuilder.DropTable(
                name: "harvest_lots");

            migrationBuilder.DropTable(
                name: "harvest_records");

            migrationBuilder.DropTable(
                name: "health_events");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropTable(
                name: "inventory_transactions");

            migrationBuilder.DropTable(
                name: "latest_sensor_states");

            migrationBuilder.DropTable(
                name: "maintenance_tasks");

            migrationBuilder.DropTable(
                name: "mortality_records");

            migrationBuilder.DropTable(
                name: "mqtt_broker_profiles");

            migrationBuilder.DropTable(
                name: "node_capabilities");

            migrationBuilder.DropTable(
                name: "node_sockets");

            migrationBuilder.DropTable(
                name: "sensor_health_events");

            migrationBuilder.DropTable(
                name: "sensor_health_snapshots");

            migrationBuilder.DropTable(
                name: "sensor_installations");

            migrationBuilder.DropTable(
                name: "sensor_readings");

            migrationBuilder.DropTable(
                name: "unassigned_node_messages");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "work_orders");

            migrationBuilder.DropTable(
                name: "production_cycles");

            migrationBuilder.DropTable(
                name: "sensors");

            migrationBuilder.DropTable(
                name: "ponds");
        }
    }
}
