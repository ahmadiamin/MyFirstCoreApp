using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Services.Interfaces;
using System.Security.Claims;

namespace MyFirstCoreApp.Authorization
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionEvaluator _evaluator;
        private readonly ICurrentTenant _currentTenant;
        private readonly ITenantSecurityService _tenantSecurity;

        public PermissionService(
            ApplicationDbContext context,
            IPermissionEvaluator evaluator,
            ICurrentTenant currentTenant,
            ITenantSecurityService tenantSecurity)
        {
            _context = context;
            _evaluator = evaluator;
            _currentTenant = currentTenant;
            _tenantSecurity = tenantSecurity;
        }

        // =========================
        // Authorization
        // =========================

        /// <summary>
        /// همان تصمیمِ Policyها (IPermissionEvaluator): مدیر سیستم / سطح حساب / سراسری (نقش‌های سراسری) /
        /// سطح شرکت (مالک یا سمت‌های کاربر «در شرکت فعال»). قبلاً فقط نقش‌های سراسری خوانده می‌شد و برای
        /// اعضای دارای سمت، دکمه‌ها و بخش‌های Viewها اشتباه پنهان می‌شد.
        /// </summary>
        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permission)
        {
            var subject = new PermissionSubject(
                IsAuthenticated: user.Identity?.IsAuthenticated == true,
                IsSuperAdmin: user.IsInRole("SuperAdmin"),
                UserId: user.FindFirstValue(ClaimTypes.NameIdentifier),
                TenantId: _currentTenant.TenantId);

            return _evaluator.HasAsync(subject, permission);
        }

        // =========================
        // Permission Management (کاتالوگ سراسری؛ فقط مدیر سیستم)
        // =========================

        public async Task<List<Permission>> GetAllAsync()
        {
            return await _context.Permissions
                .OrderBy(x => x.GroupName)
                .ThenBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<Permission?> GetByIdAsync(int id)
        {
            return await _context.Permissions
                .FindAsync(id);
        }

        public async Task<bool> ExistsAsync(
            string name,
            int? excludeId = null)
        {
            name = name.Trim();

            return await _context.Permissions
                .AnyAsync(x =>
                    x.Name == name &&
                    (!excludeId.HasValue ||
                     x.Id != excludeId.Value));
        }

        public async Task CreateAsync(
            Permission permission)
        {
            permission.Name = permission.Name.Trim();
            permission.DisplayName =
                permission.DisplayName?.Trim();
            permission.GroupName =
                permission.GroupName?.Trim();

            _context.Permissions.Add(permission);

            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(
            Permission permission)
        {
            var existing = await _context.Permissions
                .FindAsync(permission.Id);

            if (existing == null)
                throw new KeyNotFoundException(
                    "Permission پیدا نشد.");

            var newName = permission.Name.Trim();

            // نکته: کنترلر معمولاً نام را «روی همین نمونه‌ی ردیابی‌شده» عوض کرده و بعد UpdateAsync را صدا می‌زند؛
            // پس existing.Name همان مقدار جدید است. نامِ قبلی را باید از «مقدار اصلی در دیتابیس» خواند.
            var originalName = _context.Entry(existing).Property(p => p.Name).OriginalValue;

            // سمت‌های شرکت‌ها دسترسی را با «نام» نگه می‌دارند. تغییر نامِ دسترسیِ مورداستفاده،
            // دسترسی همه‌ی افرادِ آن سمت‌ها را بی‌صدا قطع می‌کرد؛ پس رد می‌شود.
            // (نام نمایشی، گروه و فعال/غیرفعال بودن آزاد است؛ غیرفعال کردن هم همه‌جا فوراً اثر دارد.)
            if (!string.Equals(originalName, newName, StringComparison.Ordinal)
                && await _tenantSecurity.IsPermissionInUseAsync(originalName))
            {
                throw new InvalidOperationException(
                    $"دسترسی «{originalName}» در سمت‌های شرکت‌ها استفاده شده و تغییر نام ندارد. " +
                    "برای جایگزینی، دسترسی جدید بسازید و این را غیرفعال کنید.");
            }

            existing.Name = newName;

            existing.DisplayName =
                permission.DisplayName?.Trim();

            existing.GroupName =
                permission.GroupName?.Trim();

            existing.IsActive = permission.IsActive;

            await _context.SaveChangesAsync();
        }
    }
}