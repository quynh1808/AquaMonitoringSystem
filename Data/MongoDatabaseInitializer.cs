using System.Text.Json;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AquacultureMonitoring.Data;

public sealed class MongoDatabaseInitializer
{
    private readonly MongoContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly SensorThresholdCatalog _thresholds;
    private readonly SimulationProfileOptions _simulation;
    private readonly SystemBehaviorOptions _behavior;
    private readonly ProductionCycleSimulationOptions _cycleSimulation;
    private readonly ILogger<MongoDatabaseInitializer> _logger;

    public MongoDatabaseInitializer(
        MongoContext db,
        IWebHostEnvironment environment,
        IOptions<SensorThresholdCatalog> thresholds,
        IOptions<SimulationProfileOptions> simulation,
        IOptions<SystemBehaviorOptions> behavior,
        IOptions<ProductionCycleSimulationOptions> cycleSimulation,
        ILogger<MongoDatabaseInitializer> logger)
    {
        _db = db;
        _environment = environment;
        _thresholds = thresholds.Value;
        _simulation = simulation.Value;
        _behavior = behavior.Value;
        _cycleSimulation = cycleSimulation.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _db.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        await NormalizeCycleOpenStateAsync(cancellationToken);
        await CreateIndexesAsync(cancellationToken);
        await SynchronizeAlarmRulesAsync(cancellationToken);

        if (await _db.Ponds.CountDocumentsAsync(FilterDefinition<Pond>.Empty, cancellationToken: cancellationToken) == 0)
        {
            await SeedAsync(cancellationToken);
        }
        await EnsureProductionCycleSeedAsync(cancellationToken);
        await BackfillFeedCycleIdsAsync(cancellationToken);
    }

