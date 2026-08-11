using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AquacultureMonitoring.Runtime;

public sealed class ProductionCycleSimulationHostedService : BackgroundService
{
    private readonly MongoContext _db; private readonly ProductionCycleService _cycles; private readonly IOptionsMonitor<ProductionCycleSimulationOptions> _options;
    public ProductionCycleSimulationHostedService(MongoContext db, ProductionCycleService cycles, IOptionsMonitor<ProductionCycleSimulationOptions> options){_db=db;_cycles=cycles;_options=options;}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            if(_options.CurrentValue.Enabled) await GenerateDueSamplesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(Math.Max(1,_options.CurrentValue.RefreshHours)),stoppingToken);
        }
    }
    private async Task GenerateDueSamplesAsync(CancellationToken ct)
    {
        var options=_options.CurrentValue; var cycles=await _db.ProductionCycles.Find(x=>x.Status=="growing"||x.Status=="stocking").ToListAsync(ct);
        foreach(var cycle in cycles)
        {
            var latest=await _db.GrowthSamples.Find(x=>x.CycleId==cycle.Id).SortByDescending(x=>x.SampledAtUtc).FirstOrDefaultAsync(ct);
            if(latest is not null && DateTime.UtcNow-latest.SampledAtUtc<TimeSpan.FromDays(options.SampleEveryDays)) continue;
            if(!options.Species.TryGetValue(cycle.Species,out var profile)) profile=options.Species.Values.FirstOrDefault()??new();
            var age=Math.Max(0,DateOnly.FromDateTime(DateTime.Today).DayNumber-cycle.StockingDate.DayNumber); var random=new Random(HashCode.Combine(options.RandomSeed,cycle.Id,DateTime.Today.DayOfYear));
            var variation=1+(random.NextDouble()*2-1)*profile.DailyGrowthVariationPercent/100d; var weight=Math.Max(cycle.InitialAverageWeightGrams,profile.InitialWeightGrams+age*profile.DailyGrowthGrams*variation);
            var survival=Math.Clamp(100-age*profile.DailySurvivalLossPercent,40,100); var alive=cycle.InitialQuantity*survival/100d; var biomass=alive*weight/1000d;
            await _cycles.AddSimulatedSampleAsync(new GrowthSample{CycleId=cycle.Id,PondId=cycle.PondId,SampledAtUtc=DateTime.UtcNow,SampleSize=profile.DefaultSampleSize,AverageWeightGrams=Math.Round(weight,2),EstimatedSurvivalRatePercent=Math.Round(survival,2),EstimatedBiomassKg=Math.Round(biomass,2),SampledBy="Simulation Worker",Notes="Dữ liệu mô phỏng theo cấu hình production-cycle-simulation.json",Source="simulation"},ct);
        }
    }
}
