using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Areas.Company.ViewModels;

namespace MyFirstCoreApp.Areas.Company.Controllers;

/// <summary>
/// سمت‌های «همین شرکت» (کارشناس فروش، منشی، مدیر فروشگاه، ...) و دسترسی‌هایشان.
/// شناسه‌ی شرکت همیشه از شرکت فعال می‌آید، هرگز از ورودی کاربر.
/// </summary>
[Area("Company")]
[Authorize]
public class PositionsController : Controller
{
    private const string NoTenantMessage = "شرکت فعال مشخص نیست.";
    private const string UnexpectedErrorMessage = "خطای غیرمنتظره رخ داد. لطفاً دوباره تلاش کنید.";

    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantSecurityService _security;
    private readonly ILogger<PositionsController> _logger;

    public PositionsController(
        ICurrentTenant currentTenant,
        ITenantSecurityService security,
        ILogger<PositionsController> logger)
    {
        _currentTenant = currentTenant;
        _security = security;
        _logger = logger;
    }

    [Authorize(Policy = "Role.View")]
    public async Task<IActionResult> Index()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        return View(await _security.GetRolesAsync(tenantId.Value));
    }

    [Authorize(Policy = "Role.Create")]
    public async Task<IActionResult> Create()
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();

        return View(new PositionFormViewModel { Groups = await _security.GetAssignablePermissionsAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Role.Create")]
    public async Task<IActionResult> Create(PositionFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (ModelState.IsValid)
        {
            try
            {
                await _security.CreateRoleAsync(tenantId.Value, ToRequest(model));

                TempData["Success"] = "سمت ثبت شد.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create position failed");
                ModelState.AddModelError("", UnexpectedErrorMessage);
            }
        }

        model.Groups = await _security.GetAssignablePermissionsAsync();
        return View(model);
    }

    [Authorize(Policy = "Role.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var role = await _security.GetRoleAsync(tenantId.Value, id);
        if (role == null) return NotFound();

        return View(new PositionFormViewModel
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            SelectedPermissions = role.PermissionNames,
            Groups = await _security.GetAssignablePermissionsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Role.Edit")]
    public async Task<IActionResult> Edit(int id, PositionFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        if (ModelState.IsValid)
        {
            try
            {
                await _security.UpdateRoleAsync(tenantId.Value, id, ToRequest(model));

                TempData["Success"] = "سمت ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit position failed");
                ModelState.AddModelError("", UnexpectedErrorMessage);
            }
        }

        model.Groups = await _security.GetAssignablePermissionsAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Role.Edit")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        try
        {
            await _security.DeleteRoleAsync(tenantId.Value, id);
            TempData["Success"] = "سمت حذف شد و از افراد برداشته شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete position failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    private static TenantRoleRequest ToRequest(PositionFormViewModel model) => new()
    {
        Name = model.Name,
        Description = model.Description,
        IsActive = model.IsActive,
        PermissionNames = model.SelectedPermissions ?? new List<string>()
    };
}