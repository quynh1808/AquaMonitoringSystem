using System.Data;
using System.Security.Claims;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using Microsoft.EntityFrameworkCore;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Transactional boundary for inventory, health, biosecurity, work orders, expenses and harvest
/// traceability. Every business write and its audit record commit together.
/// </summary>
public sealed class FarmOperationsService(IDbContextFactory<AquaDbContext> factory, FarmAccessService access)
{
    public async Task<FarmOperationsSnapshot> GetSnapshotAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        bool Allowed(string pond) => string.IsNullOrWhiteSpace(pond) || access.CanAccessPond(user, pond);
        return new FarmOperationsSnapshot
        {
            Inventory = await db.InventoryItems.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct),
            HealthEvents = (await db.HealthEvents.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(300).ToListAsync(ct)).Where(x => Allowed(x.PondId)).ToList(),
            BiosecurityEvents = (await db.BiosecurityEvents.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(300).ToListAsync(ct)).Where(x => Allowed(x.PondId)).ToList(),
            WorkOrders = (await db.WorkOrders.AsNoTracking().OrderBy(x => x.DueAtUtc).Take(500).ToListAsync(ct)).Where(x => Allowed(x.PondId)).ToList(),
            Expenses = (await db.Expenses.AsNoTracking().OrderByDescending(x => x.IncurredAtUtc).Take(500).ToListAsync(ct)).Where(x => Allowed(x.PondId)).ToList(),
            HarvestLots = (await db.HarvestLots.AsNoTracking().OrderByDescending(x => x.HarvestedAtUtc).Take(500).ToListAsync(ct)).Where(x => Allowed(x.PondId)).ToList()
        };
    }

    public async Task AddInventoryAsync(InventoryItem item, ClaimsPrincipal user, CancellationToken ct = default)
    {
        access.EnsureCanManageFarm(user);
        if (item.QuantityOnHand < 0) throw new ArgumentException("Tồn kho không được âm.");
        item.Name = item.Name.Trim(); item.LotNumber = item.LotNumber.Trim(); item.Unit = item.Unit.Trim();
        if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.LotNumber) || string.IsNullOrWhiteSpace(item.Unit))
            throw new ArgumentException("Tên vật tư, số lô và đơn vị là bắt buộc.");
        item.UpdatedAtUtc = DateTime.UtcNow;
        await InsertWithAuditAsync(item, user, "inventory.create", item.Id, string.Empty, item.LotNumber, ct);
    }

    public async Task AdjustInventoryAsync(string itemId, string type, double quantity, string pondId,
        string cycleId, decimal unitCost, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("Phải chọn lô vật tư.");
        if (quantity <= 0) throw new ArgumentException("Số lượng nhập/xuất phải lớn hơn 0.");
        if (type is not ("receipt" or "issue")) throw new ArgumentException("Loại giao dịch kho không hợp lệ.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (type == "issue") await ValidatePondCycleAsync(db, pondId, cycleId, user, true, ct);
        else access.EnsureCanManageFarm(user);

        var value = type == "receipt" ? quantity : -quantity;
        var candidates = db.InventoryItems.Where(x => x.Id == itemId);
        if (type == "issue") candidates = candidates.Where(x => x.QuantityOnHand >= quantity);
        var changed = await candidates.ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand + value)
            .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), ct);
        if (changed == 0) throw new InvalidOperationException("Không tìm thấy vật tư hoặc tồn kho không đủ.");

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = itemId, PondId = pondId, CycleId = cycleId, Type = type,
            Quantity = quantity, UnitCost = unitCost, Actor = FarmAccessService.ActorName(user)
        });
        db.AuditLogs.Add(Audit(user, $"inventory.{type}", itemId, pondId, quantity.ToString()));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task AddHealthEventAsync(AquaticHealthEvent item, ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await ValidatePondCycleAsync(db, item.PondId, item.CycleId, user, true, ct);
        if (string.IsNullOrWhiteSpace(item.SignsOrDiagnosis)) throw new ArgumentException("Phải nhập dấu hiệu hoặc chẩn đoán.");
        if (item.OccurredAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new ArgumentException("Thời điểm sự kiện không được ở tương lai.");
        item.RecordedBy = FarmAccessService.ActorName(user);
        await InsertWithAuditAsync(item, user, "health.create", item.Id, item.PondId, item.EventType, ct);
    }

    public async Task AddBiosecurityEventAsync(BiosecurityEvent item, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(item.Description)) throw new ArgumentException("Phải nhập mô tả sự kiện.");
        if (item.OccurredAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new ArgumentException("Thời điểm sự kiện không được ở tương lai.");
        if (string.IsNullOrWhiteSpace(item.PondId)) access.EnsureCanManageFarm(user); else access.EnsureCanOperatePond(user, item.PondId);
        item.RecordedBy = FarmAccessService.ActorName(user);
        await InsertWithAuditAsync(item, user, "biosecurity.create", item.Id, item.PondId, item.EventType, ct);
    }

    public async Task AddWorkOrderAsync(FarmWorkOrder item, ClaimsPrincipal user, CancellationToken ct = default)
    {
        item.Title = item.Title.Trim();
        if (string.IsNullOrWhiteSpace(item.Title)) throw new ArgumentException("Phải nhập tiêu đề công việc.");
        if (string.IsNullOrWhiteSpace(item.PondId)) access.EnsureCanManageFarm(user); else access.EnsureCanOperatePond(user, item.PondId);
        item.CreatedBy = FarmAccessService.ActorName(user);
        await InsertWithAuditAsync(item, user, "work-order.create", item.Id, item.PondId, item.Title, ct);
    }

    public async Task CompleteWorkOrderAsync(string id, string evidence, ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.WorkOrders.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Không tìm thấy công việc.");
        if (string.IsNullOrWhiteSpace(item.PondId)) access.EnsureCanManageFarm(user); else access.EnsureCanOperatePond(user, item.PondId);
        item.Status = "completed"; item.CompletedAtUtc = DateTime.UtcNow; item.CompletionEvidence = evidence.Trim();
        db.AuditLogs.Add(Audit(user, "work-order.complete", id, item.PondId, evidence));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    public async Task AddExpenseAsync(FarmExpense item, ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await ValidatePondCycleAsync(db, item.PondId, item.CycleId, user, true, ct);
        if (item.Amount <= 0) throw new ArgumentException("Chi phí phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(item.Category)) throw new ArgumentException("Phải nhập loại chi phí.");
        item.RecordedBy = FarmAccessService.ActorName(user);
        await InsertWithAuditAsync(item, user, "expense.create", item.Id, item.PondId, item.Amount.ToString(), ct);
    }

    public async Task AddHarvestLotAsync(HarvestLot item, ClaimsPrincipal user, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await ValidatePondCycleAsync(db, item.PondId, item.CycleId, user, true, ct);
        if (item.QuantityKg <= 0) throw new ArgumentException("Khối lượng lô phải lớn hơn 0.");
        item.LotCode = item.LotCode.Trim();
        if (string.IsNullOrWhiteSpace(item.LotCode)) throw new ArgumentException("Phải nhập mã lô thu hoạch.");
        item.RecordedBy = FarmAccessService.ActorName(user);
        await InsertWithAuditAsync(item, user, "harvest-lot.create", item.Id, item.PondId, item.LotCode, ct);
    }

    private async Task ValidatePondCycleAsync(AquaDbContext db, string pond, string cycleId,
        ClaimsPrincipal user, bool cycleRequired, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pond)) throw new ArgumentException("Phải chọn ao nuôi.");
        access.EnsureCanOperatePond(user, pond);
        if (string.IsNullOrWhiteSpace(cycleId))
        {
            if (cycleRequired) throw new InvalidOperationException("Ao chưa có vụ nuôi đang hoạt động.");
            return;
        }
        var cycle = await db.ProductionCycles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cycleId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy vụ nuôi.");
        if (!cycle.PondId.Equals(pond, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Vụ nuôi không thuộc ao đã chọn.");
    }

    private async Task InsertWithAuditAsync<TEntity>(TEntity item, ClaimsPrincipal user, string action,
        string id, string pond, string value, CancellationToken ct) where TEntity : class
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Set<TEntity>().Add(item);
        db.AuditLogs.Add(Audit(user, action, id, pond, value));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }

    private static AuditLog Audit(ClaimsPrincipal user, string action, string id, string pond, string value) => new()
    {
        Actor = FarmAccessService.ActorName(user), Action = action, EntityType = "farm-operation",
        EntityId = id, PondId = pond, NewValue = value
    };
}
