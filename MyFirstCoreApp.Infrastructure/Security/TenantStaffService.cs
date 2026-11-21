using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;

namespace MyFirstCoreApp.Infrastructure.Security;

/// <summary>
/// افزودن کارمند با «موبایل». مالک رمز نمی‌دهد و نمی‌بیند: حساب جدید با رمز تصادفیِ ناشناخته ساخته می‌شود
/// و کد تعیین رمز (همان OTP پیامکیِ بازیابی رمز) به موبایل کارمند می‌رود تا خودش رمز بگذارد.
/// همه‌چیز به‌جز پیامک در «یک تراکنش» است؛ پیامک بعد از Commit فرستاده می‌شود (سرویس OTP تراکنش خودش را دارد).
/// </summary>
public class TenantStaffService : ITenantStaffService
{
    private static readonly Regex MobilePattern = new(@"^09[0-9]{9}$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantSecurityService _security;
    private readonly IPasswordResetOtpService _otp;

    public TenantStaffService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ITenantSecurityService security,
        IPasswordResetOtpService otp)
    {
        _context = context;
        _userManager = userManager;
        _security = security;
        _otp = otp;
    }

    public async Task<MemberCreateResult> AddMemberAsync(int tenantId, MemberCreateRequest request)
    {
        var mobile = NormalizeMobile(request.Mobile);

        var result = await _context.ExecuteInTransactionAsync(async () =>
        {
            var tenantActive = await _context.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive && !t.IsDeleted);
            if (!tenantActive)
                throw new InvalidOperationException("شرکت پیدا نشد.");

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == mobile);
            var createdNewAccount = false;

            if (user != null)
            {
                // فقط حسابی که صاحبش «موبایل را تأیید کرده»؛ وگرنه کسی با ثبت‌نامِ تأییدنشده روی شماره‌ی دیگران
                // می‌توانست به شرکت راه پیدا کند.
                if (!user.PhoneNumberConfirmed)
                    throw new InvalidOperationException(
                        "این شماره قبلاً در سیستم ثبت شده ولی تأیید نشده است؛ از صاحب شماره بخواهید ابتدا ثبت‌نام خودش را تأیید کند.");

                if (!user.IsActive)
                    throw new InvalidOperationException("حساب این شخص غیرفعال است.");

                var alreadyMember = await _context.UserTenants.AnyAsync(ut =>
                    ut.TenantId == tenantId && ut.UserId == user.Id && !ut.IsDeleted);

                if (alreadyMember)
                    throw new InvalidOperationException("این شخص قبلاً عضو شرکت است.");
            }
            else
            {
                var first = request.FirstName?.Trim() ?? string.Empty;
                var last = request.LastName?.Trim() ?? string.Empty;

                user = new ApplicationUser
                {
                    UserName = mobile,
                    PhoneNumber = mobile,

                    // موبایلِ مورد تأییدِ مالک (واقعاً با اولین کدِ پیامکی در دست صاحب شماره‌اش ثابت می‌شود).
                    // EmailConfirmed هم علامت می‌خورد چون سیاست ورود «حساب تأییدشده» می‌خواهد و این حساب ایمیل ندارد.
                    PhoneNumberConfirmed = true,
                    EmailConfirmed = true,

                    FullName = $"{first} {last}".Trim(),
                    IsActive = true
                };

                // رمز تصادفیِ ناشناخته؛ هیچ‌کس (حتی مالک) آن را نمی‌داند و جایی ثبت نمی‌شود
                var created = await _userManager.CreateAsync(user, PasswordGenerator.Generate());
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

            return new MemberCreateResult
            {
                UserId = user.Id,
                CreatedNewAccount = createdNewAccount,
                MaskedMobile = PhoneMask.Mask(mobile)
            };
        });

        // بعد از Commit: کد تعیین رمز فقط برای حساب «جدید». شکست پیامک، ساخت حساب را برنمی‌گرداند
        // (مالک می‌تواند «ارسال کد» را دوباره بزند).
        if (result.CreatedNewAccount)
        {
            try
            {
                var sms = await _otp.RequestCodeAsync(mobile);
                result.PasswordSmsAccepted = sms.IsAccepted;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                result.PasswordSmsAccepted = false;
            }
        }

        return result;
    }

    public async Task<StaffPasswordCodeResult> SendPasswordCodeAsync(int tenantId, string userId)
    {
        // فقط عضوِ همین شرکت (کاربرِ شرکت دیگر «عضو نیست» است)
        var member = await _security.GetMemberAsync(tenantId, userId)
            ?? throw new InvalidOperationException("این شخص عضو شرکت نیست.");

        var user = await _userManager.FindByIdAsync(member.UserId)
            ?? throw new InvalidOperationException("این شخص عضو شرکت نیست.");

        if (!user.IsActive)
            throw new InvalidOperationException("حساب این شخص غیرفعال است.");

        if (string.IsNullOrWhiteSpace(user.PhoneNumber) || !user.PhoneNumberConfirmed)
            throw new InvalidOperationException("موبایل این شخص ثبت یا تأیید نشده است.");

        var sms = await _otp.RequestCodeAsync(user.PhoneNumber);

        return new StaffPasswordCodeResult
        {
            Accepted = sms.IsAccepted,
            MaskedMobile = PhoneMask.Mask(user.PhoneNumber)
        };
    }

    private static string NormalizeMobile(string? value)
    {
        var mobile = PersianDate.NormalizeDigits(value ?? string.Empty).Replace(" ", "").Trim();

        if (!MobilePattern.IsMatch(mobile))
            throw new InvalidOperationException("شماره موبایل نامعتبر است (مثال: 09121234567).");

        return mobile;
    }
}