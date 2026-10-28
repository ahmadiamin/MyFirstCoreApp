using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class InventoryAnalyticsService : IInventoryAnalyticsService
{
    private readonly ApplicationDbContext _context;

    public InventoryAnalyticsService(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<InventoryAlertDto>> GetBelowMinimumStockAsync(int tenantId)
    {
        var products = await _context.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.TrackInventory && p.MinimumStock.HasValue)
            .Select(p => new { p.Id, p.Name, p.Code, p.MinimumStock, UnitName = p.Unit.Name })
            .ToListAsync();
        if (products.Count == 0) return [];

        var movements = await GetPostedMovements(tenantId, DateOnly.MaxValue).ToListAsync();
        var balances = new Dictionary<int, decimal>();
        foreach (var movement in movements.OrderBy(m => m.Date).ThenBy(m => m.Id))
        foreach (var item in movement.Items)
        {
            if (movement.DestinationWarehouseId.HasValue)
                balances[item.ProductId] = balances.GetValueOrDefault(item.ProductId) + item.Quantity;
            if (movement.SourceWarehouseId.HasValue)
                balances[item.ProductId] = balances.GetValueOrDefault(item.ProductId) - item.Quantity;
        }

        return products.Select(p => new InventoryAlertDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                ProductCode = p.Code,
                UnitName = p.UnitName,
                MinimumStock = p.MinimumStock!.Value,
                CurrentStock = balances.GetValueOrDefault(p.Id),
                Shortage = Math.Max(0m, p.MinimumStock.Value - balances.GetValueOrDefault(p.Id))
            })
            .Where(x => x.CurrentStock < x.MinimumStock)
            .OrderByDescending(x => x.Shortage).ThenBy(x => x.ProductName)
            .ToList();
    }

    public async Task<IReadOnlyList<InventoryValuationDto>> GetInventoryValuationAsync(
        int tenantId, DateOnly asOf, int? warehouseId = null, int? productId = null)
    {
        if (warehouseId.HasValue && !await _context.Warehouses.AnyAsync(w =>
                w.Id == warehouseId && w.TenantId == tenantId))
            throw new InvalidOperationException("انبار پیدا نشد.");
        if (productId.HasValue && !await _context.Products.AnyAsync(p =>
                p.Id == productId && p.TenantId == tenantId && p.TrackInventory))
            throw new InvalidOperationException("کالا پیدا نشد یا قابل کنترل موجودی نیست.");

        var products = await _context.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.TrackInventory &&
                        (!productId.HasValue || p.Id == productId.Value))
            .Select(p => new { p.Id, p.Name, p.Code, UnitName = p.Unit.Name })
            .ToDictionaryAsync(p => p.Id);
        var warehouses = await _context.Warehouses.AsNoTracking()
            .Where(w => w.TenantId == tenantId)
            .Select(w => new { w.Id, w.Name }).ToDictionaryAsync(w => w.Id);
        var movements = await GetPostedMovements(tenantId, asOf)
            .Where(m => !productId.HasValue || m.Items.Any(i => i.ProductId == productId.Value))
            .OrderBy(m => m.Date).ThenBy(m => m.Id)
            .ToListAsync();

        var states = new Dictionary<(int ProductId, int WarehouseId), CostState>();
        CostState State(int pid, int wid)
        {
            if (!states.TryGetValue((pid, wid), out var state))
                states[(pid, wid)] = state = new CostState();
            return state;
        }

        foreach (var movement in movements)
        foreach (var item in movement.Items.Where(i => products.ContainsKey(i.ProductId)))
        {
            if (movement.SourceWarehouseId is int sourceId)
            {
                var source = State(item.ProductId, sourceId);
                var avg = source.Quantity != 0 && source.Value.HasValue
                    ? source.Value.Value / source.Quantity : (decimal?)null;
                source.Quantity -= item.Quantity;
                if (avg.HasValue && source.Value.HasValue)
                    source.Value -= avg.Value * item.Quantity;
                else
                    source.Value = null;

                if (movement.MovementType == StockMovementType.Transfer && movement.DestinationWarehouseId is int destId)
                {
                    var destination = State(item.ProductId, destId);
                    destination.Quantity += item.Quantity;
                    if (destination.Value.HasValue && avg.HasValue)
                        destination.Value += avg.Value * item.Quantity;
                    else if (!destination.Value.HasValue && destination.Quantity == item.Quantity && avg.HasValue)
                        destination.Value = avg.Value * item.Quantity;
                    else
                        destination.Value = null;
                }
            }
            else if (movement.DestinationWarehouseId is int destinationId)
            {
                var destination = State(item.ProductId, destinationId);
                var unitCost = item.UnitCost;
                if (!unitCost.HasValue && destination.Quantity != 0 && destination.Value.HasValue)
                    unitCost = destination.Value.Value / destination.Quantity;
                destination.Quantity += item.Quantity;
                if (unitCost.HasValue && destination.Value.HasValue)
                    destination.Value += unitCost.Value * item.Quantity;
                else if (unitCost.HasValue && destination.Quantity == item.Quantity)
                    destination.Value = unitCost.Value * item.Quantity;
                else
                    destination.Value = null;
            }
        }

        return states.Where(x => x.Value.Quantity != 0 &&
                                 (!warehouseId.HasValue || x.Key.WarehouseId == warehouseId.Value))
            .Where(x => products.ContainsKey(x.Key.ProductId) && warehouses.ContainsKey(x.Key.WarehouseId))
            .Select(x =>
            {
                var product = products[x.Key.ProductId];
                var state = x.Value;
                var value = state.Value;
                var average = value.HasValue && state.Quantity != 0 ? value / state.Quantity : null;
                return new InventoryValuationDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductCode = product.Code,
                    UnitName = product.UnitName,
                    WarehouseId = x.Key.WarehouseId,
                    WarehouseName = warehouses[x.Key.WarehouseId].Name,
                    Quantity = state.Quantity,
                    AverageUnitCost = average,
                    TotalValue = value,
                    HasUnknownCost = !value.HasValue
                };
            })
            .OrderBy(x => x.WarehouseName).ThenBy(x => x.ProductName)
            .ToList();
    }

    private IQueryable<StockMovement> GetPostedMovements(int tenantId, DateOnly through) =>
        _context.StockMovements.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Status == StockMovementStatus.Posted && m.Date <= through)
            .Include(m => m.Items.Where(i => i.TenantId == tenantId));

    private sealed class CostState
    {
        public decimal Quantity { get; set; }
        public decimal? Value { get; set; } = 0m;
    }
}
