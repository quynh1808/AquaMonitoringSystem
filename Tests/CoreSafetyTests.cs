using System.Security.Claims;
using AquacultureMonitoring.Configuration;
using AquacultureMonitoring.Devices;
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
            DefaultTransport = "fx5u-modbus-tcp",
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
    public void Fx5uMDeviceMapping_UsesOfficialModbusBase()
    {
        Assert.AreEqual((ushort)0x2064, Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress("M100"));
        Assert.AreEqual((ushort)0x20C8, Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress("M200"));
        Assert.AreEqual((ushort)0x212C, Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress("M300"));
    }

    [TestMethod]
    public void Fx5uMDeviceMapping_RejectsInvalidAddress()
    {
        Assert.ThrowsExactly<FormatException>(() => Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress("D100"));
        Assert.ThrowsExactly<FormatException>(() => Fx5uModbusTcpDeviceTransport.ToModbusCoilAddress("M7680"));
    }

    [TestMethod]
    public void Fx5uDDeviceMapping_UsesRegisterNumber()
    {
        Assert.AreEqual((ushort)100, Fx5uModbusTcpDeviceTransport.ToHoldingRegisterAddress("D100"));
        Assert.ThrowsExactly<FormatException>(() => Fx5uModbusTcpDeviceTransport.ToHoldingRegisterAddress("M100"));
    }

    [TestMethod]
    public void NewSensor_DefaultsToNotPublishingUntilCommissioned()
    {
        var sensor = new SensorDefinition();

        Assert.AreEqual(0, sensor.PublishIntervalSeconds);
        Assert.AreEqual("draft", sensor.ConfigurationStatus);
        Assert.IsTrue(sensor.IsPrimary);
    }

    [TestMethod]
    public void NewDevice_DefaultsToDraftAndCannotBeMistakenForCommissionedPlant()
    {
        var device = new Device();

        Assert.AreEqual("draft", device.ConfigurationStatus);
        Assert.AreEqual(0L, device.DesiredConfigurationVersion);
        Assert.AreEqual(0L, device.ReportedConfigurationVersion);
        Assert.IsFalse(device.IsPendingRemoval);
        Assert.IsFalse(device.IsArchived);
    }

    [TestMethod]
    public void PendingRemoval_IsDistinctFromArchiveUntilNodeAcknowledges()
    {
        var sensor = new SensorDefinition { IsEnabled = false, IsPendingRemoval = true, IsArchived = false };
        var device = new Device { IsEnabled = false, IsPendingRemoval = true, IsArchived = false };

        Assert.IsTrue(sensor.IsPendingRemoval && !sensor.IsArchived);
        Assert.IsTrue(device.IsPendingRemoval && !device.IsArchived);
    }

    [TestMethod]
    public void OperationalModeGuard_AllowsEdgeSimulationContract()
    {
        var guard = CreateGuard("EdgeSimulation", hardwareEnabled: false, notificationsEnabled: false);
        guard.Validate();
    }

    [TestMethod]
    public void OperationalModeGuard_RejectsUncommissionedProduction()
    {
        var guard = CreateGuard("Production", hardwareEnabled: false, notificationsEnabled: false);
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

    private static OperationalModeGuard CreateGuard(string mode, bool hardwareEnabled,
        bool notificationsEnabled) =>
        new(Options.Create(new OperationalModeOptions { Mode = mode }),
            Options.Create(new DeviceControlOptions
            {
                DefaultTransport = "fx5u-modbus-tcp", HardwareControlEnabled = hardwareEnabled
            }),
            Options.Create(new AlertNotificationOptions
            {
                Enabled = notificationsEnabled,
                WebhookUrl = notificationsEnabled ? "http://alerts.lan/hook" : string.Empty
            }));
}