    private async Task NormalizeCycleOpenStateAsync(CancellationToken ct)
    {
        // Legacy releases inferred "open" from Status and could contain more than one active
        // row per pond. Close every row first, then reopen only the newest compatible cycle so
        // the unique partial index can be created without deleting historical documents.
        await _db.ProductionCycles.UpdateManyAsync(
            FilterDefinition<ProductionCycle>.Empty,
            Builders<ProductionCycle>.Update.Set(x => x.IsOpen, false), cancellationToken: ct);
        var candidates = await _db.ProductionCycles.Find(
                Builders<ProductionCycle>.Filter.Nin(x => x.Status, new[] { "completed", "cancelled" }))
            .SortByDescending(x => x.StockingDate).ToListAsync(ct);
        foreach (var cycle in candidates.GroupBy(x => x.PondId, StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
            await _db.ProductionCycles.UpdateOneAsync(x => x.Id == cycle.Id,
                Builders<ProductionCycle>.Update.Set(x => x.IsOpen, true), cancellationToken: ct);
    }

    /// <summary>Links legacy feed rows to the most recent compatible cycle on the same pond.</summary>
    private async Task BackfillFeedCycleIdsAsync(CancellationToken ct)
    {
        var legacy = await _db.FeedLogs.Find(x => x.CycleId == string.Empty).ToListAsync(ct);
        if (legacy.Count == 0) return;
        var cycles = await _db.ProductionCycles.Find(FilterDefinition<ProductionCycle>.Empty).ToListAsync(ct);
        foreach (var feed in legacy)
        {
            var feedDate = DateOnly.FromDateTime(feed.Timestamp.ToUniversalTime());
            var cycle = cycles.Where(x => x.PondId == feed.PondId && x.StockingDate <= feedDate &&
                                          (!x.ActualHarvestDate.HasValue || x.ActualHarvestDate.Value >= feedDate))
                .OrderByDescending(x => x.StockingDate).FirstOrDefault();
            if (cycle is not null)
                await _db.FeedLogs.UpdateOneAsync(x => x.Id == feed.Id,
                    Builders<FeedLog>.Update.Set(x => x.CycleId, cycle.Id), cancellationToken: ct);
        }
    }

    private async Task CreateIndexesAsync(CancellationToken ct)
    {
        await _db.Ponds.Indexes.CreateOneAsync(
            new CreateIndexModel<Pond>(Builders<Pond>.IndexKeys.Ascending(x => x.FarmId).Ascending(x => x.Name)),
            cancellationToken: ct);

        await _db.Sensors.Indexes.CreateManyAsync([
            new CreateIndexModel<SensorDefinition>(Builders<SensorDefinition>.IndexKeys.Ascending(x => x.PondId).Ascending(x => x.Metric)),
            new CreateIndexModel<SensorDefinition>(Builders<SensorDefinition>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.LastSeenAtUtc))
        ], cancellationToken: ct);

        await _db.SensorReadings.Indexes.CreateOneAsync(
            new CreateIndexModel<SensorReading>(Builders<SensorReading>.IndexKeys
                .Ascending(x => x.Metadata.PondId).Ascending(x => x.Metadata.Metric).Descending(x => x.Timestamp),
                new CreateIndexOptions { Name = "ix_sensor_readings_pond_metric_timestamp" }),
            cancellationToken: ct);
        await _db.SensorReadings.Indexes.CreateOneAsync(new CreateIndexModel<SensorReading>(
            Builders<SensorReading>.IndexKeys.Ascending(x => x.EventId),
            new CreateIndexOptions<SensorReading> { Unique = true, Name = "ux_sensor_readings_event_id",
                PartialFilterExpression = Builders<SensorReading>.Filter.Gt(x => x.EventId, string.Empty) }), cancellationToken: ct);
        await EnsureReadingTtlIndexAsync(ct);

        await _db.Alerts.Indexes.CreateOneAsync(
            new CreateIndexModel<AlertItem>(Builders<AlertItem>.IndexKeys
                .Ascending(x => x.PondId).Ascending(x => x.Metric).Ascending(x => x.IsResolved).Descending(x => x.Timestamp)),
            cancellationToken: ct);
        await _db.AlertNotifications.Indexes.CreateOneAsync(new CreateIndexModel<AlertNotification>(
            Builders<AlertNotification>.IndexKeys.Ascending(x => x.AlertId).Ascending(x => x.Channel).Descending(x => x.CreatedAtUtc)), cancellationToken: ct);

        await _db.DeviceCommands.Indexes.CreateOneAsync(
            new CreateIndexModel<DeviceCommand>(Builders<DeviceCommand>.IndexKeys
                .Ascending(x => x.DeviceId).Descending(x => x.CreatedAtUtc)), cancellationToken: ct);

        await _db.CalibrationRecords.Indexes.CreateOneAsync(
            new CreateIndexModel<CalibrationRecord>(Builders<CalibrationRecord>.IndexKeys
                .Ascending(x => x.SensorId).Descending(x => x.PerformedAtUtc)), cancellationToken: ct);

        await _db.MaintenanceTasks.Indexes.CreateManyAsync([
            new CreateIndexModel<MaintenanceTask>(Builders<MaintenanceTask>.IndexKeys
                .Ascending(x => x.Status).Ascending(x => x.DueAtUtc)),
            new CreateIndexModel<MaintenanceTask>(Builders<MaintenanceTask>.IndexKeys
                .Ascending(x => x.PondId).Ascending(x => x.AssetId))
        ], cancellationToken: ct);

        await _db.AuditLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<AuditLog>(Builders<AuditLog>.IndexKeys
                .Descending(x => x.TimestampUtc).Ascending(x => x.EntityType).Ascending(x => x.EntityId)), cancellationToken: ct);

        await _db.Users.Indexes.CreateOneAsync(
            new CreateIndexModel<AppUser>(Builders<AppUser>.IndexKeys.Ascending(x => x.NormalizedUsername),
                new CreateIndexOptions { Unique = true, Name = "ux_users_normalized_username" }), cancellationToken: ct);
        await _db.ProductionCycles.Indexes.CreateManyAsync([
            new CreateIndexModel<ProductionCycle>(Builders<ProductionCycle>.IndexKeys.Ascending(x => x.PondId).Descending(x => x.StockingDate)),
            new CreateIndexModel<ProductionCycle>(Builders<ProductionCycle>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.ExpectedHarvestDate)),
            new CreateIndexModel<ProductionCycle>(Builders<ProductionCycle>.IndexKeys.Ascending(x => x.PondId),
                new CreateIndexOptions<ProductionCycle> { Unique = true, Name = "ux_production_cycles_open_pond",
                    PartialFilterExpression = Builders<ProductionCycle>.Filter.Eq(x => x.IsOpen, true) })
        ], cancellationToken: ct);
        await _db.FeedLogs.Indexes.CreateOneAsync(new CreateIndexModel<FeedLog>(Builders<FeedLog>.IndexKeys.Ascending(x => x.CycleId).Descending(x => x.Timestamp)), cancellationToken: ct);
        await _db.GrowthSamples.Indexes.CreateOneAsync(new CreateIndexModel<GrowthSample>(Builders<GrowthSample>.IndexKeys.Ascending(x => x.CycleId).Descending(x => x.SampledAtUtc)), cancellationToken: ct);
        await _db.MortalityRecords.Indexes.CreateOneAsync(new CreateIndexModel<MortalityRecord>(Builders<MortalityRecord>.IndexKeys.Ascending(x => x.CycleId).Descending(x => x.RecordedAtUtc)), cancellationToken: ct);
        await _db.HarvestRecords.Indexes.CreateOneAsync(new CreateIndexModel<HarvestRecord>(Builders<HarvestRecord>.IndexKeys.Ascending(x => x.CycleId).Descending(x => x.HarvestedAtUtc)), cancellationToken: ct);
        await _db.InventoryItems.Indexes.CreateManyAsync([
            new CreateIndexModel<InventoryItem>(Builders<InventoryItem>.IndexKeys.Ascending(x => x.Category).Ascending(x => x.Name)),
            new CreateIndexModel<InventoryItem>(Builders<InventoryItem>.IndexKeys.Ascending(x => x.LotNumber), new CreateIndexOptions { Unique = true })], cancellationToken: ct);
        await _db.InventoryTransactions.Indexes.CreateManyAsync([
            new CreateIndexModel<InventoryTransaction>(Builders<InventoryTransaction>.IndexKeys.Ascending(x => x.InventoryItemId).Descending(x => x.TimestampUtc)),
            new CreateIndexModel<InventoryTransaction>(Builders<InventoryTransaction>.IndexKeys.Ascending(x => x.CycleId).Descending(x => x.TimestampUtc))], cancellationToken: ct);
        await _db.HealthEvents.Indexes.CreateOneAsync(new CreateIndexModel<AquaticHealthEvent>(Builders<AquaticHealthEvent>.IndexKeys.Ascending(x => x.PondId).Descending(x => x.OccurredAtUtc)), cancellationToken: ct);
        await _db.BiosecurityEvents.Indexes.CreateOneAsync(new CreateIndexModel<BiosecurityEvent>(Builders<BiosecurityEvent>.IndexKeys.Descending(x => x.OccurredAtUtc).Ascending(x => x.EventType)), cancellationToken: ct);
        await _db.WorkOrders.Indexes.CreateOneAsync(new CreateIndexModel<FarmWorkOrder>(Builders<FarmWorkOrder>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.DueAtUtc)), cancellationToken: ct);
        await _db.Expenses.Indexes.CreateOneAsync(new CreateIndexModel<FarmExpense>(Builders<FarmExpense>.IndexKeys.Ascending(x => x.CycleId).Descending(x => x.IncurredAtUtc)), cancellationToken: ct);
        await _db.HarvestLots.Indexes.CreateOneAsync(new CreateIndexModel<HarvestLot>(Builders<HarvestLot>.IndexKeys.Ascending(x => x.LotCode), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
    }


    private async Task EnsureReadingTtlIndexAsync(CancellationToken ct)
    {
        const string indexName = "ttl_sensor_readings_timestamp";
        var desiredSeconds = checked((long)TimeSpan.FromDays(_behavior.ReadingRetentionDays).TotalSeconds);
        using var cursor = await _db.SensorReadings.Indexes.ListAsync(cancellationToken: ct);
        var indexes = await cursor.ToListAsync(ct);
        var existing = indexes.FirstOrDefault(x =>
            x.TryGetValue("name", out var name) && name.IsString && name.AsString == indexName)
            ?? indexes.FirstOrDefault(IsTimestampTtlIndex);

        if (existing is not null)
        {
            var currentSeconds = existing.TryGetValue("expireAfterSeconds", out var expire) && expire.IsNumeric
                ? Convert.ToInt64(expire.ToDouble())
                : -1;
            if (currentSeconds == desiredSeconds) return;

            var existingName = existing.GetValue("name", indexName).AsString;
            await _db.SensorReadings.Indexes.DropOneAsync(existingName, cancellationToken: ct);
        }

        await _db.SensorReadings.Indexes.CreateOneAsync(
            new CreateIndexModel<SensorReading>(
                Builders<SensorReading>.IndexKeys.Ascending(x => x.Timestamp),
                new CreateIndexOptions
                {
                    Name = indexName,
                    ExpireAfter = TimeSpan.FromSeconds(desiredSeconds)
                }),
            cancellationToken: ct);
    }

    private static bool IsTimestampTtlIndex(BsonDocument index)
    {
        if (!index.TryGetValue("expireAfterSeconds", out _)) return false;
        if (!index.TryGetValue("key", out var keyValue) || !keyValue.IsBsonDocument) return false;
        var key = keyValue.AsBsonDocument;
        return key.ElementCount == 1 && key.TryGetValue(nameof(SensorReading.Timestamp), out var direction) &&
               direction.IsNumeric && direction.ToDouble() == 1d;
    }

    private async Task SynchronizeAlarmRulesAsync(CancellationToken ct)
    {
        foreach (var (metric, threshold) in _thresholds.Metrics)
        {
            var rule = new AlarmRule
            {
                Id = $"default:{metric}",
                PondId = "*",
                Metric = metric,
                IsEnabled = true,
                WarningLow = threshold.WarningLow,
                WarningHigh = threshold.WarningHigh,
                CriticalLow = threshold.CriticalLow,
                CriticalHigh = threshold.CriticalHigh,
                Hysteresis = threshold.Hysteresis,
                DelaySeconds = threshold.DelaySeconds,
                CooldownSeconds = threshold.CooldownSeconds,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _db.AlarmRules.ReplaceOneAsync(x => x.Id == rule.Id, rule,
                new ReplaceOptions { IsUpsert = true }, ct);
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        var path = Path.Combine(_environment.ContentRootPath, "Config", "seed-data.json");
        var json = await File.ReadAllTextAsync(path, ct);
        var seed = JsonSerializer.Deserialize<FarmSeedOptions>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Không đọc được Config/seed-data.json.");

        var ponds = seed.Ponds.Select(x => new Pond
        {
            Id = x.Id,
            FarmId = seed.FarmId,
            Name = x.Name,
            Species = x.Species,
            StockingDate = x.StockingDate,
            StockingDensity = x.StockingDensity,
            Area = x.Area,
            EstimatedHarvestDate = x.EstimatedHarvestDate,
            Status = x.Status,
            UpdatedAtUtc = DateTime.UtcNow
        }).ToList();

        var devices = seed.Devices.Select(x => new Device
        {
            Id = x.Id,
            PondId = x.PondId,
            Name = x.Name,
            Type = x.Type,
            Status = x.Status,
            Mode = x.Mode,
            PowerKw = x.PowerKw,
            ControlTransport = "simulation",
            FailSafeState = "off",
            ConnectionStatus = "online",
            LastSeenAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }).ToList();

        var feeds = seed.FeedLogs.Select(x => new FeedLog
        {
            Id = x.Id,
            PondId = x.PondId,
            PondName = ponds.FirstOrDefault(p => p.Id == x.PondId)?.Name ?? x.PondId,
            Timestamp = x.TimestampUtc,
            FeedType = x.FeedType,
            AmountKg = x.AmountKg,
            Technician = x.Technician,
            Notes = x.Notes
        }).ToList();

        var sensors = new List<SensorDefinition>();
        var readings = new List<SensorReading>();
        var latest = new List<TelemetryData>();
        var now = DateTime.UtcNow;

        foreach (var pond in ponds)
        {
            var telemetry = new TelemetryData
            {
                Id = pond.Id,
                PondId = pond.Id,
                Timestamp = now,
                LastSeenAtUtc = now,
                IsOnline = true,
                Quality = "good",
                SourceType = "simulation"
            };

            foreach (var pair in _thresholds.Metrics)
            {
                var metric = pair.Key;
                var threshold = pair.Value;
                var profile = _simulation.Metrics[metric];
                var offset = _simulation.PondMetricOffsets.TryGetValue(pond.Id, out var pondOffsets) &&
                             pondOffsets.TryGetValue(metric, out var configuredOffset)
                    ? configuredOffset
                    : 0;
                var value = Math.Round(profile.BaseValue + offset, profile.DecimalPlaces);
                var sensor = new SensorDefinition
                {
                    Id = $"sensor_{pond.Id}_{metric}",
                    PondId = pond.Id,
                    Name = $"{threshold.DisplayName} · {pond.Name}",
                    Metric = metric,
                    Unit = threshold.Unit,
                    SourceType = "simulation",
                    ExternalAddress = $"simulation://{pond.Id}/{metric}",
                    Status = "online",
                    LastSeenAtUtc = now,
                    FirmwareVersion = _simulation.FirmwareVersion,
                    CalibrationDateUtc = now,
                    CalibrationDueDateUtc = now.AddDays(_behavior.DefaultCalibrationIntervalDays),
                    MinValidValue = threshold.ValidMin,
                    MaxValidValue = threshold.ValidMax,
                    LastQuality = "good",
                    UpdatedAtUtc = now
                };
                sensors.Add(sensor);
                readings.Add(new SensorReading
                {
                    Metadata = new SensorReadingMetadata
                    {
                        PondId = pond.Id,
                        SensorId = sensor.Id,
                        Metric = metric,
                        SourceType = "simulation"
                    },
                    Value = value,
                    Unit = threshold.Unit,
                    Quality = "good",
                    Timestamp = now,
                    ReceivedAtUtc = now,
                    FirmwareVersion = _simulation.FirmwareVersion
                });
                ApplyMetric(telemetry, metric, value);
            }

            latest.Add(telemetry);
        }

        if (ponds.Count > 0) await _db.Ponds.InsertManyAsync(ponds, cancellationToken: ct);
        if (devices.Count > 0) await _db.Devices.InsertManyAsync(devices, cancellationToken: ct);
        if (feeds.Count > 0) await _db.FeedLogs.InsertManyAsync(feeds, cancellationToken: ct);
        if (sensors.Count > 0) await _db.Sensors.InsertManyAsync(sensors, cancellationToken: ct);
        if (readings.Count > 0) await _db.SensorReadings.InsertManyAsync(readings, cancellationToken: ct);
        if (latest.Count > 0) await _db.LatestTelemetry.InsertManyAsync(latest, cancellationToken: ct);

        _logger.LogInformation("Đã tạo dữ liệu MongoDB ban đầu cho {PondCount} ao và {SensorCount} cảm biến.", ponds.Count, sensors.Count);
    }

    private async Task EnsureProductionCycleSeedAsync(CancellationToken ct)
    {
        if (!_cycleSimulation.Enabled || await _db.ProductionCycles.CountDocumentsAsync(FilterDefinition<ProductionCycle>.Empty, cancellationToken: ct) > 0) return;
        var ponds = await _db.Ponds.Find(x => x.Status == "active").ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var pond in ponds)
        {
            if (!_cycleSimulation.Species.TryGetValue(pond.Species, out var profile))
                profile = _cycleSimulation.Species.Values.FirstOrDefault() ?? new CycleSpeciesSimulationOptions();
            var age = Math.Max(1, DateOnly.FromDateTime(DateTime.Today).DayNumber - pond.StockingDate.DayNumber);
            var initialQuantity = Math.Max(1L, (long)Math.Round(pond.Area * pond.StockingDensity));
            var survival = Math.Clamp(100 - age * profile.DailySurvivalLossPercent, 40, 100);
            var weight = Math.Max(profile.InitialWeightGrams, profile.InitialWeightGrams + age * profile.DailyGrowthGrams);
            var biomass = initialQuantity * survival / 100d * weight / 1000d;
            var cycle = new ProductionCycle
            {
                PondId = pond.Id, Code = $"{pond.Id.ToUpperInvariant()}-{pond.StockingDate:yyyyMMdd}", Species = pond.Species,
                SeedBatch = "SIM-SEED", PreparationStartedDate = pond.StockingDate.AddDays(-14), StockingDate = pond.StockingDate,
                InitialQuantity = initialQuantity, InitialAverageWeightGrams = profile.InitialWeightGrams,
                StockingDensityPerM2 = pond.StockingDensity, ExpectedHarvestDate = pond.EstimatedHarvestDate,
                Status = "growing", EstimatedSurvivalRatePercent = survival, LatestAverageWeightGrams = weight,
                EstimatedBiomassKg = biomass, FeedConversionRatio = profile.ExpectedFcr,
                Notes = "Vụ nuôi mô phỏng được tạo từ dữ liệu ao ban đầu.", CreatedAtUtc = now, UpdatedAtUtc = now
            };
            await _db.ProductionCycles.InsertOneAsync(cycle, cancellationToken: ct);
            await _db.GrowthSamples.InsertOneAsync(new GrowthSample
            {
                CycleId = cycle.Id, PondId = pond.Id, SampledAtUtc = now, SampleSize = profile.DefaultSampleSize,
                AverageWeightGrams = Math.Round(weight, 2), EstimatedSurvivalRatePercent = Math.Round(survival, 2),
                EstimatedBiomassKg = Math.Round(biomass, 2), SampledBy = "Mongo seed", Source = "simulation",
                Notes = "Giá trị mô phỏng ban đầu; thay bằng lấy mẫu thực tế khi vận hành."
            }, cancellationToken: ct);
        }
        _logger.LogInformation("Đã tạo {Count} vụ nuôi mô phỏng ban đầu.", ponds.Count);
    }

    public static void ApplyMetric(TelemetryData telemetry, string metric, double value)
    {
        switch (metric)
        {
            case "temperature": telemetry.Temperature = value; break;
            case "ph": telemetry.Ph = value; break;
            case "dissolvedOxygen": telemetry.DissolvedOxygen = value; break;
            case "salinity": telemetry.Salinity = value; break;
            case "ammonia": telemetry.Ammonia = value; break;
            case "waterLevel": telemetry.WaterLevel = value; break;
        }
    }
}
