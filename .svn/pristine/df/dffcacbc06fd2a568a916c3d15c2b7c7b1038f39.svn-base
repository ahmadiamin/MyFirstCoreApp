using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;
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

        public DashboardController(
            ApplicationDbContext context,
            ITenantService tenantService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _tenantService = tenantService;
            _userManager = userManager;
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
                // آمار کاربران این شرکت
                UserCount = await _context.UserTenants
                    .Where(ut => ut.TenantId == tenantId && !ut.IsDeleted)
                    .CountAsync(),

                RoleCount = await _context.Roles.CountAsync(),

                PermissionCount = await _context.Permissions
                    .CountAsync(x => x.IsActive),

                MenuCount = await _context.Menus
                    .CountAsync(x => x.IsActive),

                // اطلاعات Tenant
                TenantName = tenant?.Name ?? "",
                TenantLegalName = tenant?.LegalName,
                IsTenantOwner = isOwner,

                // آمار حسابداری (فعلاً ۰، بعداً پر می‌شه)
                ContactCount = 0,
                ProductCount = 0,
                InvoiceCount = 0,
                ChequeCount = 0,
                TotalSales = 0,
                TotalPurchases = 0
            };

            return View(model);
        }
    }
}