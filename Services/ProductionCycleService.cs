using System.Data;
using System.Security.Claims;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Production-cycle writes, derived metrics and audit rows share PostgreSQL transactions. This
/// prevents partial events such as mortality being stored without updating cycle totals.
/// </summary>
public sealed class ProductionCycleService(
    IDbContextFactory<AquaDbContext> factory,
    FarmAccessService access,
    FarmRuntimeState runtime)
{
    public async Task<List<ProductionCycle>> GetAllAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await db.ProductionCycles.AsNoTracking().OrderByDescending(x => x.StockingDate).ToListAsync(ct);
        return items.Where(x => access.CanAccessPond(user, x.PondId)).ToList();
    }

    public async Task<List<ProductionCycle>> GetForPondAsync(string pondId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        access.EnsureCanViewPond(user, pondId);
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ProductionCycles.AsNoTracking().Where(x => x.PondId == pondId)
            .OrderByDescending(x => x.StockingDate).ToListAsync(ct);
    }

    public async Task<ProductionCycle?> GetActiveForPondAsync(string pondId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.ProductionCycles.AsNoTracking().Where(x => x.PondId == pondId && x.IsOpen)
            .OrderByDescending(x => x.StockingDate).FirstOrDefaultAsync(ct);
    }

    public async Task<ProductionCycleSnapshot?> GetSnapshotAsync(string id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var cycle = await db.ProductionCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (cycle is null) return null;
        access.EnsureCanViewPond(user, cycle.PondId);
        var pond = await db.Ponds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cycle.PondId, ct);
        return pond is null ? null : new ProductionCycleSnapshot
        {
            Cycle = cycle, Pond = pond,
            Samples = await db.GrowthSamples.AsNoTracking().Where(x => x.CycleId == id).OrderByDescending(x => x.SampledAtUtc).ToListAsync(ct),
            MortalityRecords = await db.MortalityRecords.AsNoTracking().Where(x => x.CycleId == id).OrderByDescending(x => x.RecordedAtUtc).ToListAsync(ct),
            HarvestRecords = await db.HarvestRecords.AsNoTracking().Where(x => x.CycleId == id).OrderByDescending(x => x.HarvestedAtUtc).ToListAsync(ct)
        };
    }

    public async Task<ProductionCycle> CreateAsync(CreateProductionCycleRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        access.EnsureCanManageFarm(user); access.EnsureCanViewPond(user, request.PondId); ValidateCreateRequest(request);
        var cycle = new ProductionCycle
        {
            PondId = request.PondId, Code = request.Code.Trim(), Species = request.Species.Trim(),
            SeedBatch = request.SeedBatch.Trim(), PreparationStartedDate = request.PreparationStartedDate,
            StockingDate = request.StockingDate, InitialQuantity = request.InitialQuantity,
            InitialAverageWeightGrams = request.InitialAverageWeightGrams,
            StockingDensityPerM2 = request.StockingDensityPerM2, ExpectedHarvestDate = request.ExpectedHarvestDate,
            Status = request.Status, IsOpen = true, LatestAverageWeightGrams = request.InitialAverageWeightGrams,
            EstimatedBiomassKg = request.InitialQuantity * request.InitialAverageWeightGrams / 1000d, Notes = request.Notes
        };
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.ProductionCycles.Add(cycle);
        db.AuditLogs.Add(Audit(FarmAccessService.ActorName(user), "production_cycle.create", cycle.Id, cycle.PondId, cycle.Code));
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new InvalidOperationException("Ao đang có một vụ nuôi chưa kết thúc hoặc mã vụ đã tồn tại.", ex); }
        runtime.UpsertProductionCycle(cycle);
        return cycle;
    }

    public async Task AddSampleAsync(GrowthSample sample, ClaimsPrincipal user, CancellationToken ct = default)
    {
        ValidateSample(sample);
        await WriteCycleEventAsync(sample.CycleId, sample.PondId, user, async (db, cycle) =>
        {
            sample.Source = string.IsNullOrWhiteSpace(sample.Source) ? "manual" : sample.Source;
            db.GrowthSamples.Add(sample);
            db.AuditLogs.Add(Audit(FarmAccessService.ActorName(user), "production_cycle.sample", sample.CycleId,
                sample.PondId, $"{sample.AverageWeightGrams} g"));
            await Task.CompletedTask;
        }, ct);
    }

    public async Task AddMortalityAsync(MortalityRecord record, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (record.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(record.Quantity), "Hao hụt phải lớn hơn 0.");
        if (record.RecordedAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new InvalidOperationException("Thời gian hao hụt không được ở tương lai.");
        await WriteCycleEventAsync(record.CycleId, record.PondId, user, async (db, cycle) =>
        {
            await EnsureAnimalsAvailableAsync(db, cycle, record.Quantity, ct);
            db.MortalityRecords.Add(record);
            db.AuditLogs.Add(Audit(FarmAccessService.ActorName(user), "production_cycle.mortality", record.CycleId,
                record.PondId, record.Quantity.ToString()));
        }, ct);
    }

    public async Task AddHarvestAsync(HarvestRecord record, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (record.QuantityKg <= 0) throw new ArgumentOutOfRangeException(nameof(record.QuantityKg), "Khối lượng thu hoạch phải lớn hơn 0.");
        if (record.AnimalQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(record.AnimalQuantity), "Số con thu hoạch phải lớn hơn 0.");
        if (record.HarvestedAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new InvalidOperationException("Thời gian thu hoạch không được ở tương lai.");
        await WriteCycleEventAsync(record.CycleId, record.PondId, user, async (db, cycle) =>
        {
            await EnsureAnimalsAvailableAsync(db, cycle, record.AnimalQuantity, ct);
            db.HarvestRecords.Add(record);
            db.AuditLogs.Add(Audit(FarmAccessService.ActorName(user), "production_cycle.harvest", record.CycleId,
                record.PondId, record.QuantityKg.ToString("0.##")));
        }, ct);
    }

    public async Task SetStatusAsync(string id, string status, ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var cycle = await db.ProductionCycles.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Không tìm thấy vụ nuôi.");
        access.EnsureCanManageFarm(user); access.EnsureCanViewPond(user, cycle.PondId);
        ValidateStatusTransition(cycle.Status, status);
        var closed = status is "completed" or "cancelled";
        cycle.Status = status; cycle.IsOpen = !closed; cycle.UpdatedAtUtc = DateTime.UtcNow;
        if (status == "completed") cycle.ActualHarvestDate = DateOnly.FromDateTime(DateTime.Today);
        db.AuditLogs.Add(Audit(FarmAccessService.ActorName(user), "production_cycle.status", id, cycle.PondId, status));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        runtime.UpsertProductionCycle(cycle);
    }

    public async Task RecalculateAsync(string cycleId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var cycle = await db.ProductionCycles.FirstOrDefaultAsync(x => x.Id == cycleId, ct);
        if (cycle is null) return;
        await RecalculateCoreAsync(db, cycle, ct);
        await db.SaveChangesAsync(ct);
        runtime.UpsertProductionCycle(cycle);
    }

    public async Task RecalculateAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        foreach (var cycle in await db.ProductionCycles.ToListAsync(ct)) await RecalculateCoreAsync(db, cycle, ct);
        await db.SaveChangesAsync(ct);
        foreach (var cycle in db.ProductionCycles.Local) runtime.UpsertProductionCycle(cycle);
    }

    private async Task WriteCycleEventAsync(string cycleId, string pondId, ClaimsPrincipal user,
        Func<AquaDbContext, ProductionCycle, Task> append, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var cycle = await EnsureRecordCycleAsync(db, cycleId, pondId, user, ct);
        await append(db, cycle);
        await db.SaveChangesAsync(ct);
        await RecalculateCoreAsync(db, cycle, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        runtime.UpsertProductionCycle(cycle);
    }

    private async Task RecalculateCoreAsync(AquaDbContext db, ProductionCycle cycle, CancellationToken ct)
    {
        var samples = await db.GrowthSamples.Where(x => x.CycleId == cycle.Id).OrderByDescending(x => x.SampledAtUtc).ToListAsync(ct);
        var latest = samples.FirstOrDefault();
        var dailyGrowth = 0d;
        if (samples.Count >= 2)
        {
            var days = (samples[0].SampledAtUtc - samples[1].SampledAtUtc).TotalDays;
            if (days > 0) dailyGrowth = (samples[0].EstimatedBiomassKg - samples[1].EstimatedBiomassKg) / days;
        }
        var metrics = ProductionMetricsCalculator.Calculate(cycle, latest,
            await db.MortalityRecords.Where(x => x.CycleId == cycle.Id).ToListAsync(ct),
            await db.HarvestRecords.Where(x => x.CycleId == cycle.Id).ToListAsync(ct),
            await db.FeedLogs.Where(x => x.CycleId == cycle.Id).ToListAsync(ct));
        cycle.MortalityQuantity = metrics.MortalityQuantity; cycle.HarvestedAnimalQuantity = metrics.HarvestedAnimalQuantity;
        cycle.EstimatedSurvivalRatePercent = metrics.SurvivalRatePercent; cycle.LatestAverageWeightGrams = metrics.AverageWeightGrams;
        cycle.EstimatedBiomassKg = metrics.BiomassKg; cycle.LatestSampleAtUtc = latest?.SampledAtUtc;
        cycle.GrowthSampleCount = samples.Count; cycle.EstimatedDailyBiomassGrowthKg = dailyGrowth;
        cycle.TotalFeedKg = metrics.TotalFeedKg; cycle.HarvestQuantityKg = metrics.HarvestedKg;
        cycle.FeedConversionRatio = metrics.Fcr; cycle.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task<ProductionCycle> EnsureRecordCycleAsync(AquaDbContext db, string cycleId, string pondId,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var cycle = await db.ProductionCycles.FirstOrDefaultAsync(x => x.Id == cycleId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy vụ nuôi.");
        if (!cycle.PondId.Equals(pondId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bản ghi không thuộc ao của vụ nuôi.");
        if (!cycle.IsOpen) throw new InvalidOperationException("Vụ nuôi đã đóng, không thể thêm bản ghi.");
        access.EnsureCanOperatePond(user, cycle.PondId);
        return cycle;
    }

    private static async Task EnsureAnimalsAvailableAsync(AquaDbContext db, ProductionCycle cycle, long additional, CancellationToken ct)
    {
        var mortality = await db.MortalityRecords.Where(x => x.CycleId == cycle.Id).SumAsync(x => (long?)x.Quantity, ct) ?? 0;
        var harvest = await db.HarvestRecords.Where(x => x.CycleId == cycle.Id).SumAsync(x => (long?)x.AnimalQuantity, ct) ?? 0;
        if (mortality + harvest + additional > cycle.InitialQuantity)
            throw new InvalidOperationException("Tổng hao hụt và số con thu hoạch vượt số lượng đã thả.");
    }

    private static AuditLog Audit(string actor, string action, string id, string pond, string value) => new()
    { Actor = actor, Action = action, EntityType = "ProductionCycle", EntityId = id, PondId = pond, NewValue = value };

    private static void ValidateCreateRequest(CreateProductionCycleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PondId) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Species))
            throw new ArgumentException("Ao, mã vụ và loài nuôi là bắt buộc.");
        if (request.Status is not ("preparing" or "stocking" or "growing")) throw new ArgumentException("Trạng thái khởi tạo vụ nuôi không hợp lệ.");
        if (request.InitialQuantity <= 0 || request.InitialAverageWeightGrams <= 0 || request.StockingDensityPerM2 <= 0) throw new ArgumentException("Thông tin thả giống phải lớn hơn 0.");
        if (request.PreparationStartedDate > request.StockingDate) throw new ArgumentException("Ngày chuẩn bị phải trước hoặc bằng ngày thả.");
        if (request.ExpectedHarvestDate <= request.StockingDate) throw new ArgumentException("Ngày thu hoạch dự kiến phải sau ngày thả.");
        if (request.StockingDate > DateOnly.FromDateTime(DateTime.Today.AddDays(1))) throw new ArgumentException("Ngày thả không được ở tương lai xa.");
    }

    private static void ValidateSample(GrowthSample sample)
    {
        if (sample.SampleSize <= 0 || sample.AverageWeightGrams <= 0 || sample.EstimatedBiomassKg < 0) throw new ArgumentException("Mẫu tăng trưởng có giá trị không hợp lệ.");
        if (sample.EstimatedSurvivalRatePercent is < 0 or > 100) throw new ArgumentException("Tỷ lệ sống phải trong 0–100%.");
        if (sample.SampledAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new ArgumentException("Thời gian lấy mẫu không được ở tương lai.");
    }

    private static void ValidateStatusTransition(string current, string next)
    {
        var transitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["preparing"] = ["stocking", "cancelled"], ["stocking"] = ["growing", "cancelled"],
            ["growing"] = ["harvesting", "cancelled"], ["harvesting"] = ["completed", "growing", "cancelled"],
            ["completed"] = [], ["cancelled"] = []
        };
        if (!transitions.TryGetValue(current, out var allowed) || !allowed.Contains(next, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Không thể chuyển trạng thái từ '{current}' sang '{next}'.");
    }
}
