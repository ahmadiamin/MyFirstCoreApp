using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Models.ViewModels;

namespace MyFirstCoreApp.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDashboardService _dashboardService;
        private readonly IAuthorizationService _authorization;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ApplicationDbContext context,
            ITenantService tenantService,
            UserManager<ApplicationUser> userManager,
            IDashboardService dashboardService,
            IAuthorizationService authorization,
            TimeProvider timeProvider,
            ILogger<DashboardController> logger)
        {
            _context = context;
            _tenantService = tenantService;
            _userManager = userManager;
            _dashboardService = dashboardService;
            _authorization = authorization;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Challenge();

            // چک Tenant پیش‌فرض
            var tenantIdClaim = User.FindFirst("TenantId")?.Value;

            if (string.IsNullOrEmpty(tenantIdClaim))
            {
                var tenants = await _tenantService.GetUserTenantsAsync(userId);

                if (!tenants.Any())
                    return RedirectToAction("Create", "Tenant", new { area = "Tenancy" });

                return RedirectToAction("Select", "Tenant", new { area = "Tenancy" });
            }

            var tenantId = int.Parse(tenantIdClaim);

            // اطلاعات Tenant
            var tenant = await _tenantService.GetByIdAsync(tenantId);
            var isOwner = await _tenantService.IsUserOwnerAsync(tenantId, userId);

            var model = new DashboardViewModel
            {
                TenantName = tenant?.Name ?? "",
                TenantLegalName = tenant?.LegalName,
                IsTenantOwner = isOwner,

                // ویجت‌ها فقط برای کاربرانِ دارای دسترسیِ مربوطه (همان دسترسی‌های موجود)
                ShowSystemStats = await HasAsync("User.View"),
                ShowFinance = await HasAsync("Report.Ledger"),
                ShowCheques = await HasAsync("Cheque.View"),
                ShowInvoices = await HasAsync("Invoice.View")
            };

            // آمار سیستم (کاربران این شرکت؛ نقش‌ها/دسترسی‌ها/منوها سراسری‌اند، فقط برای دارندگان User.View)
            if (model.ShowSystemStats)
            {
                model.UserCount = await _context.UserTenants
                    .Where(ut => ut.TenantId == tenantId && !ut.IsDeleted)
                    .CountAsync();

                model.RoleCount = await _context.Roles.CountAsync();
                model.PermissionCount = await _context.Permissions.CountAsync(x => x.IsActive);
                model.MenuCount = await _context.Menus.CountAsync(x => x.IsActive);
            }

            // شمارنده‌های حسابداری (مقدار واقعی؛ قبلاً همه ۰ بودند)
            model.ContactCount = await _context.Contacts
                .CountAsync(c => c.TenantId == tenantId && c.IsActive);

            model.ProductCount = await _context.Products
                .CountAsync(p => p.TenantId == tenantId && p.IsActive);

            model.InvoiceCount = await _context.Invoices
                .CountAsync(i => i.TenantId == tenantId);

            model.ChequeCount = await _context.Cheques
                .CountAsync(c => c.TenantId == tenantId
                              && (c.Status == ChequeStatus.InProgress || c.Status == ChequeStatus.RePresented));

            // خلاصه‌ی مالی: اگر نشد (مثلاً Migration پروسیجرها هنوز اجرا نشده)،
            // داشبورد نمی‌شکند؛ فقط بخش مالی نمایش داده نمی‌شود و خطا لاگ می‌شود.
            if (model.ShowFinance || model.ShowCheques || model.ShowInvoices)
            {
                try
                {
                    var summary = await _dashboardService.GetSummaryAsync();

                    model.Summary = summary;
                    model.TotalSales = summary.SalesTotal;
                    model.TotalPurchases = summary.PurchasesTotal;
                    model.FiscalRangeText =
                        $"{PersianDate.Format(summary.FiscalFrom)} تا {PersianDate.Format(summary.FiscalTo)}";
                    model.TodayText = PersianDate.Format(summary.Today);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خلاصه‌ی مالی داشبورد در دسترس نبود");
                }
            }

            if (string.IsNullOrEmpty(model.TodayText))
                model.TodayText = PersianDate.Format(_timeProvider.Today());

            return View(model);
        }

        private async Task<bool> HasAsync(string policy)
            => (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }
}
