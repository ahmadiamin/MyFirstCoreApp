using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Tenancy.ViewModels;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Security.Claims;

namespace MyFirstCoreApp.Areas.Tenancy.Controllers;

[Area("Tenancy")]
[Authorize]
public class TenantController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantSmsSettingsService _smsSettings;

    public TenantController(
        ApplicationDbContext context,
        ITenantService tenantService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICurrentUser currentUser,
        ITenantSmsSettingsService smsSettings)
    {
        _context = context;
        _tenantService = tenantService;
        _userManager = userManager;
        _signInManager = signInManager;
        _currentUser = currentUser;
        _smsSettings = smsSettings;
    }

    // GET: /Tenancy/Tenant
    [HttpGet]
    [Authorize(Policy = "Tenancy.View")]
    public async Task<IActionResult> Index()
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var tenants = User.IsInRole("SuperAdmin")
            ? await _context.Tenants.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync()
            : await _tenantService.GetUserTenantsAsync(userId);
        return View(tenants);
    }

    // GET: /Tenancy/Tenant/Create
    [HttpGet]
    [Authorize]
    public IActionResult Create()
    {
        return View(new CreateTenantViewModel());
    }

    // POST: /Tenancy/Tenant/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Create(CreateTenantViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var tenant = new Tenant
        {
            Name = model.Name.Trim(),
            LegalName = model.LegalName?.Trim(),
            NationalId = model.NationalId?.Trim(),
            EconomicCode = model.EconomicCode?.Trim(),
            Phone = model.Phone?.Trim(),
            Address = model.Address?.Trim(),
            IsActive = true,
            DefaultCurrency = "IRR",
            TimeZone = "Iran Standard Time"
        };

        await _tenantService.CreateAsync(tenant, userId);

        // 🆕 بعد از ساخت، Claim رو ست کن و SignIn مجدد
        await SetCurrentTenantAndSignIn(userId, tenant.Id);

        return RedirectToAction("Index", "Dashboard", new { area = "" });
    }

    // GET: /Tenancy/Tenant/Select
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Select()
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var tenants = User.IsInRole("SuperAdmin")
            ? await _context.Tenants.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.Name).ToListAsync()
            : await _tenantService.GetUserTenantsAsync(userId);

        // حالت ۱: هیچ Tenant نداره → Create
        if (!tenants.Any())
            return RedirectToAction(nameof(Create));

        // حالت ۲: فقط یک Tenant داره → خودکار انتخاب
        if (tenants.Count == 1)
        {
            await SetCurrentTenantAndSignIn(userId, tenants[0].Id);
            return RedirectToAction("Index", "Dashboard", new { area = "" });
        }

        // حالت ۳: چند Tenant داره → ببین پیش‌فرض داره؟
        var userTenants = await _tenantService.GetUserTenantRelationsAsync(userId);
        var defaultRelation = userTenants.FirstOrDefault(ut => ut.IsDefault);

        if (defaultRelation != null)
        {
            // پیش‌فرض داره → خودکار انتخاب
            await SetCurrentTenantAndSignIn(userId, defaultRelation.TenantId);
            return RedirectToAction("Index", "Dashboard", new { area = "" });
        }

        // حالت ۴: چند Tenant داره ولی پیش‌فرض نداره → Select View
        var vm = new List<SelectTenantViewModel>();
        foreach (var t in tenants)
        {
            var isOwner = await _tenantService.IsUserOwnerAsync(t.Id, userId);
            vm.Add(new SelectTenantViewModel
            {
                TenantId = t.Id,
                TenantName = t.Name,
                IsOwner = isOwner
            });
        }

        return View(vm);
    }

    // POST: /Tenancy/Tenant/Select
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Select(int tenantId)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!User.IsInRole("SuperAdmin") && !await _tenantService.IsUserMemberAsync(tenantId, userId))
            return Forbid();

        // ست کردن به‌عنوان پیش‌فرض
        await _tenantService.SetDefaultTenantAsync(userId, tenantId);

        // ست کردن Claim + SignIn مجدد
        await SetCurrentTenantAndSignIn(userId, tenantId);

        return RedirectToAction("Index", "Dashboard", new { area = "" });
    }

    /// <summary>
    /// بعد از انتخاب Tenant، Claim رو ست می‌کنه و SignIn مجدد می‌کنه
    /// </summary>
    private async Task SetCurrentTenantAndSignIn(string userId, int tenantId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        var tenant = await _tenantService.GetByIdAsync(tenantId);
        if (tenant == null) return;

        // حذف Claim های قدیمی
        var claims = await _userManager.GetClaimsAsync(user);
        var oldTenantId = claims.FirstOrDefault(c => c.Type == "TenantId");
        if (oldTenantId != null)
            await _userManager.RemoveClaimAsync(user, oldTenantId);

        var oldTenantName = claims.FirstOrDefault(c => c.Type == "TenantName");
        if (oldTenantName != null)
            await _userManager.RemoveClaimAsync(user, oldTenantName);

        // اضافه کردن Claim های جدید
        await _userManager.AddClaimAsync(user, new Claim("TenantId", tenantId.ToString()));
        await _userManager.AddClaimAsync(user, new Claim("TenantName", tenant.Name));

        // 🆕 SignIn مجدد (کوکی با Claim های جدید بازسازی می‌شه)
        await _signInManager.RefreshSignInAsync(user);
    }

    // GET: /Tenancy/Tenant/Select/5
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> SelectById(int tenantId)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!await _tenantService.IsUserMemberAsync(tenantId, userId))
            return Forbid();

        await _tenantService.SetDefaultTenantAsync(userId, tenantId);
        await SetCurrentTenantAndSignIn(userId, tenantId);

        return RedirectToAction("Index", "Dashboard", new { area = "" });
    }
    // POST: /Tenancy/Tenant/GetData
    [HttpPost]
    [Authorize(Policy = "Tenancy.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId)) return Json(new { success = false });

        // 🆕 فقط Tenant هایی که کاربر عضوشونه
        var query = User.IsInRole("SuperAdmin")
            ? _context.Tenants.IgnoreQueryFilters().Where(t => !t.IsDeleted)
            : _context.UserTenants
                .Where(ut => ut.UserId == userId && !ut.IsDeleted)
                .Select(ut => ut.Tenant)
                .Where(t => !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(t =>
                (t.Name != null && t.Name.Contains(s)) ||
                (t.LegalName != null && t.LegalName.Contains(s)) ||
                (t.NationalId != null && t.NationalId.Contains(s)) ||
                (t.Phone != null && t.Phone.Contains(s)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "name" => request.SortDir == "asc" ? query.OrderBy(t => t.Name) : query.OrderByDescending(t => t.Name),
            _ => query.OrderBy(t => t.Name)
        };

        var items = await query
            .Skip(request.StartRow)
            .Take(request.EndRow - request.StartRow)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                legalName = t.LegalName,
                nationalId = t.NationalId,
                phone = t.Phone,
                isActive = t.IsActive
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }
    // GET: /Tenancy/Tenant/Edit/5
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        // چک کن کاربر مالک یا عضو این Tenant هست
        if (!User.IsInRole("SuperAdmin") && !await _tenantService.IsUserOwnerAsync(id, userId))
            return Forbid();

        var entity = await _tenantService.GetByIdAsync(id);
        if (entity == null) return NotFound();

        var model = new CreateTenantViewModel   // 👈 همون ViewModel قبلی
        {
            Name = entity.Name,
            LegalName = entity.LegalName,
            NationalId = entity.NationalId,
            EconomicCode = entity.EconomicCode,
            Phone = entity.Phone,
            Address = entity.Address,
            SmsLineNumber = string.Empty
        };

        await PopulateSmsSettingsSummaryAsync(id, model);

        ViewBag.TenantId = id;

        return View(model);
    }

    // POST: /Tenancy/Tenant/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Edit(int id, CreateTenantViewModel model)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!User.IsInRole("SuperAdmin") && !await _tenantService.IsUserOwnerAsync(id, userId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(model.SmsLineNumber))
            ModelState.AddModelError(nameof(model.SmsLineNumber), "شماره خط پیامک را وارد کنید.");

        var validReminderTime = TimeOnly.TryParse(model.SmsChequeReminderTime, out var chequeReminderTime);
        if (!validReminderTime)
            ModelState.AddModelError(nameof(model.SmsChequeReminderTime), "ساعت ارسال معتبر نیست.");
        if (model.SmsChequeReminderEnabled &&
            (string.IsNullOrWhiteSpace(model.SmsChequeReminderText) ||
             new[] { "%var1%", "%var2%", "%var3%", "%var4%" }
                 .Any(token => !model.SmsChequeReminderText.Contains(token, StringComparison.Ordinal))))
            ModelState.AddModelError(nameof(model.SmsChequeReminderText),
                "الگوی فعال باید شامل متغیرهای %var1% تا %var4% باشد.");
        if (model.SmsChequeReminderEnabled &&
            !model.SmsChequeReminderDayBeforeEnabled && !model.SmsChequeReminderDueDayEnabled)
            ModelState.AddModelError(nameof(model.SmsChequeReminderEnabled),
                "حداقل یکی از زمان‌های یادآوری را انتخاب کنید.");
        if (model.SmsChequeReminderEnabled &&
            !System.Text.RegularExpressions.Regex.IsMatch(NormalizeMobileDigits(model.SmsChequeReminderMobile),
                "^09[0-9]{9}$"))
            ModelState.AddModelError(nameof(model.SmsChequeReminderMobile),
                "شماره موبایل شرکت باید ۱۱ رقم و با 09 شروع شود.");

        if (!ModelState.IsValid)
        {
            ViewBag.TenantId = id;
            await PopulateSmsSettingsSummaryAsync(id, model, preserveReminderInput: true);
            return View(model);
        }

        if (model.ClearSmsApiKey && !string.IsNullOrWhiteSpace(model.SmsApiKeyInput))
        {
            ModelState.AddModelError(nameof(model.SmsApiKeyInput), "برای جایگزینی کلید، گزینهٔ پاک‌کردن را خالی بگذارید.");
            ViewBag.TenantId = id;
            await PopulateSmsSettingsSummaryAsync(id, model, preserveReminderInput: true);
            return View(model);
        }
        if (!string.IsNullOrWhiteSpace(model.SmsPortalLinkText) &&
            !model.SmsPortalLinkText.Contains("%var1%", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.SmsPortalLinkText), "الگوی پیامک باید شامل متغیر %var1% باشد.");
            ViewBag.TenantId = id;
            await PopulateSmsSettingsSummaryAsync(id, model, preserveReminderInput: true);
            return View(model);
        }

        var entity = await _tenantService.GetByIdAsync(id);
        if (entity == null) return NotFound();

        entity.Name = model.Name.Trim();
        entity.LegalName = model.LegalName?.Trim();
        entity.NationalId = model.NationalId?.Trim();
        entity.EconomicCode = model.EconomicCode?.Trim();
        entity.Phone = model.Phone?.Trim();
        entity.Address = model.Address?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        var currentSmsSettings = await _smsSettings.GetSummaryAsync(id);
        var smsLineNumber = model.SmsLineNumber ?? string.Empty;
        var smsSettingsChanged = model.ClearSmsApiKey ||
            !string.IsNullOrWhiteSpace(model.SmsApiKeyInput) ||
            !string.Equals(smsLineNumber.Trim(), currentSmsSettings.LineNumber, StringComparison.Ordinal) ||
            !string.Equals(model.SmsPortalLinkText?.Trim(), currentSmsSettings.PortalLinkText, StringComparison.Ordinal) ||
            model.SmsChequeReminderEnabled != currentSmsSettings.ChequeReminderEnabled ||
            model.SmsChequeReminderDayBeforeEnabled != currentSmsSettings.ChequeReminderDayBeforeEnabled ||
            model.SmsChequeReminderDueDayEnabled != currentSmsSettings.ChequeReminderDueDayEnabled ||
            chequeReminderTime != currentSmsSettings.ChequeReminderTime ||
            !string.Equals(NormalizeMobileDigits(model.SmsChequeReminderMobile),
                currentSmsSettings.ChequeReminderMobile, StringComparison.Ordinal) ||
            !string.Equals(model.SmsChequeReminderText?.Trim(), currentSmsSettings.ChequeReminderText, StringComparison.Ordinal);
        if (smsSettingsChanged)
        {
            await _smsSettings.SaveAsync(id, model.SmsApiKeyInput, model.ClearSmsApiKey,
                smsLineNumber, model.SmsPortalLinkText, userId,
                model.SmsChequeReminderEnabled, model.SmsChequeReminderDayBeforeEnabled,
                model.SmsChequeReminderDueDayEnabled, chequeReminderTime,
                model.SmsChequeReminderMobile, model.SmsChequeReminderText);
        }

        await _tenantService.UpdateAsync(entity);

        TempData["Success"] = "اطلاعات شرکت با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSmsSettingsSummaryAsync(int tenantId, CreateTenantViewModel model,
        bool preserveReminderInput = false)
    {
        var settings = await _smsSettings.GetSummaryAsync(tenantId);
        model.SmsApiKeyConfigured = settings.ApiKeyConfigured;
        if (string.IsNullOrWhiteSpace(model.SmsLineNumber))
            model.SmsLineNumber = settings.LineNumber;
        if (model.SmsPortalLinkText is null)
            model.SmsPortalLinkText = settings.PortalLinkText;
        if (!preserveReminderInput)
        {
            model.SmsChequeReminderEnabled = settings.ChequeReminderEnabled;
            model.SmsChequeReminderDayBeforeEnabled = settings.ChequeReminderDayBeforeEnabled;
            model.SmsChequeReminderDueDayEnabled = settings.ChequeReminderDueDayEnabled;
            model.SmsChequeReminderTime = settings.ChequeReminderTime.ToString("HH:mm");
            model.SmsChequeReminderMobile = settings.ChequeReminderMobile;
            model.SmsChequeReminderText = settings.ChequeReminderText;
        }
    }

    private static string NormalizeMobileDigits(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
        var result = new System.Text.StringBuilder(mobile.Length);
        foreach (var character in mobile.Trim())
        {
            if (character is >= '\u06F0' and <= '\u06F9') result.Append((char)('0' + character - '\u06F0'));
            else if (character is >= '\u0660' and <= '\u0669') result.Append((char)('0' + character - '\u0660'));
            else result.Append(character);
        }
        return result.ToString();
    }
}
