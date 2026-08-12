using System.Security.Claims;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Models;
using AquacultureMonitoring.Sensors;
using AquacultureMonitoring.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Options;

namespace AquacultureMonitoring.Tests;

/// <summary>Regression tests for protocol decoding, passwords and server-side authorization.</summary>
[TestClass]
public sealed class CoreSafetyTests
{
    [TestMethod]
    public void ModbusRtuRequest_AppendsKnownCrc()
    {
        var request = ModbusCodec.BuildRtuReadHoldingRegistersRequest(1, 0, 2);

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x02, 0xC4, 0x0B }, request);
    }

    [TestMethod]
    public void ModbusFloat32_DecodesConfiguredByteOrderAndScale()
    {
        var mapping = new ModbusRegisterMapping
        {
            SensorId = "test",
            DataType = "Float32",
            ByteOrder = "CDAB",
            Scale = 2,
            Offset = 1
        };

        var value = ModbusCodec.Decode(new byte[] { 0x00, 0x00, 0x41, 0x20 }, mapping);

        Assert.AreEqual(21d, value, 0.0001);
    }

    [TestMethod]
    public void PasswordHasher_VerifiesCorrectPasswordAndRejectsWrongPassword()
    {
        var hasher = new PasswordHasher();
        var value = hasher.Hash("A-strong-test-password", 100_000);

        Assert.IsTrue(hasher.Verify("A-strong-test-password", value.Hash, value.Salt, value.Iterations));
        Assert.IsFalse(hasher.Verify("wrong-password", value.Hash, value.Salt, value.Iterations));
    }

    [TestMethod]
    public void FarmAccess_RestrictsAssignedTechnicianToTheirPond()
    {
        var service = new FarmAccessService();
        var user = Principal(SystemRoles.Technician, "pond_01");

        Assert.IsTrue(service.CanAccessPond(user, "pond_01"));
        Assert.IsFalse(service.CanAccessPond(user, "pond_02"));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => service.EnsureCanOperatePond(user, "pond_02"));
    }

    [TestMethod]
    public void FarmAccess_ViewerCannotOperate()
    {
        var service = new FarmAccessService();
        var viewer = Principal(SystemRoles.Viewer, "pond_01");

        Assert.ThrowsExactly<UnauthorizedAccessException>(() => service.EnsureCanOperatePond(viewer, "pond_01"));
    }

    [TestMethod]
    public void DeviceControlValidator_RejectsUnsafeTimeout()
    {
        var result = new DeviceControlOptionsValidator().Validate(null, new DeviceControlOptions
        {
            DefaultTransport = "simulation",
            CommandTimeoutSeconds = 0
        });

        Assert.IsTrue(result.Failed);
    }

    [TestMethod]
    public void ProductionMetrics_UsesNetBiomassGainForFcr()
    {
        var cycle = new ProductionCycle { InitialQuantity = 1_000, InitialAverageWeightGrams = 2, LatestAverageWeightGrams = 2 };
        var sample = new GrowthSample { AverageWeightGrams = 100, EstimatedBiomassKg = 90 };
        var metrics = ProductionMetricsCalculator.Calculate(cycle, sample, [], [],
            [new FeedLog { AmountKg = 88, CycleId = cycle.Id }]);

        Assert.AreEqual(88d, metrics.NetBiomassGainKg, 0.001);
        Assert.AreEqual(1d, metrics.Fcr, 0.001);
    }

    [TestMethod]
    public void ProductionMetrics_SubtractsMortalityAndHarvestedAnimals()
    {
        var cycle = new ProductionCycle { InitialQuantity = 1_000, InitialAverageWeightGrams = 2, LatestAverageWeightGrams = 100 };
        var metrics = ProductionMetricsCalculator.Calculate(cycle, null,
            [new MortalityRecord { Quantity = 100 }],
            [new HarvestRecord { AnimalQuantity = 200, QuantityKg = 20 }], []);

        Assert.AreEqual(700L, metrics.AliveQuantity);
        Assert.AreEqual(70d, metrics.SurvivalRatePercent, 0.001);
    }

    [TestMethod]
    public void OperationalModeGuard_AllowsSimulationDefaults()
    {
        var guard = CreateGuard("Simulation", simulationEnabled: true, activeSources: ["simulation"], notificationsEnabled: false);
        guard.Validate();
    }

    [TestMethod]
    public void OperationalModeGuard_RejectsSimulatorInProduction()
    {
        var guard = CreateGuard("Production", simulationEnabled: true, activeSources: ["simulation"], notificationsEnabled: false);
        Assert.ThrowsExactly<OptionsValidationException>(guard.Validate);
    }

    private static ClaimsPrincipal Principal(string role, params string[] ponds)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "tester"),
            new(ClaimTypes.Role, role)
        };
        claims.AddRange(ponds.Select(pond => new Claim("pond_access", pond)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static OperationalModeGuard CreateGuard(string mode, bool simulationEnabled,
        List<string> activeSources, bool notificationsEnabled) =>
        new(Options.Create(new OperationalModeOptions { Mode = mode }),
            Options.Create(new SimulationProfileOptions { Enabled = simulationEnabled }),
            Options.Create(new DashboardSimulationOptions { Enabled = simulationEnabled }),
            Options.Create(new ProductionCycleSimulationOptions { Enabled = simulationEnabled }),
            Options.Create(new SensorConnectionOptions { ActiveSources = activeSources }),
            Options.Create(new DeviceControlOptions { SimulationEnabled = simulationEnabled }),
            Options.Create(new AlertNotificationOptions
            {
                Enabled = notificationsEnabled,
                WebhookUrl = notificationsEnabled ? "http://alerts.lan/hook" : string.Empty
            }));
}
