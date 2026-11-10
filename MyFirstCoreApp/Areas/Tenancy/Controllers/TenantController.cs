using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Tenancy.ViewModels;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
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
    private readonly IAttachmentService _attachments;

    public TenantController(
        ApplicationDbContext context,
        ITenantService tenantService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ICurrentUser currentUser,
        ITenantSmsSettingsService smsSettings,
        IAttachmentService attachments)
    {
        _context = context;
        _tenantService = tenantService;
        _userManager = userManager;
        _signInManager = signInManager;
        _currentUser = currentUser;
        _smsSettings = smsSettings;
        _attachments = attachments;
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
            LogoAttachmentId = entity.LogoAttachmentId,
            StampAttachmentId = entity.StampAttachmentId,
            SignatureAttachmentId = entity.SignatureAttachmentId,
            SmsLineNumber = string.Empty,
        };

        await PopulateSmsSettingsSummaryAsync(id, model);

        ViewBag.TenantId = id;
        ViewBag.IsCurrentTenant = _context.CurrentTenantId == id;

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
            !model.SmsChequeReminderDayBeforeEnabled && !model.SmsChequeReminderDueDayEnabled)
            ModelState.AddModelError(nameof(model.SmsChequeReminderEnabled),
                "حداقل یکی از زمان‌های یادآوری را انتخاب کنید.");

        if (model.SmsChequeReminderEnabled &&
            !System.Text.RegularExpressions.Regex.IsMatch(NormalizeMobileDigits(model.SmsChequeReminderMobile),"^09[0-9]{9}$"))
            ModelState.AddModelError(nameof(model.SmsChequeReminderMobile), "شماره موبایل شرکت باید ۱۱ رقم و با 09 شروع شود.");

        if (!ModelState.IsValid)
        {
            ViewBag.TenantId = id;
            ViewBag.IsCurrentTenant = _context.CurrentTenantId == id;
            await PopulateBrandingSummaryAsync(id, model);
            await PopulateSmsSettingsSummaryAsync(id, model, preserveReminderInput: true);
            return View(model);
        }

        if (model.ClearSmsApiKey && !string.IsNullOrWhiteSpace(model.SmsApiKeyInput))
        {
            ModelState.AddModelError(nameof(model.SmsApiKeyInput), "برای جایگزینی کلید، گزینهٔ پاک‌کردن را خالی بگذارید.");
            ViewBag.TenantId = id;
            ViewBag.IsCurrentTenant = _context.CurrentTenantId == id;
            await PopulateBrandingSummaryAsync(id, model);
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
            !string.Equals(model.PortalLinkPatternCode?.Trim(), currentSmsSettings.PortalLinkText, StringComparison.Ordinal) ||
            model.SmsChequeReminderEnabled != currentSmsSettings.ChequeReminderEnabled ||
            model.SmsChequeReminderDayBeforeEnabled != currentSmsSettings.ChequeReminderDayBeforeEnabled ||
            model.SmsChequeReminderDueDayEnabled != currentSmsSettings.ChequeReminderDueDayEnabled ||
            chequeReminderTime != currentSmsSettings.ChequeReminderTime ||
            !string.Equals(NormalizeMobileDigits(model.SmsChequeReminderMobile), currentSmsSettings.ChequeReminderMobile, StringComparison.Ordinal) ||
            !string.Equals(model.SmsChequeReminderPatternCode?.Trim(), currentSmsSettings.ChequeReminderPatternCode, StringComparison.Ordinal);
        if (smsSettingsChanged)
        {
            await _smsSettings.SaveAsync(id, model.SmsApiKeyInput, model.ClearSmsApiKey,
                smsLineNumber, model.PortalLinkPatternCode, userId,
                model.SmsChequeReminderEnabled, model.SmsChequeReminderDayBeforeEnabled,
                model.SmsChequeReminderDueDayEnabled, chequeReminderTime,
                model.SmsChequeReminderMobile, model.SmsChequeReminderPatternCode);
        }

        await _tenantService.UpdateAsync(entity);

        TempData["Success"] = "اطلاعات شرکت با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Tenancy/Tenant/UploadBranding
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadBranding(int id, string kind, IFormFile? file)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!User.IsInRole("SuperAdmin") && !await _tenantService.IsUserOwnerAsync(id, userId))
            return Forbid();

        // سرویس پیوست‌ها فقط روی شرکت فعال کار می‌کند؛ شناسه‌ی شرکت مقصد باید همان شرکت فعال باشد.
        if (_context.CurrentTenantId != id)
            return Forbid();

        var tenant = await _tenantService.GetByIdAsync(id);
        if (tenant == null)
            return NotFound();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "لطفاً یک تصویر انتخاب کنید.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            TempData["Error"] = "فقط تصاویر JPG، PNG و WEBP مجاز هستند.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        int? oldAttachmentId;
        switch (kind)
        {
            case "logo": oldAttachmentId = tenant.LogoAttachmentId; break;
            case "stamp": oldAttachmentId = tenant.StampAttachmentId; break;
            case "signature": oldAttachmentId = tenant.SignatureAttachmentId; break;
            default: return BadRequest();
        }

        int newAttachmentId;
        try
        {
            await using var stream = file.OpenReadStream();
            var attachment = await _attachments.AddAsync(AttachmentEntityType.Tenant, id, new AttachmentUpload
            {
                FileName = file.FileName,
                Length = file.Length,
                Content = stream,
                Description = $"Tenant {kind}"
            });
            newAttachmentId = attachment.Id;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }

        switch (kind)
        {
            case "logo": tenant.LogoAttachmentId = newAttachmentId; break;
            case "stamp": tenant.StampAttachmentId = newAttachmentId; break;
            case "signature": tenant.SignatureAttachmentId = newAttachmentId; break;
        }

        try
        {
            tenant.UpdatedAt = DateTime.UtcNow;
            await _tenantService.UpdateAsync(tenant);
        }
        catch
        {
            // اگر اتصال فایل جدید به شرکت ذخیره نشد، رکورد جدید را حذف نرم می‌کنیم.
            await _attachments.DeleteAsync(newAttachmentId);
            throw;
        }

        // فایل قبلی فقط پس از ثبت موفق شناسه‌ی جدید حذف نرم می‌شود.
        if (oldAttachmentId.HasValue)
        {
            try
            {
                await _attachments.DeleteAsync(oldAttachmentId.Value);
            }
            catch (InvalidOperationException)
            {
                // ممکن است رکورد قبلی از قبل حذف شده باشد؛ فایل جدید معتبر باقی می‌ماند.
            }
        }

        TempData["Success"] = "تصویر با موفقیت ذخیره شد.";

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> BrandingImage(int id, string kind)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Challenge();

        if (!User.IsInRole("SuperAdmin") && !await _tenantService.IsUserOwnerAsync(id, userId))
            return Forbid();

        if (_context.CurrentTenantId != id)
            return Forbid();

        var tenant = await _tenantService.GetByIdAsync(id);
        if (tenant == null)
            return NotFound();

        var attachmentId = kind switch
        {
            "logo" => tenant.LogoAttachmentId,
            "stamp" => tenant.StampAttachmentId,
            "signature" => tenant.SignatureAttachmentId,
            _ => null
        };

        if (!attachmentId.HasValue)
            return NotFound();

        try
        {
            var info = await _attachments.GetInfoAsync(attachmentId.Value);
            if (info.EntityType != AttachmentEntityType.Tenant || info.EntityId != id ||
                !info.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var file = await _attachments.OpenAsync(attachmentId.Value);
            return File(file.Content, file.ContentType);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    private async Task PopulateBrandingSummaryAsync(int tenantId, CreateTenantViewModel model)
    {
        var tenant = await _tenantService.GetByIdAsync(tenantId);
        if (tenant == null) return;

        model.LogoAttachmentId = tenant.LogoAttachmentId;
        model.StampAttachmentId = tenant.StampAttachmentId;
        model.SignatureAttachmentId = tenant.SignatureAttachmentId;
    }

    private async Task PopulateSmsSettingsSummaryAsync(int tenantId, CreateTenantViewModel model,
        bool preserveReminderInput = false)
    {
        var settings = await _smsSettings.GetSummaryAsync(tenantId);
        model.SmsApiKeyConfigured = settings.ApiKeyConfigured;
        if (string.IsNullOrWhiteSpace(model.SmsLineNumber))
            model.SmsLineNumber = settings.LineNumber;
        if (model.PortalLinkPatternCode is null)
            model.PortalLinkPatternCode = settings.PortalLinkText;
        if (!preserveReminderInput)
        {
            model.SmsChequeReminderEnabled = settings.ChequeReminderEnabled;
            model.SmsChequeReminderDayBeforeEnabled = settings.ChequeReminderDayBeforeEnabled;
            model.SmsChequeReminderDueDayEnabled = settings.ChequeReminderDueDayEnabled;
            model.SmsChequeReminderTime = settings.ChequeReminderTime.ToString("HH:mm");
            model.SmsChequeReminderMobile = settings.ChequeReminderMobile;
            model.SmsChequeReminderPatternCode = settings.ChequeReminderPatternCode;
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
