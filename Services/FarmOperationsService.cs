using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AquacultureMonitoring.Data;
using AquacultureMonitoring.Models;
using MongoDB.Driver;

namespace AquacultureMonitoring.Services;

/// <summary>
/// Application boundary for inventory, health, biosecurity, work orders, expenses and
/// harvest traceability. Authorization and cross-entity validation are enforced here.
/// </summary>
public sealed class FarmOperationsService
{
	private readonly MongoContext _db;

	private readonly FarmAccessService _access;

	public FarmOperationsService(MongoContext db, FarmAccessService access)
	{
		_db = db;
		_access = access;
	}

	public async Task<FarmOperationsSnapshot> GetSnapshotAsync(ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		Task<List<InventoryItem>> inventoryTask = _db.InventoryItems.Find<InventoryItem>(FilterDefinition<InventoryItem>.Empty).SortBy((InventoryItem x) => x.Name).ToListAsync(ct);
		Task<List<AquaticHealthEvent>> healthTask = _db.HealthEvents.Find<AquaticHealthEvent>(FilterDefinition<AquaticHealthEvent>.Empty).SortByDescending((AquaticHealthEvent x) => x.OccurredAtUtc).Limit(300)
			.ToListAsync(ct);
		Task<List<BiosecurityEvent>> bioTask = _db.BiosecurityEvents.Find<BiosecurityEvent>(FilterDefinition<BiosecurityEvent>.Empty).SortByDescending((BiosecurityEvent x) => x.OccurredAtUtc).Limit(300)
			.ToListAsync(ct);
		Task<List<FarmWorkOrder>> workTask = _db.WorkOrders.Find<FarmWorkOrder>(FilterDefinition<FarmWorkOrder>.Empty).SortBy((FarmWorkOrder x) => x.DueAtUtc).Limit(500)
			.ToListAsync(ct);
		Task<List<FarmExpense>> expenseTask = _db.Expenses.Find<FarmExpense>(FilterDefinition<FarmExpense>.Empty).SortByDescending((FarmExpense x) => x.IncurredAtUtc).Limit(500)
			.ToListAsync(ct);
		Task<List<HarvestLot>> harvestTask = _db.HarvestLots.Find<HarvestLot>(FilterDefinition<HarvestLot>.Empty).SortByDescending((HarvestLot x) => x.HarvestedAtUtc).Limit(500)
			.ToListAsync(ct);
		await Task.WhenAll(inventoryTask, healthTask, bioTask, workTask, expenseTask, harvestTask);
		return new FarmOperationsSnapshot
		{
			Inventory = await inventoryTask,
			HealthEvents = (await healthTask).Where(x => allowed(x.PondId)).ToList(),
			BiosecurityEvents = (await bioTask).Where(x => allowed(x.PondId)).ToList(),
			WorkOrders = (await workTask).Where(x => allowed(x.PondId)).ToList(),
			Expenses = (await expenseTask).Where(x => allowed(x.PondId)).ToList(),
			HarvestLots = (await harvestTask).Where(x => allowed(x.PondId)).ToList()
		};
		bool allowed(string pond)
		{
			if (!string.IsNullOrWhiteSpace(pond))
			{
				return _access.CanAccessPond(user, pond);
			}
			return true;
		}
	}

