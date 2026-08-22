using System.Text.Json;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Data;

/// <summary>
/// Applies versioned EF migrations and creates only the clean operational baseline: one pond,
/// no sensors and no devices unless Config/seed-data.json explicitly contains them.
/// </summary>
public sealed class PostgreSqlDatabaseInitializer(
    IDbContextFactory<AquaDbContext> factory,
    IOptions<PostgreSqlOptions> databaseOptions,
    IOptions<SensorThresholdCatalog> thresholds,
    IWebHostEnvironment environment,
    ILogger<PostgreSqlDatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (databaseOptions.Value.AutoMigrate) await db.Database.MigrateAsync(ct);
        else if (!await db.Database.CanConnectAsync(ct))
            throw new InvalidOperationException("Không thể kết nối PostgreSQL và AutoMigrate đang tắt.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Ponds.AnyAsync(ct)) await SeedFarmAsync(db, ct);
        await SynchronizeDefaultAlarmRulesAsync(db, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("PostgreSQL schema và dữ liệu baseline đã sẵn sàng.");
    }

    private async Task SeedFarmAsync(AquaDbContext db, CancellationToken ct)
    {
        var path = Path.Combine(environment.ContentRootPath, "Config", "seed-data.json");
        await using var stream = File.OpenRead(path);
        var seed = await JsonSerializer.DeserializeAsync<FarmSeedOptions>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true }, ct)
            ?? throw new InvalidOperationException("Không đọc được Config/seed-data.json.");

        var ponds = seed.Ponds.Select(x => new Pond
        {
            Id = x.Id, FarmId = seed.FarmId, Name = x.Name, Species = x.Species,
            StockingDate = x.StockingDate, StockingDensity = x.StockingDensity, Area = x.Area,
            EstimatedHarvestDate = x.EstimatedHarvestDate, Status = x.Status, UpdatedAtUtc = DateTime.UtcNow
        }).ToList();
        db.Ponds.AddRange(ponds);
        db.Devices.AddRange(seed.Devices.Select(x => new Device
        {
            Id = x.Id, PondId = x.PondId, Name = x.Name, Type = x.Type, Status = x.Status,
            Mode = x.Mode, PowerKw = x.PowerKw, ControlTransport = x.ControlTransport,
            ExternalAddress = x.ExternalAddress, FailSafeState = "off", ConnectionStatus = "offline",
            UpdatedAtUtc = DateTime.UtcNow
        }));
        db.FeedLogs.AddRange(seed.FeedLogs.Select(x => new FeedLog
        {
            Id = x.Id, PondId = x.PondId,
            PondName = ponds.FirstOrDefault(p => p.Id == x.PondId)?.Name ?? x.PondId,
            Timestamp = x.TimestampUtc, FeedType = x.FeedType, AmountKg = x.AmountKg,
            Technician = x.Technician, Notes = x.Notes
        }));
        logger.LogInformation("Đã tạo baseline PostgreSQL cho {PondCount} ao; không tạo card cảm biến/thiết bị mặc định.", ponds.Count);
    }

    private async Task SynchronizeDefaultAlarmRulesAsync(AquaDbContext db, CancellationToken ct)
    {
        foreach (var (metric, threshold) in thresholds.Value.Metrics)
        {
            var id = $"default:{metric}";
            var rule = await db.AlarmRules.FindAsync([id], ct);
            if (rule is null)
            {
                rule = new AlarmRule { Id = id, PondId = "*", Metric = metric };
                db.AlarmRules.Add(rule);
            }
            rule.IsEnabled = true;
            rule.WarningLow = threshold.WarningLow; rule.WarningHigh = threshold.WarningHigh;
            rule.CriticalLow = threshold.CriticalLow; rule.CriticalHigh = threshold.CriticalHigh;
            rule.Hysteresis = threshold.Hysteresis; rule.DelaySeconds = threshold.DelaySeconds;
            rule.CooldownSeconds = threshold.CooldownSeconds; rule.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
