using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Areas.Projects.ViewModels.ProjectMaterials;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Projects.Controllers;

[Area("Projects"), Authorize]
public sealed class ProjectMaterialsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly IStockMovementService _movements;
    private readonly TimeProvider _time;
    private readonly IAuthorizationService _authorization;

    public ProjectMaterialsController(
        ApplicationDbContext db,
        ICurrentTenant tenant,
        IStockMovementService movements,
        TimeProvider time,
        IAuthorizationService authorization)
    {
        _db = db;
        _tenant = tenant;
        _movements = movements;
        _time = time;
        _authorization = authorization;
    }

    [Authorize(Policy = "StockMovement.View")]
    public async Task<IActionResult> Index(int projectId)
    {
        var tenantId = RequireTenant();
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == projectId && x.TenantId == tenantId);
        if (project == null) return NotFound();

        var movements = await _db.StockMovements.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted &&
                (x.ProjectId == projectId || (x.ReversesMovementId.HasValue &&
                    _db.StockMovements.IgnoreQueryFilters().Any(original =>
                        original.Id == x.ReversesMovementId.Value && original.TenantId == tenantId && original.ProjectId == projectId))) &&
                (x.MovementType == StockMovementType.Issue || x.ReversesMovementId.HasValue))
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Items).ThenInclude(x => x.Product).ThenInclude(x => x.Unit)
            .Include(x => x.ReversesMovement)
            .Include(x => x.ReversalMovements)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .ToListAsync();

        return View(new ProjectMaterialListViewModel
        {
            ProjectId = projectId,
            ProjectTitle = project.Title,
            CanCreate = (await _authorization.AuthorizeAsync(User, null, "StockMovement.Create")).Succeeded,
            CanPost = (await _authorization.AuthorizeAsync(User, null, "StockMovement.Post")).Succeeded,
            CanDelete = (await _authorization.AuthorizeAsync(User, null, "StockMovement.Delete")).Succeeded,
            Movements = movements.Select(x => new ProjectMaterialMovementViewModel
            {
                Id = x.Id,
                Number = x.Number,
                Date = PersianDate.Format(x.Date) ?? string.Empty,
                Warehouse = x.MovementType == StockMovementType.Receipt
                    ? x.DestinationWarehouse?.Name ?? "—"
                    : x.SourceWarehouse?.Name ?? "—",
                IsReversal = x.ReversesMovementId.HasValue,
                HasPostedReversal = x.ReversalMovements.Any(r => !r.IsDeleted && r.Status == StockMovementStatus.Posted),
                Status = x.ReversesMovementId.HasValue
                    ? x.Status == StockMovementStatus.Posted ? "برگشت قطعی" : x.Status == StockMovementStatus.Draft ? "پیش‌نویس برگشت" : "برگشت لغوشده"
                    : x.ReversalMovements.Any(r => !r.IsDeleted && r.Status == StockMovementStatus.Posted)
                        ? "برگشت‌شده"
                        : x.Status switch
                {
                    StockMovementStatus.Draft => "پیش‌نویس",
                    StockMovementStatus.Posted => "قطعی",
                    StockMovementStatus.Cancelled => "لغوشده",
                    _ => "نامشخص"
                },
                IsDraft = x.Status == StockMovementStatus.Draft,
                CanReverse = !x.ReversesMovementId.HasValue && !x.ReferenceInvoiceId.HasValue &&
                    x.Status == StockMovementStatus.Posted &&
                    !x.ReversalMovements.Any(r => !r.IsDeleted),
                CanDelete = !x.ReversesMovementId.HasValue && !x.ReferenceInvoiceId.HasValue &&
                    (x.Status is StockMovementStatus.Draft or StockMovementStatus.Cancelled ||
                     x.Status == StockMovementStatus.Posted && x.ReversalMovements.Any(r => !r.IsDeleted && r.Status == StockMovementStatus.Posted)),
                Items = string.Join("، ", x.Items.Select(i => $"{i.Product.Name} × {i.Quantity:0.###} {i.Product.Unit.Symbol}")),
                Description = x.Description
            }).ToList()
        });
    }

    [Authorize(Policy = "StockMovement.Create")]
    public async Task<IActionResult> Create(int projectId)
    {
        var tenantId = RequireTenant();
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId && x.TenantId == tenantId)) return NotFound();
        var model = new ProjectMaterialFormViewModel
        {
            ProjectId = projectId,
            Date = PersianDate.Format(_time.Today())
        };
        await PopulateOptionsAsync(model, tenantId);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "StockMovement.Create")]
    public async Task<IActionResult> Create(ProjectMaterialFormViewModel model)
    {
        var tenantId = RequireTenant();
        if (!await _db.Projects.AnyAsync(x => x.Id == model.ProjectId && x.TenantId == tenantId))
            return NotFound();

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), "تاریخ شمسی معتبر نیست.");
        if (model.Items.Count == 0 || model.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
            ModelState.AddModelError(nameof(model.Items), "برای هر ردیف، قلم مصرفی و مقدار معتبر انتخاب کنید.");
        if (model.Items.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
            ModelState.AddModelError(nameof(model.Items), "هر قلم مصرفی را فقط یک‌بار در سند وارد کنید.");

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, tenantId);
            return View(model);
        }

        try
        {
            var movementId = await _movements.CreateAsync(new StockMovementRequest
            {
                Date = date,
                MovementType = (int)StockMovementType.Issue,
                SourceWarehouseId = model.SourceWarehouseId,
                ProjectId = model.ProjectId,
                Description = model.Description,
                Items = model.Items.Select(x => new StockMovementItemRequest
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity
                }).ToList()
            }, tenantId);

            TempData["Success"] = "پیش‌نویس مصرف اقلام پروژه ثبت شد؛ پس از بررسی، آن را قطعی کنید تا از موجودی انبار کسر شود.";
            return RedirectToAction(nameof(Index), new { projectId = model.ProjectId, movementId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateOptionsAsync(model, tenantId);
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "StockMovement.Post")]
    public async Task<IActionResult> Post(int projectId, int id)
    {
        var tenantId = RequireTenant();
        var isProjectMovement = await IsProjectMovementAsync(id, projectId, tenantId);
        if (!isProjectMovement) return NotFound();

        try
        {
            await _movements.PostAsync(id, tenantId);
            TempData["Success"] = "مصرف قطعی شد و مقدار اقلام از موجودی انبار کسر شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { projectId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "StockMovement.Post")]
    public async Task<IActionResult> Reverse(int projectId, int id)
    {
        var tenantId = RequireTenant();
        if (!await IsProjectMovementAsync(id, projectId, tenantId)) return NotFound();

        try
        {
            await _movements.ReverseAsync(id, tenantId);
            TempData["Success"] = "برگشت از قطعی ثبت شد؛ اثر حواله با سند معکوس جبران شد و سابقه حفظ شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { projectId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "StockMovement.Delete")]
    public async Task<IActionResult> Delete(int projectId, int id)
    {
        var tenantId = RequireTenant();
        if (!await IsProjectMovementAsync(id, projectId, tenantId)) return NotFound();

        try
        {
            await _movements.DeleteAsync(id, tenantId, User.FindFirstValue(ClaimTypes.NameIdentifier));
            TempData["Success"] = "سند از فهرست حذف شد؛ سوابق انبار برای حسابرسی نگهداری شده‌اند.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { projectId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "StockMovement.Create")]
    public async Task<IActionResult> Cancel(int projectId, int id)
    {
        var tenantId = RequireTenant();
        var isProjectMovement = await IsProjectMovementAsync(id, projectId, tenantId);
        if (!isProjectMovement) return NotFound();

        try
        {
            await _movements.CancelAsync(id, tenantId);
            TempData["Success"] = "پیش‌نویس مصرف لغو شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { projectId });
    }

    private int RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("شرکت جاری مشخص نیست.");

    private Task<bool> IsProjectMovementAsync(int id, int projectId, int tenantId) =>
        _db.StockMovements.IgnoreQueryFilters().AnyAsync(x =>
            x.Id == id && x.TenantId == tenantId && !x.IsDeleted &&
            (x.ProjectId == projectId || (x.ReversesMovementId.HasValue &&
                _db.StockMovements.IgnoreQueryFilters().Any(original =>
                    original.Id == x.ReversesMovementId.Value && original.TenantId == tenantId && original.ProjectId == projectId))));

    private async Task PopulateOptionsAsync(ProjectMaterialFormViewModel model, int tenantId)
    {
        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name).ToListAsync();
        model.Warehouses = warehouses.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList();
        model.SourceWarehouseId ??= warehouses.FirstOrDefault(x => x.IsDefault)?.Id;

        model.Consumables = await _db.Products.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.TrackInventory &&
                x.ProductType == ProductType.Consumable)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Code == null ? x.Name : $"{x.Name} ({x.Code})", x.Id.ToString()))
            .ToListAsync();
    }
}
