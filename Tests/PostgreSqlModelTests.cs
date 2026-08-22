using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AquacultureMonitoring.Tests;

[TestClass]
public sealed class PostgreSqlModelTests
{
    private static AquaDbContext CreateContext() => new AquaDbContextDesignFactory().CreateDbContext([]);

    [TestMethod]
    public void RelationalModel_UsesPerSensorLatestState_AndDoesNotPersistFixedTelemetryDocument()
    {
        using var db = CreateContext();
        Assert.IsNotNull(db.Model.FindEntityType(typeof(LatestSensorState)));
        Assert.IsNull(db.Model.FindEntityType(typeof(TelemetryData)));
        Assert.AreEqual("latest_sensor_states", db.Model.FindEntityType(typeof(LatestSensorState))!.GetTableName());
    }

    [TestMethod]
    public void SensorReading_RoutingMetadata_IsFlattenedIntoIndexedColumns()
    {
        var reading = new SensorReading
        {
            Metadata = new SensorReadingMetadata { PondId = "pond_01", SensorId = "do_01", Metric = "dissolvedOxygen" }
        };
        Assert.AreEqual("pond_01", reading.PondId);
        Assert.AreEqual("do_01", reading.SensorId);

        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(SensorReading))!;
        Assert.IsNull(entity.FindProperty(nameof(SensorReading.Metadata)));
        Assert.IsNotNull(entity.FindProperty(nameof(SensorReading.SensorId)));
        Assert.IsTrue(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == nameof(SensorReading.EventId)));
    }

    [TestMethod]
    public void CoreAssets_HaveDatabaseForeignKeysToPond()
    {
        using var db = CreateContext();
        foreach (var type in new[] { typeof(SensorDefinition), typeof(Device), typeof(ProductionCycle), typeof(SensorReading) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.IsTrue(entity.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(Pond)),
                $"{type.Name} must reference Pond through a PostgreSQL foreign key.");
        }
    }
}
