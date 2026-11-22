using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Areas.Inventory.ViewModels.CompanyAssets;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory"), Authorize]
public sealed class CompanyAssetsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICompanyAssetService _service;
    private readonly ICurrentTenant _tenant;
    private readonly TimeProvider _time;
    private readonly IAuthorizationService _authorization;

    public CompanyAssetsController(ApplicationDbContext db, ICompanyAssetService service, ICurrentTenant tenant, TimeProvider time, IAuthorizationService authorization)
    {
        _db = db;
        _service = service;
        _tenant = tenant;
        _time = time;
        _authorization = authorization;
    }

    [Authorize(Policy = "CompanyAsset.View")]
    public async Task<IActionResult> Index()
    {
        return View(await _service.GetListAsync(RequireTenant()));
    }

    [Authorize(Policy = "CompanyAsset.Create")]
    public async Task<IActionResult> Create()
    {
        var model = new CompanyAssetFormViewModel();
        await LoadProductOptionsAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CompanyAsset.Create")]
    public async Task<IActionResult> Create(CompanyAssetFormViewModel model)
    {
        DateOnly? acquisitionDate = null;
        if (!string.IsNullOrWhiteSpace(model.AcquisitionDateText))
        {
            if (PersianDate.TryParse(model.AcquisitionDateText, out var parsed)) acquisitionDate = parsed;
            else ModelState.AddModelError(nameof(model.AcquisitionDateText), "تاریخ شمسی معتبر نیست.");
        }
        decimal? acquisitionCost = null;
        if (!string.IsNullOrWhiteSpace(model.AcquisitionCostText))
        {
            if (TryParseAmount(model.AcquisitionCostText, out var parsedCost)) acquisitionCost = parsedCost;
            else ModelState.AddModelError(nameof(model.AcquisitionCostText), "بهای خرید را به‌صورت عدد معتبر وارد کنید.");
        }

        if (!ModelState.IsValid)
        {
            await LoadProductOptionsAsync(model);
            return View(model);
        }

        try
        {
            var id = await _service.CreateAsync(new CompanyAssetRequest
            {
                ProductId = model.ProductId,
                SerialNumber = model.SerialNumber,
                AcquisitionDate = acquisitionDate,
                AcquisitionCost = acquisitionCost,
                Description = model.Description
            }, RequireTenant());
            TempData["Success"] = "ابزار یا دارایی با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadProductOptionsAsync(model);
            return View(model);
        }
    }

    [Authorize(Policy = "CompanyAsset.View")]
    public async Task<IActionResult> Details(int id)
    {
        var asset = await _service.GetByIdAsync(id, RequireTenant());
        if (asset == null) return NotFound();
        ViewBag.CanAssign = (await _authorization.AuthorizeAsync(User, null, "CompanyAsset.Assign")).Succeeded;
        ViewBag.CanReturn = (await _authorization.AuthorizeAsync(User, null, "CompanyAsset.Return")).Succeeded;
        ViewBag.CanDelete = (await _authorization.AuthorizeAsync(User, null, "CompanyAsset.Delete")).Succeeded;
        ViewBag.AssignmentForm = await BuildAssignmentFormAsync(asset.Id);
        ViewBag.ReturnForm = new CompanyAssetReturnFormViewModel
        {
            AssignmentId = asset.Assignments.FirstOrDefault(x => x.IsCurrent)?.Id ?? 0,
            ReturnedDateText = PersianDate.Format(_time.Today())
        };
        return View(asset);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CompanyAsset.Assign")]
    public async Task<IActionResult> Assign(CompanyAssetAssignmentFormViewModel model)
    {
        if (!PersianDate.TryParse(model.AssignedDateText, out var assignedDate))
            ModelState.AddModelError(nameof(model.AssignedDateText), "تاریخ شمسی معتبر نیست.");
        else if (!model.UserTenantId.HasValue)
            ModelState.AddModelError(string.Empty, "کارمند مسئول را برای تحویل ابزار انتخاب کنید.");

        if (ModelState.IsValid)
        {
            try
            {
                await _service.AssignAsync(new CompanyAssetAssignmentRequest
                {
                    CompanyAssetId = model.CompanyAssetId,
                    UserTenantId = model.UserTenantId,
                    AssignedDate = assignedDate,
                    Notes = model.Notes,
                    AssignedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                }, RequireTenant());
                TempData["Success"] = "تحویل ابزار ثبت شد.";
                return RedirectToAction(nameof(Details), new { id = model.CompanyAssetId });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        }
        else TempData["Error"] = ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = model.CompanyAssetId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CompanyAsset.Return")]
    public async Task<IActionResult> Return(CompanyAssetReturnFormViewModel model, int assetId)
    {
        if (!PersianDate.TryParse(model.ReturnedDateText, out var returnedDate))
            TempData["Error"] = "تاریخ برگشت شمسی معتبر نیست.";
        else
        {
            try
            {
                await _service.ReturnAsync(new CompanyAssetReturnRequest
                {
                    AssignmentId = model.AssignmentId,
                    ReturnedDate = returnedDate,
                    ReturnNotes = model.ReturnNotes,
                    ReturnedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                }, RequireTenant());
                TempData["Success"] = "برگشت ابزار ثبت شد و سابقه تحویل حفظ شد.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        }
        return RedirectToAction(nameof(Details), new { id = assetId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CompanyAsset.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id, RequireTenant(), User.FindFirstValue(ClaimTypes.NameIdentifier));
            TempData["Success"] = "ابزار یا دارایی حذف شد؛ سابقه‌های آن برای حفظ اطلاعات نگهداری شده‌اند.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private int RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("شرکت جاری مشخص نیست.");

    private async Task LoadProductOptionsAsync(CompanyAssetFormViewModel model)
    {
        var tenantId = RequireTenant();
        var products = await _db.Products.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive &&
                (x.ProductType == ProductType.Tool || x.ProductType == ProductType.FixedAsset))
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.ProductType })
            .ToListAsync();
        model.ProductOptions = products.Select(x => new SelectListItem
        {
            Value = x.Id.ToString(),
            Text = $"{x.Name} - {(x.ProductType == ProductType.Tool ? "ابزار" : "دارایی ثابت")}"
        }).ToList();
    }

    private async Task<CompanyAssetAssignmentFormViewModel> BuildAssignmentFormAsync(int assetId)
    {
        var tenantId = RequireTenant();
        var model = new CompanyAssetAssignmentFormViewModel
        {
            CompanyAssetId = assetId,
            AssignedDateText = PersianDate.Format(_time.Today()),
            UserOptions = await (
                from membership in _db.UserTenants.AsNoTracking()
                join user in _db.Users.AsNoTracking() on membership.UserId equals user.Id
                where membership.TenantId == tenantId && user.IsActive
                orderby membership.FirstName, membership.LastName
                select new SelectListItem
                {
                    Value = membership.Id.ToString(),
                    Text = (((membership.FirstName ?? "") + " " + (membership.LastName ?? "")).Trim() == "")
                        ? user.FullName : ((membership.FirstName ?? "") + " " + (membership.LastName ?? "")).Trim()
                }).ToListAsync(),
        };
        return model;
    }

    private static bool TryParseAmount(string? value, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var cleaned = PersianDate.NormalizeDigits(value).Replace(",", "").Replace("٬", "").Replace(" ", "");
        return decimal.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount >= 0;
    }
}
