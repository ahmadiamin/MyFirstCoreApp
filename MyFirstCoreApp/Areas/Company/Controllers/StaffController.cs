using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Areas.Company.ViewModels;

namespace MyFirstCoreApp.Areas.Company.Controllers;

/// <summary>
/// افراد «همین شرکت» با نام، فامیل، عنوان شغلی، تلفن داخلی و سمت‌هایشان.
/// فقط عضوِ شرکت فعال دیده/ویرایش می‌شود؛ کاربرِ شرکت دیگر «پیدا نشد» است (سرویس تضمین می‌کند).
/// </summary>
[Area("Company")]
[Authorize]
public class StaffController : Controller
{
    private const string UnexpectedErrorMessage = "خطای غیرمنتظره رخ داد. لطفاً دوباره تلاش کنید.";

    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantSecurityService _security;
    private readonly ITenantStaffService _staff;
    private readonly ITenantAccessService _access;
    private readonly ILogger<StaffController> _logger;

    public StaffController(
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ITenantSecurityService security,
        ITenantStaffService staff,
        ITenantAccessService access,
        ILogger<StaffController> logger)
    {
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _security = security;
        _staff = staff;
        _access = access;
        _logger = logger;
    }

    // ============================================
    // لیست
    // ============================================

    [Authorize(Policy = "User.View")]
    public async Task<IActionResult> Index()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        return View(await _security.GetMembersAsync(tenantId.Value));
    }

    // ============================================
    // دفترچه‌ی تلفن داخلی (برای همه‌ی اعضای شرکت)
    // ============================================

    /// <summary>
    /// نام، سمت و تلفن داخلی همکاران؛ برای هر عضوِ شرکت (بدون نیاز به دسترسی مدیریت کاربران).
    /// ایمیل و موبایل نشان داده نمی‌شود.
    /// </summary>
    public async Task<IActionResult> Directory()
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        if (!tenantId.HasValue || string.IsNullOrEmpty(userId)) return Forbid();

        if (!await _access.IsMemberAsync(userId, tenantId.Value))
            return Forbid();

        var members = await _security.GetMembersAsync(tenantId.Value);
        return View(members.Where(m => m.UserIsActive).ToList());
    }

    // ============================================
    // افزودن
    // ============================================

    [Authorize(Policy = "User.Create")]
    public async Task<IActionResult> Create()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        return View(new StaffCreateViewModel { RoleOptions = await ActiveRolesAsync(tenantId.Value) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "User.Create")]
    public async Task<IActionResult> Create(StaffCreateViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (ModelState.IsValid)
        {
            try
            {
                var result = await _staff.AddMemberAsync(tenantId.Value, new MemberCreateRequest
                {
                    Email = model.Email,
                    Password = model.Password,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    JobTitle = model.JobTitle,
                    InternalPhone = model.InternalPhone,
                    PhoneNumber = model.PhoneNumber,
                    RoleIds = model.SelectedRoleIds ?? new List<int>()
                });

                TempData["Success"] = result.CreatedNewAccount
                    ? "فرد جدید و حساب کاربری‌اش ثبت شد."
                    : "این ایمیل از قبل حساب داشت؛ به شرکت اضافه شد (رمز و نام حساب او تغییر نکرد).";

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Add staff member failed");
                ModelState.AddModelError("", UnexpectedErrorMessage);
            }
        }

        model.RoleOptions = await ActiveRolesAsync(tenantId.Value);
        return View(model);
    }

    // ============================================
    // ویرایش
    // ============================================

    [Authorize(Policy = "User.Edit")]
    public async Task<IActionResult> Edit(string id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var member = await _security.GetMemberAsync(tenantId.Value, id);
        if (member == null) return NotFound();

        return View(new StaffEditViewModel
        {
            UserId = member.UserId,
            Email = member.Email ?? string.Empty,
            IsOwner = member.IsOwner,
            FirstName = member.FirstName ?? string.Empty,
            LastName = member.LastName ?? string.Empty,
            JobTitle = member.JobTitle,
            InternalPhone = member.InternalPhone,
            SelectedRoleIds = member.Roles.Select(r => r.RoleId).ToList(),
            RoleOptions = await ActiveRolesAsync(tenantId.Value)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "User.Edit")]
    public async Task<IActionResult> Edit(string id, StaffEditViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.UserId) return BadRequest();

        if (ModelState.IsValid)
        {
            try
            {
                await _security.UpdateMemberProfileAsync(tenantId.Value, id, new MemberProfileRequest
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    JobTitle = model.JobTitle,
                    InternalPhone = model.InternalPhone
                });

                // مالک نقش نمی‌گیرد (همه‌ی دسترسی‌های شرکت را دارد)
                var member = await _security.GetMemberAsync(tenantId.Value, id);
                if (member != null && !member.IsOwner)
                    await _security.SetMemberRolesAsync(tenantId.Value, id, model.SelectedRoleIds ?? new List<int>());

                TempData["Success"] = "مشخصات و سمت‌ها ذخیره شد.";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edit staff member failed");
                ModelState.AddModelError("", UnexpectedErrorMessage);
            }
        }

        model.RoleOptions = await ActiveRolesAsync(tenantId.Value);
        return View(model);
    }

    // ============================================
    // حذف از شرکت
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "User.Edit")]
    public async Task<IActionResult> Remove(string id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id == _currentUser.UserId)
        {
            TempData["Error"] = "نمی‌توانید خودتان را از شرکت حذف کنید.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _security.RemoveMemberAsync(tenantId.Value, id);
            TempData["Success"] = "فرد از شرکت حذف شد (حساب کاربری‌اش باقی می‌ماند).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remove staff member failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<TenantRoleSummary>> ActiveRolesAsync(int tenantId)
        => (await _security.GetRolesAsync(tenantId)).Where(r => r.IsActive).ToList();
}