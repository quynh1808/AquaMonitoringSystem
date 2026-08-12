using System.ComponentModel.DataAnnotations;

namespace AquacultureMonitoring.Models;

/// <summary>Traceable feed, medicine, chemical, spare part or other farm stock.</summary>
public sealed class InventoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Required] public string FarmId { get; set; } = "farm_main";
    [Required] public string Category { get; set; } = "feed";
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string LotNumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg";
    [Range(0, double.MaxValue)] public double QuantityOnHand { get; set; }
    [Range(0, double.MaxValue)] public double ReorderLevel { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InventoryTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string InventoryItemId { get; set; } = string.Empty;
    public string PondId { get; set; } = string.Empty;
    public string CycleId { get; set; } = string.Empty;
    public string Type { get; set; } = "receipt";
    public double Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string Actor { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}

public sealed class AquaticHealthEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Required] public string PondId { get; set; } = string.Empty;
    public string CycleId { get; set; } = string.Empty;
    [Required] public string EventType { get; set; } = "observation";
    [Required] public string SignsOrDiagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string MedicineLot { get; set; } = string.Empty;
    public string LaboratoryReference { get; set; } = string.Empty;
    public DateTime? WithdrawalUntilUtc { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class BiosecurityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    [Required] public string EventType { get; set; } = "disinfection";
    [Required] public string Description { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
}

public sealed class FarmWorkOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    [Required] public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "operations";
    public string Status { get; set; } = "open";
    public string Priority { get; set; } = "normal";
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime DueAtUtc { get; set; } = DateTime.UtcNow.AddDays(1);
    public DateTime? CompletedAtUtc { get; set; }
    public string SopReference { get; set; } = string.Empty;
    public string CompletionEvidence { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class FarmExpense
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PondId { get; set; } = string.Empty;
    public string CycleId { get; set; } = string.Empty;
    [Required] public string Category { get; set; } = "feed";
    [Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Supplier { get; set; } = string.Empty;
    public string InvoiceReference { get; set; } = string.Empty;
    public DateTime IncurredAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
}

public sealed class HarvestLot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Required] public string LotCode { get; set; } = string.Empty;
    [Required] public string PondId { get; set; } = string.Empty;
    [Required] public string CycleId { get; set; } = string.Empty;
    public DateTime HarvestedAtUtc { get; set; } = DateTime.UtcNow;
    [Range(0.01, double.MaxValue)] public double QuantityKg { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string TransportReference { get; set; } = string.Empty;
    public double? StorageTemperatureC { get; set; }
    public string RecordedBy { get; set; } = string.Empty;
}

public sealed class FarmOperationsSnapshot
{
    public List<InventoryItem> Inventory { get; set; } = [];
    public List<AquaticHealthEvent> HealthEvents { get; set; } = [];
    public List<BiosecurityEvent> BiosecurityEvents { get; set; } = [];
    public List<FarmWorkOrder> WorkOrders { get; set; } = [];
    public List<FarmExpense> Expenses { get; set; } = [];
    public List<HarvestLot> HarvestLots { get; set; } = [];
}
