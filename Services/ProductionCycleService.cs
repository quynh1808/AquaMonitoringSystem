using System.Security.Claims;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Runtime;
using MongoDB.Driver;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Owns production-cycle validation and metric recalculation. All writes pass through this
/// boundary so cycle, pond and animal-count invariants are enforced consistently.
/// </summary>
public sealed class ProductionCycleService
{
    private readonly MongoContext _db;
    private readonly FarmAccessService _access;
    private readonly FarmRuntimeState _runtime;

    public ProductionCycleService(MongoContext db, FarmAccessService access, FarmRuntimeState runtime)
    {
        _db = db;
        _access = access;
        _runtime = runtime;
    }

    public async Task<List<ProductionCycle>> GetAllAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var items = await _db.ProductionCycles.Find(FilterDefinition<ProductionCycle>.Empty)
            .SortByDescending(x => x.StockingDate).ToListAsync(ct);
        return items.Where(x => _access.CanAccessPond(user, x.PondId)).ToList();
    }

    public async Task<List<ProductionCycle>> GetForPondAsync(string pondId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        _access.EnsureCanViewPond(user, pondId);
        return await _db.ProductionCycles.Find(x => x.PondId == pondId)
            .SortByDescending(x => x.StockingDate).ToListAsync(ct);
    }

    public async Task<ProductionCycle?> GetActiveForPondAsync(string pondId, CancellationToken ct = default) =>
        await _db.ProductionCycles.Find(x => x.PondId == pondId && x.IsOpen)
            .SortByDescending(x => x.StockingDate).FirstOrDefaultAsync(ct);

    public async Task<ProductionCycleSnapshot?> GetSnapshotAsync(string id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var cycle = await _db.ProductionCycles.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (cycle is null) return null;
        _access.EnsureCanViewPond(user, cycle.PondId);
        var pondTask = _db.Ponds.Find(x => x.Id == cycle.PondId).FirstOrDefaultAsync(ct);
        var samplesTask = _db.GrowthSamples.Find(x => x.CycleId == id).SortByDescending(x => x.SampledAtUtc).ToListAsync(ct);
        var mortalityTask = _db.MortalityRecords.Find(x => x.CycleId == id).SortByDescending(x => x.RecordedAtUtc).ToListAsync(ct);
        var harvestTask = _db.HarvestRecords.Find(x => x.CycleId == id).SortByDescending(x => x.HarvestedAtUtc).ToListAsync(ct);
        await Task.WhenAll(pondTask, samplesTask, mortalityTask, harvestTask);
        var pond = await pondTask;
        return pond is null ? null : new ProductionCycleSnapshot
        {
            Cycle = cycle,
            Pond = pond,
            Samples = await samplesTask,
            MortalityRecords = await mortalityTask,
            HarvestRecords = await harvestTask
        };
    }

    public async Task<ProductionCycle> CreateAsync(CreateProductionCycleRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        _access.EnsureCanManageFarm(user);
        _access.EnsureCanViewPond(user, request.PondId);
        ValidateCreateRequest(request);
        var cycle = new ProductionCycle
        {
            PondId = request.PondId,
            Code = request.Code.Trim(),
            Species = request.Species.Trim(),
            SeedBatch = request.SeedBatch.Trim(),
            PreparationStartedDate = request.PreparationStartedDate,
            StockingDate = request.StockingDate,
            InitialQuantity = request.InitialQuantity,
            InitialAverageWeightGrams = request.InitialAverageWeightGrams,
            StockingDensityPerM2 = request.StockingDensityPerM2,
            ExpectedHarvestDate = request.ExpectedHarvestDate,
            Status = request.Status,
            IsOpen = true,
            LatestAverageWeightGrams = request.InitialAverageWeightGrams,
            EstimatedBiomassKg = request.InitialQuantity * request.InitialAverageWeightGrams / 1000d,
            Notes = request.Notes
        };
        try
        {
            await _db.ProductionCycles.InsertOneAsync(cycle, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException("Ao đang có một vụ nuôi chưa kết thúc hoặc mã vụ đã tồn tại.", ex);
        }
        await AuditAsync(FarmAccessService.ActorName(user), "production_cycle.create", cycle.Id, cycle.PondId, cycle.Code, ct);
        _runtime.UpsertProductionCycle(cycle);
        return cycle;
    }

    public async Task AddSampleAsync(GrowthSample sample, ClaimsPrincipal user, CancellationToken ct = default)
    {
        await EnsureRecordCycleAsync(sample.CycleId, sample.PondId, user, ct);
        await AddSampleCoreAsync(sample, FarmAccessService.ActorName(user), ct);
    }

    /// <summary>Trusted worker entry point; simulation never receives a user principal.</summary>
    public Task AddSimulatedSampleAsync(GrowthSample sample, CancellationToken ct = default) =>
        AddSampleCoreAsync(sample, "simulation", ct);

    public async Task AddMortalityAsync(MortalityRecord record, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var cycle = await EnsureRecordCycleAsync(record.CycleId, record.PondId, user, ct);
        if (record.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(record.Quantity), "Hao hụt phải lớn hơn 0.");
        if (record.RecordedAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new InvalidOperationException("Thời gian hao hụt không được ở tương lai.");
        await EnsureAnimalsAvailableAsync(cycle, record.Quantity, ct);
        await _db.MortalityRecords.InsertOneAsync(record, cancellationToken: ct);
        await RecalculateAsync(record.CycleId, ct);
        await AuditAsync(FarmAccessService.ActorName(user), "production_cycle.mortality", record.CycleId, record.PondId, record.Quantity.ToString(), ct);
    }

    public async Task AddHarvestAsync(HarvestRecord record, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var cycle = await EnsureRecordCycleAsync(record.CycleId, record.PondId, user, ct);
        if (record.QuantityKg <= 0) throw new ArgumentOutOfRangeException(nameof(record.QuantityKg), "Khối lượng thu hoạch phải lớn hơn 0.");
        if (record.AnimalQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(record.AnimalQuantity), "Số con thu hoạch đếm hoặc ước tính phải lớn hơn 0.");
        if (record.HarvestedAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new InvalidOperationException("Thời gian thu hoạch không được ở tương lai.");
        await EnsureAnimalsAvailableAsync(cycle, record.AnimalQuantity, ct);
        await _db.HarvestRecords.InsertOneAsync(record, cancellationToken: ct);
        await RecalculateAsync(record.CycleId, ct);
        await AuditAsync(FarmAccessService.ActorName(user), "production_cycle.harvest", record.CycleId, record.PondId, record.QuantityKg.ToString("0.##"), ct);
    }

    public async Task SetStatusAsync(string id, string status, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var cycle = await _db.ProductionCycles.Find(x => x.Id == id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy vụ nuôi.");
        _access.EnsureCanManageFarm(user);
        _access.EnsureCanViewPond(user, cycle.PondId);
        ValidateStatusTransition(cycle.Status, status);
        var closed = status is "completed" or "cancelled";
        var update = Builders<ProductionCycle>.Update.Set(x => x.Status, status).Set(x => x.IsOpen, !closed).Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
        if (status == "completed") update = update.Set(x => x.ActualHarvestDate, DateOnly.FromDateTime(DateTime.Today));
        await _db.ProductionCycles.UpdateOneAsync(x => x.Id == id, update, cancellationToken: ct);
        cycle.Status = status;
        cycle.IsOpen = !closed;
        cycle.UpdatedAtUtc = DateTime.UtcNow;
        if (status == "completed") cycle.ActualHarvestDate = DateOnly.FromDateTime(DateTime.Today);
        _runtime.UpsertProductionCycle(cycle);
        await AuditAsync(FarmAccessService.ActorName(user), "production_cycle.status", id, cycle.PondId, status, ct);
    }

    private async Task AddSampleCoreAsync(GrowthSample sample, string actor, CancellationToken ct)
    {
        ValidateSample(sample);
        sample.Source = string.IsNullOrWhiteSpace(sample.Source) ? "manual" : sample.Source;
        await _db.GrowthSamples.InsertOneAsync(sample, cancellationToken: ct);
        await RecalculateAsync(sample.CycleId, ct);
        await AuditAsync(actor, "production_cycle.sample", sample.CycleId, sample.PondId, $"{sample.AverageWeightGrams} g", ct);
    }

    public async Task RecalculateAsync(string cycleId, CancellationToken ct = default)
    {
        var cycle = await _db.ProductionCycles.Find(x => x.Id == cycleId).FirstOrDefaultAsync(ct);
        if (cycle is null) return;
        var samples = await _db.GrowthSamples.Find(x => x.CycleId == cycleId).SortByDescending(x => x.SampledAtUtc).ToListAsync(ct);
        var latest = samples.FirstOrDefault();
        var dailyGrowth = 0d;
        if (samples.Count >= 2)
        {
            var days = (samples[0].SampledAtUtc - samples[1].SampledAtUtc).TotalDays;
            if (days > 0) dailyGrowth = (samples[0].EstimatedBiomassKg - samples[1].EstimatedBiomassKg) / days;
        }
        var metrics = ProductionMetricsCalculator.Calculate(cycle, latest,
            await _db.MortalityRecords.Find(x => x.CycleId == cycleId).ToListAsync(ct),
            await _db.HarvestRecords.Find(x => x.CycleId == cycleId).ToListAsync(ct),
            await _db.FeedLogs.Find(x => x.CycleId == cycleId).ToListAsync(ct));
        var now = DateTime.UtcNow;
        var update = Builders<ProductionCycle>.Update
            .Set(x => x.MortalityQuantity, metrics.MortalityQuantity)
            .Set(x => x.HarvestedAnimalQuantity, metrics.HarvestedAnimalQuantity)
            .Set(x => x.EstimatedSurvivalRatePercent, metrics.SurvivalRatePercent)
            .Set(x => x.LatestAverageWeightGrams, metrics.AverageWeightGrams)
            .Set(x => x.EstimatedBiomassKg, metrics.BiomassKg)
            .Set(x => x.LatestSampleAtUtc, latest == null ? null : latest.SampledAtUtc)
            .Set(x => x.GrowthSampleCount, samples.Count)
            .Set(x => x.EstimatedDailyBiomassGrowthKg, dailyGrowth)
            .Set(x => x.TotalFeedKg, metrics.TotalFeedKg)
            .Set(x => x.HarvestQuantityKg, metrics.HarvestedKg)
            .Set(x => x.FeedConversionRatio, metrics.Fcr)
            .Set(x => x.UpdatedAtUtc, now);
        await _db.ProductionCycles.UpdateOneAsync(x => x.Id == cycleId, update, cancellationToken: ct);
        cycle.MortalityQuantity = metrics.MortalityQuantity;
        cycle.HarvestedAnimalQuantity = metrics.HarvestedAnimalQuantity;
        cycle.EstimatedSurvivalRatePercent = metrics.SurvivalRatePercent;
        cycle.LatestAverageWeightGrams = metrics.AverageWeightGrams;
        cycle.EstimatedBiomassKg = metrics.BiomassKg;
        cycle.LatestSampleAtUtc = latest?.SampledAtUtc;
        cycle.GrowthSampleCount = samples.Count;
        cycle.EstimatedDailyBiomassGrowthKg = dailyGrowth;
        cycle.TotalFeedKg = metrics.TotalFeedKg;
        cycle.HarvestQuantityKg = metrics.HarvestedKg;
        cycle.FeedConversionRatio = metrics.Fcr;
        cycle.UpdatedAtUtc = now;
        _runtime.UpsertProductionCycle(cycle);
    }

    public async Task RecalculateAllAsync(CancellationToken ct = default)
    {
        foreach (var id in await _db.ProductionCycles.Find(FilterDefinition<ProductionCycle>.Empty).Project(x => x.Id).ToListAsync(ct))
            await RecalculateAsync(id, ct);
    }

    private async Task<ProductionCycle> EnsureRecordCycleAsync(string cycleId, string pondId, ClaimsPrincipal user, CancellationToken ct)
    {
        var cycle = await _db.ProductionCycles.Find(x => x.Id == cycleId).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Không tìm thấy vụ nuôi.");
        if (!cycle.PondId.Equals(pondId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bản ghi không thuộc ao của vụ nuôi.");
        if (!cycle.IsOpen) throw new InvalidOperationException("Vụ nuôi đã đóng, không thể thêm bản ghi.");
        _access.EnsureCanOperatePond(user, cycle.PondId);
        return cycle;
    }

    private async Task EnsureAnimalsAvailableAsync(ProductionCycle cycle, long additional, CancellationToken ct)
    {
        var mortality = await _db.MortalityRecords.Find(x => x.CycleId == cycle.Id).ToListAsync(ct);
        var harvest = await _db.HarvestRecords.Find(x => x.CycleId == cycle.Id).ToListAsync(ct);
        if (mortality.Sum(x => x.Quantity) + harvest.Sum(x => x.AnimalQuantity) + additional > cycle.InitialQuantity)
            throw new InvalidOperationException("Tổng hao hụt và số con thu hoạch vượt số lượng đã thả.");
    }

    private static void ValidateCreateRequest(CreateProductionCycleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PondId) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Species))
            throw new ArgumentException("Ao, mã vụ và loài nuôi là bắt buộc.");
        if (request.Status is not ("preparing" or "stocking" or "growing"))
            throw new ArgumentException("Trạng thái khởi tạo vụ nuôi không hợp lệ.");
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

    private Task AuditAsync(string actor, string action, string id, string pond, string value, CancellationToken ct) =>
        _db.AuditLogs.InsertOneAsync(new AuditLog { Actor = actor, Action = action, EntityType = "ProductionCycle", EntityId = id, PondId = pond, NewValue = value }, cancellationToken: ct);
}
