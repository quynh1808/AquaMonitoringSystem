using System.Security.Claims;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using MongoDB.Driver;

namespace AquacultureMonitoring.Services;

public sealed class ProductionCycleService
{
    private readonly MongoContext _db;
    private readonly FarmAccessService _access;
    public ProductionCycleService(MongoContext db, FarmAccessService access) { _db = db; _access = access; }

    public async Task<List<ProductionCycle>> GetAllAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var items = await _db.ProductionCycles.Find(FilterDefinition<ProductionCycle>.Empty).SortByDescending(x => x.StockingDate).ToListAsync(ct);
        return items.Where(x => _access.CanAccessPond(user, x.PondId)).ToList();
    }

    public async Task<List<ProductionCycle>> GetForPondAsync(string pondId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        _access.EnsureCanViewPond(user, pondId);
        return await _db.ProductionCycles.Find(x => x.PondId == pondId).SortByDescending(x => x.StockingDate).ToListAsync(ct);
    }

    public async Task<ProductionCycleSnapshot?> GetSnapshotAsync(string id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var cycle = await _db.ProductionCycles.Find(x => x.Id == id).FirstOrDefaultAsync(ct); if (cycle is null) return null;
        _access.EnsureCanViewPond(user, cycle.PondId);
        var pondTask = _db.Ponds.Find(x => x.Id == cycle.PondId).FirstOrDefaultAsync(ct);
        var samplesTask = _db.GrowthSamples.Find(x => x.CycleId == id).SortByDescending(x => x.SampledAtUtc).ToListAsync(ct);
        var mortalityTask = _db.MortalityRecords.Find(x => x.CycleId == id).SortByDescending(x => x.RecordedAtUtc).ToListAsync(ct);
        var harvestTask = _db.HarvestRecords.Find(x => x.CycleId == id).SortByDescending(x => x.HarvestedAtUtc).ToListAsync(ct);
        await Task.WhenAll(pondTask, samplesTask, mortalityTask, harvestTask);
        var pond = await pondTask; if (pond is null) return null;
        return new ProductionCycleSnapshot { Cycle = cycle, Pond = pond, Samples = await samplesTask, MortalityRecords = await mortalityTask, HarvestRecords = await harvestTask };
    }

    public async Task<ProductionCycle> CreateAsync(CreateProductionCycleRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        _access.EnsureCanManageFarm(user);
        _access.EnsureCanViewPond(user, request.PondId);
        var actor = FarmAccessService.ActorName(user);
        if (await _db.ProductionCycles.CountDocumentsAsync(x => x.PondId == request.PondId && x.Status != "completed" && x.Status != "cancelled", cancellationToken: ct) > 0) throw new InvalidOperationException("Ao đang có một vụ nuôi chưa kết thúc.");
        var cycle = new ProductionCycle { PondId=request.PondId, Code=request.Code.Trim(), Species=request.Species.Trim(), SeedBatch=request.SeedBatch.Trim(), PreparationStartedDate=request.PreparationStartedDate, StockingDate=request.StockingDate, InitialQuantity=request.InitialQuantity, InitialAverageWeightGrams=request.InitialAverageWeightGrams, StockingDensityPerM2=request.StockingDensityPerM2, ExpectedHarvestDate=request.ExpectedHarvestDate, Status=request.Status, LatestAverageWeightGrams=request.InitialAverageWeightGrams, EstimatedBiomassKg=request.InitialQuantity*request.InitialAverageWeightGrams/1000d, Notes=request.Notes };
        await _db.ProductionCycles.InsertOneAsync(cycle, cancellationToken: ct);
        await _db.AuditLogs.InsertOneAsync(new AuditLog { Actor=actor, Action="production_cycle.create", EntityType="ProductionCycle", EntityId=cycle.Id, PondId=cycle.PondId, NewValue=cycle.Code }, cancellationToken: ct);
        return cycle;
    }

    public Task AddSampleAsync(GrowthSample sample, ClaimsPrincipal user, CancellationToken ct = default)
    { _access.EnsureCanOperatePond(user, sample.PondId); return AddSampleCoreAsync(sample, FarmAccessService.ActorName(user), ct); }
    public async Task AddMortalityAsync(MortalityRecord record, ClaimsPrincipal user, CancellationToken ct = default)
    { _access.EnsureCanOperatePond(user, record.PondId); await _db.MortalityRecords.InsertOneAsync(record,cancellationToken:ct); await RecalculateAsync(record.CycleId,ct); await AuditAsync(FarmAccessService.ActorName(user),"production_cycle.mortality",record.CycleId,record.PondId,record.Quantity.ToString(),ct); }
    public async Task AddHarvestAsync(HarvestRecord record, ClaimsPrincipal user, CancellationToken ct = default)
    { _access.EnsureCanOperatePond(user, record.PondId); await _db.HarvestRecords.InsertOneAsync(record,cancellationToken:ct); await RecalculateAsync(record.CycleId,ct); await AuditAsync(FarmAccessService.ActorName(user),"production_cycle.harvest",record.CycleId,record.PondId,record.QuantityKg.ToString("0.##"),ct); }
    public async Task SetStatusAsync(string id,string status,ClaimsPrincipal user,CancellationToken ct=default)
    { var cycle=await _db.ProductionCycles.Find(x=>x.Id==id).FirstOrDefaultAsync(ct)??throw new InvalidOperationException("Không tìm thấy vụ nuôi."); _access.EnsureCanManageFarm(user); _access.EnsureCanViewPond(user,cycle.PondId); var update=Builders<ProductionCycle>.Update.Set(x=>x.Status,status).Set(x=>x.UpdatedAtUtc,DateTime.UtcNow); if(status=="completed") update=update.Set(x=>x.ActualHarvestDate,DateOnly.FromDateTime(DateTime.Today)); await _db.ProductionCycles.UpdateOneAsync(x=>x.Id==id,update,cancellationToken:ct); await AuditAsync(FarmAccessService.ActorName(user),"production_cycle.status",id,cycle.PondId,status,ct); }

    /// <summary>Trusted worker entry point; simulation never receives a user principal.</summary>
    public Task AddSimulatedSampleAsync(GrowthSample sample, CancellationToken ct = default) =>
        AddSampleCoreAsync(sample, "simulation", ct);

    private async Task AddSampleCoreAsync(GrowthSample sample, string actor, CancellationToken ct)
    { sample.Source=string.IsNullOrWhiteSpace(sample.Source)?"manual":sample.Source; await _db.GrowthSamples.InsertOneAsync(sample, cancellationToken:ct); await RecalculateAsync(sample.CycleId, ct); await AuditAsync(actor,"production_cycle.sample",sample.CycleId,sample.PondId,$"{sample.AverageWeightGrams} g",ct); }

    public async Task RecalculateAsync(string cycleId, CancellationToken ct=default)
    {
        var cycle = await _db.ProductionCycles.Find(x => x.Id == cycleId).FirstOrDefaultAsync(ct);
        if (cycle is null) return;
        var latest = await _db.GrowthSamples.Find(x => x.CycleId == cycleId).SortByDescending(x => x.SampledAtUtc).FirstOrDefaultAsync(ct);
        var mortalityItems = await _db.MortalityRecords.Find(x => x.CycleId == cycleId).ToListAsync(ct);
        var harvestItems = await _db.HarvestRecords.Find(x => x.CycleId == cycleId).ToListAsync(ct);
        var cycleStartUtc = DateTime.SpecifyKind(cycle.StockingDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var feedItems = await _db.FeedLogs.Find(x => x.PondId == cycle.PondId && x.Timestamp >= cycleStartUtc).ToListAsync(ct);
        var dead = mortalityItems.Sum(x => (double)x.Quantity);
        var alive = Math.Max(0, cycle.InitialQuantity - dead);
        var survival = cycle.InitialQuantity <= 0 ? 0 : alive / cycle.InitialQuantity * 100d;
        var weight = latest?.AverageWeightGrams ?? cycle.LatestAverageWeightGrams;
        var biomass = latest?.EstimatedBiomassKg ?? alive * weight / 1000d;
        var totalFeed = feedItems.Sum(x => x.AmountKg);
        var harvested = harvestItems.Sum(x => x.QuantityKg);
        var fcr = biomass + harvested > 0 ? totalFeed / (biomass + harvested) : 0;
        var update = Builders<ProductionCycle>.Update
            .Set(x => x.MortalityQuantity, dead)
            .Set(x => x.EstimatedSurvivalRatePercent, survival)
            .Set(x => x.LatestAverageWeightGrams, weight)
            .Set(x => x.EstimatedBiomassKg, biomass)
            .Set(x => x.TotalFeedKg, totalFeed)
            .Set(x => x.HarvestQuantityKg, harvested)
            .Set(x => x.FeedConversionRatio, fcr)
            .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
        await _db.ProductionCycles.UpdateOneAsync(x => x.Id == cycleId, update, cancellationToken: ct);
    }
    private Task AuditAsync(string actor,string action,string id,string pond,string value,CancellationToken ct)=>_db.AuditLogs.InsertOneAsync(new AuditLog{Actor=actor,Action=action,EntityType="ProductionCycle",EntityId=id,PondId=pond,NewValue=value},cancellationToken:ct);
}
