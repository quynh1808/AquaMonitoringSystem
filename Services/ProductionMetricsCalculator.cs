using AquacultureMonitoring.Models;

namespace AquacultureMonitoring.Services;

/// <summary>Pure KPI calculator shared by production service and regression tests.</summary>
public static class ProductionMetricsCalculator
{
    public static ProductionMetrics Calculate(ProductionCycle cycle, GrowthSample? latest,
        IEnumerable<MortalityRecord> mortalities, IEnumerable<HarvestRecord> harvests,
        IEnumerable<FeedLog> feeds)
    {
        var dead = mortalities.Sum(x => x.Quantity);
        var harvestedAnimals = harvests.Sum(x => x.AnimalQuantity);
        var alive = Math.Max(0, cycle.InitialQuantity - dead - harvestedAnimals);
        var survival = cycle.InitialQuantity <= 0 ? 0 : alive * 100d / cycle.InitialQuantity;
        var weight = latest?.AverageWeightGrams ?? cycle.LatestAverageWeightGrams;
        var biomass = latest?.EstimatedBiomassKg ?? alive * weight / 1000d;
        var harvestedKg = harvests.Sum(x => x.QuantityKg);
        var totalFeed = feeds.Sum(x => x.AmountKg);
        var initialBiomass = cycle.InitialQuantity * cycle.InitialAverageWeightGrams / 1000d;
        var netGain = biomass + harvestedKg - initialBiomass;
        var fcr = netGain > 0 ? totalFeed / netGain : 0;
        return new(dead, harvestedAnimals, alive, survival, weight, biomass, totalFeed,
            harvestedKg, Math.Max(0, netGain), fcr);
    }
}

public sealed record ProductionMetrics(long MortalityQuantity, long HarvestedAnimalQuantity,
    long AliveQuantity, double SurvivalRatePercent, double AverageWeightGrams, double BiomassKg,
    double TotalFeedKg, double HarvestedKg, double NetBiomassGainKg, double Fcr);
