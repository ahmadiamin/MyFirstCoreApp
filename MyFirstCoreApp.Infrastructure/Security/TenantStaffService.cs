using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;

namespace MyFirstCoreApp.Infrastructure.Security;

/// <summary>
/// افزودن فرد به شرکت: ساخت حساب (یا استفاده از حسابِ موجود)، عضویت، مشخصات پرسنلی و نقش‌ها
/// همه در «یک تراکنش»؛ اگر مثلاً تلفن داخلی تکراری باشد، حساب هم ساخته نمی‌شود.
/// اعتبارسنجی مشخصات و نقش‌ها همان قواعد ITenantSecurityService است (یک منبع قاعده).
/// </summary>
public class TenantStaffService : ITenantStaffService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantSecurityService _security;

    public TenantStaffService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ITenantSecurityService security)
    {
        _context = context;
        _userManager = userManager;
        _security = security;
    }

    public Task<MemberCreateResult> AddMemberAsync(int tenantId, MemberCreateRequest request)
    {
        var email = NormalizeEmail(request.Email);

        return _context.ExecuteInTransactionAsync(async () =>
        {
            var tenantActive = await _context.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive && !t.IsDeleted);
            if (!tenantActive)
                throw new InvalidOperationException("شرکت پیدا نشد.");

            var user = await _userManager.FindByEmailAsync(email);
            var createdNewAccount = false;

            if (user != null)
            {
                var alreadyMember = await _context.UserTenants.AnyAsync(ut =>
                    ut.TenantId == tenantId && ut.UserId == user.Id && !ut.IsDeleted);

                if (alreadyMember)
                    throw new InvalidOperationException("این شخص قبلاً عضو شرکت است.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.Password))
                    throw new InvalidOperationException("برای ساخت حساب جدید، رمز عبور الزامی است.");

                var firstName = request.FirstName?.Trim() ?? string.Empty;
                var lastName = request.LastName?.Trim() ?? string.Empty;

                user = new ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    FullName = $"{firstName} {lastName}".Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                    IsActive = true
                };

                var created = await _userManager.CreateAsync(user, request.Password);
                if (!created.Succeeded)
                    throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));

                createdNewAccount = true;
            }

            // اولین شرکتِ این شخص ← پیش‌فرضش
            var hasOtherMembership = await _context.UserTenants.AnyAsync(ut =>
                ut.UserId == user.Id && ut.TenantId != tenantId && !ut.IsDeleted);

            _context.UserTenants.Add(new UserTenant
            {
                UserId = user.Id,
                TenantId = tenantId,
                IsOwner = false,
                IsDefault = !hasOtherMembership,
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // اعتبارسنجی و ذخیره‌ی مشخصات و نقش‌ها با همان قواعدِ مدیریت افراد
            await _security.UpdateMemberProfileAsync(tenantId, user.Id, new MemberProfileRequest
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                JobTitle = request.JobTitle,
                InternalPhone = request.InternalPhone
            });

            if (request.RoleIds.Count > 0)
                await _security.SetMemberRolesAsync(tenantId, user.Id, request.RoleIds);

            return new MemberCreateResult { UserId = user.Id, CreatedNewAccount = createdNewAccount };
        });
    }

    private static string NormalizeEmail(string? email)
    {
        var value = email?.Trim() ?? string.Empty;

        if (value.Length == 0)
            throw new InvalidOperationException("ایمیل الزامی است.");

        try
        {
            var address = new MailAddress(value);
            if (!string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase))
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("ایمیل نامعتبر است.");
        }

        return value;
    }
}