	public async Task AddInventoryAsync(InventoryItem item, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		_access.EnsureCanManageFarm(user);
		if (item.QuantityOnHand < 0.0)
		{
			throw new ArgumentException("Tồn kho không được âm.");
		}
		item.Name = item.Name.Trim();
		item.LotNumber = item.LotNumber.Trim();
		item.Unit = item.Unit.Trim();
		if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.LotNumber) || string.IsNullOrWhiteSpace(item.Unit))
		{
			throw new ArgumentException("Tên vật tư, số lô và đơn vị là bắt buộc.");
		}
		item.UpdatedAtUtc = DateTime.UtcNow;
		await _db.InventoryItems.InsertOneAsync(item, null, ct);
		await AuditAsync(user, "inventory.create", item.Id, string.Empty, item.LotNumber, ct);
	}

	public async Task AdjustInventoryAsync(string itemId, string type, double quantity, string pondId, string cycleId, decimal unitCost, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(itemId))
		{
			throw new ArgumentException("Phải chọn lô vật tư.");
		}
		if (quantity <= 0.0)
		{
			throw new ArgumentException("Số lượng nhập/xuất phải lớn hơn 0.");
		}
		if (type is not ("receipt" or "issue"))
		{
			throw new ArgumentException("Loại giao dịch kho không hợp lệ.");
		}
		if (!(type == "receipt"))
		{
			await ValidatePondCycleAsync(pondId, cycleId, user, cycleRequired: true, ct);
		}
		else
		{
			_access.EnsureCanManageFarm(user);
		}
		double value = ((type == "receipt") ? quantity : (0.0 - quantity));
		FilterDefinition<InventoryItem> filter = Builders<InventoryItem>.Filter.Eq<string>((InventoryItem x) => x.Id, itemId);
		if (type == "issue")
		{
			filter &= Builders<InventoryItem>.Filter.Gte((InventoryItem x) => x.QuantityOnHand, quantity);
		}
		if ((await _db.InventoryItems.UpdateOneAsync(filter, Builders<InventoryItem>.Update.Inc((InventoryItem x) => x.QuantityOnHand, value).Set((InventoryItem x) => x.UpdatedAtUtc, DateTime.UtcNow), null, ct)).ModifiedCount == 0L)
		{
			throw new InvalidOperationException("Không tìm thấy vật tư hoặc tồn kho không đủ.");
		}
		InventoryTransaction document = new InventoryTransaction
		{
			InventoryItemId = itemId,
			PondId = pondId,
			CycleId = cycleId,
			Type = type,
			Quantity = quantity,
			UnitCost = unitCost,
			Actor = FarmAccessService.ActorName(user)
		};
		await _db.InventoryTransactions.InsertOneAsync(document, null, ct);
		await AuditAsync(user, "inventory." + type, itemId, pondId, quantity.ToString(), ct);
	}

	public async Task AddHealthEventAsync(AquaticHealthEvent item, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		await ValidatePondCycleAsync(item.PondId, item.CycleId, user, cycleRequired: true, ct);
		if (string.IsNullOrWhiteSpace(item.SignsOrDiagnosis))
		{
			throw new ArgumentException("Phải nhập dấu hiệu hoặc chẩn đoán.");
		}
		if (item.OccurredAtUtc > DateTime.UtcNow.AddMinutes(5.0))
		{
			throw new ArgumentException("Thời điểm sự kiện không được ở tương lai.");
		}
		item.RecordedBy = FarmAccessService.ActorName(user);
		await _db.HealthEvents.InsertOneAsync(item, null, ct);
		await AuditAsync(user, "health.create", item.Id, item.PondId, item.EventType, ct);
	}

	public async Task AddBiosecurityEventAsync(BiosecurityEvent item, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		if (string.IsNullOrWhiteSpace(item.Description))
		{
			throw new ArgumentException("Phải nhập mô tả sự kiện.");
		}
		if (item.OccurredAtUtc > DateTime.UtcNow.AddMinutes(5.0))
		{
			throw new ArgumentException("Thời điểm sự kiện không được ở tương lai.");
		}
		if (string.IsNullOrWhiteSpace(item.PondId))
		{
			_access.EnsureCanManageFarm(user);
		}
		else
		{
			_access.EnsureCanOperatePond(user, item.PondId);
		}
		item.RecordedBy = FarmAccessService.ActorName(user);
		await _db.BiosecurityEvents.InsertOneAsync(item, null, ct);
		await AuditAsync(user, "biosecurity.create", item.Id, item.PondId, item.EventType, ct);
	}

	public async Task AddWorkOrderAsync(FarmWorkOrder item, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		item.Title = item.Title.Trim();
		if (string.IsNullOrWhiteSpace(item.Title))
		{
			throw new ArgumentException("Phải nhập tiêu đề công việc.");
		}
		if (string.IsNullOrWhiteSpace(item.PondId))
		{
			_access.EnsureCanManageFarm(user);
		}
		else
		{
			_access.EnsureCanOperatePond(user, item.PondId);
		}
		item.CreatedBy = FarmAccessService.ActorName(user);
		await _db.WorkOrders.InsertOneAsync(item, null, ct);
		await AuditAsync(user, "work-order.create", item.Id, item.PondId, item.Title, ct);
	}

	public async Task CompleteWorkOrderAsync(string id, string evidence, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		FarmWorkOrder item = (await _db.WorkOrders.Find<FarmWorkOrder>((FarmWorkOrder x) => x.Id == id).FirstOrDefaultAsync(ct)) ?? throw new InvalidOperationException("Không tìm thấy công việc.");
		if (string.IsNullOrWhiteSpace(item.PondId))
		{
			_access.EnsureCanManageFarm(user);
		}
		else
		{
			_access.EnsureCanOperatePond(user, item.PondId);
		}
		await _db.WorkOrders.UpdateOneAsync<FarmWorkOrder>((FarmWorkOrder x) => x.Id == id, Builders<FarmWorkOrder>.Update.Set<string>((FarmWorkOrder x) => x.Status, "completed").Set((FarmWorkOrder x) => x.CompletedAtUtc, DateTime.UtcNow).Set<FarmWorkOrder, string>((FarmWorkOrder x) => x.CompletionEvidence, evidence.Trim()), null, ct);
		await AuditAsync(user, "work-order.complete", id, item.PondId, evidence, ct);
	}

	public async Task AddExpenseAsync(FarmExpense item, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		await ValidatePondCycleAsync(item.PondId, item.CycleId, user, cycleRequired: true, ct);
		if (item.Amount <= 0m)
		{
			throw new ArgumentException("Chi phí phải lớn hơn 0.");
		}
		if (string.IsNullOrWhiteSpace(item.Category))
		{
			throw new ArgumentException("Phải nhập loại chi phí.");
		}
		item.RecordedBy = FarmAccessService.ActorName(user);
		await _db.Expenses.InsertOneAsync(item, null, ct);
		await AuditAsync(user, "expense.create", item.Id, item.PondId, item.Amount.ToString(), ct);
	}

	public async Task AddHarvestLotAsync(HarvestLot item, ClaimsPrincipal user, CancellationToken ct = default(CancellationToken))
	{
		await ValidatePondCycleAsync(item.PondId, item.CycleId, user, cycleRequired: true, ct);
		if (item.QuantityKg <= 0.0)
		{
			throw new ArgumentException("Khối lượng lô phải lớn hơn 0.");
		}
		item.LotCode = item.LotCode.Trim();
		if (string.IsNullOrWhiteSpace(item.LotCode))
		{
			throw new ArgumentException("Phải nhập mã lô thu hoạch.");
		}
		item.RecordedBy = FarmAccessService.ActorName(user);
		await _db.HarvestLots.InsertOneAsync(item, null, ct);
		await AuditAsync(user, "harvest-lot.create", item.Id, item.PondId, item.LotCode, ct);
	}

	private async Task ValidatePondCycleAsync(string pond, string cycleId, ClaimsPrincipal user, bool cycleRequired, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(pond))
		{
			throw new ArgumentException("Phải chọn ao nuôi.");
		}
		_access.EnsureCanOperatePond(user, pond);
		if (string.IsNullOrWhiteSpace(cycleId))
		{
			if (cycleRequired)
			{
				throw new InvalidOperationException("Ao chưa có vụ nuôi đang hoạt động.");
			}
			return;
		}
		ProductionCycle productionCycle = (await _db.ProductionCycles.Find<ProductionCycle>((ProductionCycle x) => x.Id == cycleId).FirstOrDefaultAsync(ct)) ?? throw new InvalidOperationException("Không tìm thấy vụ nuôi.");
		if (productionCycle.PondId.Equals(pond, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		throw new InvalidOperationException("Vụ nuôi không thuộc ao đã chọn.");
	}

	private Task AuditAsync(ClaimsPrincipal user, string action, string id, string pond, string value, CancellationToken ct)
	{
		return _db.AuditLogs.InsertOneAsync(new AuditLog
		{
			Actor = FarmAccessService.ActorName(user),
			Action = action,
			EntityType = "farm-operation",
			EntityId = id,
			PondId = pond,
			NewValue = value
		}, null, ct);
	}
}
