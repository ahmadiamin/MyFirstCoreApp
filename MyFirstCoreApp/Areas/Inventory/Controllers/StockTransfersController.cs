using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Areas.Inventory.ViewModels.StockTransfers;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public sealed class StockTransfersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStockMovementService _stockMovementService;
    private readonly TimeProvider _timeProvider;

    public StockTransfersController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IStockMovementService stockMovementService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentTenant = currentTenant;
        _stockMovementService = stockMovementService;
        _timeProvider = timeProvider;
    }

    [Authorize(Policy = "StockMovement.View")]
    public async Task<IActionResult> Index()
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var tenantId = _currentTenant.TenantId.Value;

        var transfers = await _context.StockMovements
            .AsNoTracking()
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .Where(x => x.TenantId == tenantId && x.MovementType == StockMovementType.Transfer)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .ToListAsync();

        return View(new StockTransferListViewModel
        {
            Transfers = transfers.Select(x => new StockTransferListItemViewModel
            {
                Id = x.Id,
                Number = x.Number,
                Date = PersianDate.Format(x.Date),
                Status = x.Status switch
                {
                    StockMovementStatus.Draft => "پیش‌نویس",
                    StockMovementStatus.Posted => "قطعی",
                    StockMovementStatus.Cancelled => "لغوشده",
                    _ => "نامشخص"
                },
                SourceWarehouse = x.SourceWarehouse?.Name ?? "—",
                DestinationWarehouse = x.DestinationWarehouse?.Name ?? "—",
                Description = x.Description,
                ItemCount = x.Items.Count,
                ItemsSummary = string.Join("، ", x.Items.Select(i => $"{i.Product?.Name ?? "کالای نامشخص"} × {i.Quantity}"))
            }).ToList()
        });
    }

    [Authorize(Policy = "StockMovement.Create")]
    public async Task<IActionResult> Create()
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var model = new StockTransferViewModel
        {
            Date = PersianDate.Format(_timeProvider.Today())
        };
        await PopulateOptionsAsync(model, _currentTenant.TenantId.Value);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "StockMovement.Create")]
    public async Task<IActionResult> Create(StockTransferViewModel model)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var tenantId = _currentTenant.TenantId.Value;

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), "تاریخ انتقال معتبر نیست.");

        if (model.Items.Count == 0)
            ModelState.AddModelError(nameof(model.Items), "حداقل یک کالا به انتقال اضافه کنید.");
        if (model.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
            ModelState.AddModelError(nameof(model.Items), "برای همه ردیف‌ها کالا و مقدار معتبر انتخاب کنید.");
        if (model.Items.GroupBy(x => x.ProductId).Any(g => g.Key > 0 && g.Count() > 1))
            ModelState.AddModelError(nameof(model.Items), "هر کالا را فقط یک‌بار به سند اضافه کنید.");

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, tenantId);
            return View(model);
        }

        try
        {
            await _stockMovementService.CreateAsync(new StockMovementRequest
            {
                Date = date,
                MovementType = (int)StockMovementType.Transfer,
                SourceWarehouseId = model.SourceWarehouseId,
                DestinationWarehouseId = model.DestinationWarehouseId,
                Description = model.Description,
                Items = model.Items.Select(x => new StockMovementItemRequest
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity
                }).ToList()
            }, tenantId);

            TempData["Success"] = "پیش‌نویس انتقال ثبت شد. برای اعمال در موجودی، آن را قطعی کنید.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateOptionsAsync(model, tenantId);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "StockMovement.Post")]
    public async Task<IActionResult> Post(int id)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        try
        {
            await _stockMovementService.PostAsync(id, _currentTenant.TenantId.Value);
            TempData["Success"] = "انتقال قطعی شد و موجودی دو انبار به‌روزرسانی شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "StockMovement.Create")]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        try
        {
            await _stockMovementService.CancelAsync(id, _currentTenant.TenantId.Value);
            TempData["Success"] = "پیش‌نویس انتقال لغو شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(StockTransferViewModel model, int tenantId)
    {
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync();
        model.Warehouses = warehouses
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();

        var products = await _context.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.TrackInventory)
            .OrderBy(x => x.Name)
            .ToListAsync();
        model.Products = products
            .Select(x => new SelectListItem(
                x.Code == null ? x.Name : x.Name + " (" + x.Code + ")",
                x.Id.ToString()))
            .ToList();
    }
}
