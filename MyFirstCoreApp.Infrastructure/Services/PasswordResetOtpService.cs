
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyFirstCoreApp.Application.Identity;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class PasswordResetOtpService : IPasswordResetOtpService
{
    private const int CodeLength = 6;
    private const int CodeValidityMinutes = 5;
    private const int MaxFailedAttempts = 5;
    private const int ResendCooldownSeconds = 60;
    private const int MaxRequestsPerWindow = 3;
    private const int RequestWindowMinutes = 15;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ISystemSmsService _smsService;

    public PasswordResetOtpService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ISystemSmsService smsService)
    {
        _context = context;
        _userManager = userManager;
        _passwordHasher = passwordHasher;
        _smsService = smsService;
    }

    public async Task<PasswordResetOtpRequestResult> RequestCodeAsync(
        string mobile,
        CancellationToken cancellationToken = default)
    {
        var normalizedMobile = NormalizeMobile(mobile);

        if (normalizedMobile is null)
            return RequestFailed();

        var user = await _userManager.Users
            .FirstOrDefaultAsync(
                x => x.PhoneNumber == normalizedMobile &&
                     x.PhoneNumberConfirmed &&
                     x.IsActive,
                cancellationToken);

        // برای جلوگیری از تشخیص شماره‌های ثبت‌شده،
        // در صورت پیدا نشدن کاربر نیز پاسخ عمومی می‌دهیم.
        if (user is null)
            return RequestAccepted();

        var smsSettings = await _smsService.GetSummaryAsync(cancellationToken);

        if (!smsSettings.ApiKeyConfigured ||
            !smsSettings.PasswordRecoverySmsEnabled ||
            string.IsNullOrWhiteSpace(smsSettings.PasswordRecoverySmsPatternCode))
        {
            return RequestFailed();
        }

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-RequestWindowMinutes);

        PasswordResetOtp otp;
        var code = string.Empty;
        await using (var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken))
        {
            // قفل اختصاصی برای جلوگیری از درخواست هم‌زمان یک کاربر
            var lockAcquired = await AcquireUserLockAsync(
                user.Id,
                cancellationToken);

            if (!lockAcquired)
                return RequestFailed();

            var recentRequests = await _context.PasswordResetOtps
                .Where(x =>
                    x.UserId == user.Id &&
                    x.SentAt >= windowStart)
                .OrderByDescending(x => x.SentAt)
                .ToListAsync(cancellationToken);
                
            // محدودیت ارسال مجدد
            var lastRequest = recentRequests.FirstOrDefault();

            if (lastRequest is not null &&
                (now - lastRequest.SentAt).TotalSeconds < ResendCooldownSeconds)
            {
                return RequestFailed();
            }

            // حداکثر سه درخواست در بازه پانزده دقیقه
            if (recentRequests.Count >= MaxRequestsPerWindow)
                return RequestFailed();

            // غیرفعال کردن کدهای قبلی
            var previousRequests = await _context.PasswordResetOtps
                .Where(x =>
                    x.UserId == user.Id &&
                    x.ConsumedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var previous in previousRequests)
                previous.ConsumedAt = now;

            code = RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString(CultureInfo.InvariantCulture);

            otp = new PasswordResetOtp
            {
                UserId = user.Id,
                CodeHash = _passwordHasher.HashPassword(user, code),
                SentAt = now,
                ExpiresAt = now.AddMinutes(CodeValidityMinutes),
                FailedAttempts = 0,
                CreatedAt = now,
                VerifiedAt = null,
                ConsumedAt = null,
                IsDeleted = false
            };

            await _context.PasswordResetOtps.AddAsync(
                otp, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            // قبل از ارسال پیامک، تراکنش را نهایی می‌کنیم.
            await transaction.CommitAsync(cancellationToken);
        }

        // ارسال پیامک پس از ذخیره درخواست
        try
        {
            var sendResult = await _smsService.SendPasswordRecoveryCodeAsync(
                normalizedMobile,
                code,
                cancellationToken);

            if (!sendResult.IsAccepted)
            {
                await ConsumeOtpAsync(otp);
                return RequestFailed();
            }
        }
        catch (OperationCanceledException)
        {
            // حتی در صورت لغو درخواست، کد ذخیره‌شده معتبر باقی نماند.
            await ConsumeOtpAsync(otp);
            throw;
        }
        catch (Exception)
        {
            // در صورت بروز خطا هنگام ارسال پیامک، کد مصرف شود.
            await ConsumeOtpAsync(otp);
            return RequestFailed();
        }

        return RequestAccepted();
    }
    private async Task ConsumeOtpAsync(PasswordResetOtp otp)
    {
        otp.ConsumedAt = DateTime.UtcNow;

        // برای اطمینان از ذخیره وضعیت مصرف‌شده،
        // حتی اگر CancellationToken درخواست لغو شده باشد.
        await _context.SaveChangesAsync(CancellationToken.None);
    }
    public async Task<PasswordResetOtpVerificationResult> VerifyCodeAsync(
    string mobile,
    string code,
    CancellationToken cancellationToken = default)
    {
        var normalizedMobile = NormalizeMobile(mobile);
        var normalizedCode = NormalizeDigits(code)?.Trim();

        if (normalizedMobile is null ||
            string.IsNullOrWhiteSpace(normalizedCode) ||
            normalizedCode.Length != CodeLength ||
            normalizedCode.Any(c => c < '0' || c > '9'))
        {
            return VerificationFailed();
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(
                x => x.PhoneNumber == normalizedMobile &&
                     x.PhoneNumberConfirmed &&
                     x.IsActive,
                cancellationToken);

        if (user is null)
            return VerificationFailed();

        var now = DateTime.UtcNow;

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // قفل اختصاصی کاربر؛ همان قفلی که در RequestCodeAsync استفاده می‌شود.
        var lockAcquired = await AcquireUserLockAsync(
            user.Id,
            cancellationToken);

        if (!lockAcquired)
            return VerificationFailed();

        var otp = await _context.PasswordResetOtps
            .IgnoreQueryFilters()
            .Where(x =>
                x.UserId == user.Id &&
                !x.IsDeleted &&
                x.ConsumedAt == null &&
                x.VerifiedAt == null &&
                x.ExpiresAt > now)
            .OrderByDescending(x => x.SentAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
            return VerificationFailed();

        if (otp.ConsumedAt != null)
            return VerificationFailed();

        if (otp.VerifiedAt != null)
            return VerificationFailed();

        if (otp.ExpiresAt <= now)
            return VerificationFailed();

        if (otp.FailedAttempts >= MaxFailedAttempts)
        {
            otp.ConsumedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            
            await transaction.CommitAsync(cancellationToken);

            return VerificationFailed();
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            otp.CodeHash,
            normalizedCode);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            otp.FailedAttempts++;

            if (otp.FailedAttempts >= MaxFailedAttempts)
                otp.ConsumedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return VerificationFailed();
        }

        otp.VerifiedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PasswordResetOtpVerificationResult(
            true,
            otp.Id,
            "کد تأیید شد.");
    }

    private static PasswordResetOtpRequestResult RequestAccepted()
    {
        return new PasswordResetOtpRequestResult(
            true,
            "در صورت معتبر بودن شماره موبایل، کد بازیابی برای شما ارسال خواهد شد.");
    }

    private static PasswordResetOtpRequestResult RequestFailed()
    {
        return new PasswordResetOtpRequestResult(
            false,
            "در حال حاضر امکان ارسال کد بازیابی وجود ندارد. لطفاً کمی بعد تلاش کنید.");
    }

    private static PasswordResetOtpVerificationResult VerificationFailed()
    {
        return new PasswordResetOtpVerificationResult(
            false,
            null,
            "کد واردشده معتبر نیست یا اعتبار آن به پایان رسیده است.");
    }

    private static string? NormalizeMobile(string? mobile)
    {
        var normalized = NormalizeDigits(mobile)?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return System.Text.RegularExpressions.Regex.IsMatch(
            normalized,
            @"^09[0-9]{9}$")
            ? normalized
            : null;
    }

    private static string? NormalizeDigits(string? value)
    {
        if (value is null)
            return null;

        return value
            .Replace('۰', '0')
            .Replace('۱', '1')
            .Replace('۲', '2')
            .Replace('۳', '3')
            .Replace('۴', '4')
            .Replace('۵', '5')
            .Replace('۶', '6')
            .Replace('۷', '7')
            .Replace('۸', '8')
            .Replace('۹', '9')
            .Replace('٠', '0')
            .Replace('١', '1')
            .Replace('٢', '2')
            .Replace('٣', '3')
            .Replace('٤', '4')
            .Replace('٥', '5')
            .Replace('٦', '6')
            .Replace('٧', '7')
            .Replace('٨', '8')
            .Replace('٩', '9');
    }


    public async Task<PasswordResetOtpCompletionResult> CompletePasswordResetAsync(
        int requestId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        // ابتدا مالک OTP را خارج از تراکنش پیدا می‌کنیم.
        var otpOwner = await _context.PasswordResetOtps
            .AsNoTracking()
            .Where(x => x.Id == requestId)
            .Select(x => x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(otpOwner))
            return CompletionFailed();

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var now = DateTime.UtcNow;

        // قفل اختصاصی کاربر
        var lockAcquired = await AcquireUserLockAsync(
            otpOwner,
            cancellationToken);

        if (!lockAcquired)
            return CompletionFailed();

        // پس از گرفتن قفل، وضعیت OTP را دوباره بررسی می‌کنیم.
        var otp = await _context.PasswordResetOtps
            .FirstOrDefaultAsync(
                x => x.Id == requestId &&
                     x.UserId == otpOwner &&
                     x.ConsumedAt == null &&
                     x.VerifiedAt != null &&
                     x.VerifiedAt >= now.AddMinutes(-10),
                cancellationToken);

        if (otp is null)
            return CompletionFailed();

        var user = await _userManager.FindByIdAsync(otp.UserId);

        if (user is null ||
            !user.IsActive ||
            !user.PhoneNumberConfirmed)
        {
            return new PasswordResetOtpCompletionResult(
                false,
                ["امکان تغییر رمز عبور این حساب وجود ندارد."]);
        }

        var identityToken =
            await _userManager.GeneratePasswordResetTokenAsync(user);

        var resetResult = await _userManager.ResetPasswordAsync(
            user,
            identityToken,
            newPassword);

        if (!resetResult.Succeeded)
        {
            return new PasswordResetOtpCompletionResult(
                false,
                resetResult.Errors
                    .Select(x => x.Description)
                    .ToArray());
        }

        var pendingOtps = await _context.PasswordResetOtps
            .Where(x =>
                x.UserId == user.Id &&
                x.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var pendingOtp in pendingOtps)
            pendingOtp.ConsumedAt = now;

        user.PasswordChangedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PasswordResetOtpCompletionResult(true, []);
    }

    private static PasswordResetOtpCompletionResult CompletionFailed()
    {
        return new PasswordResetOtpCompletionResult(
            false,
            ["درخواست بازیابی معتبر نیست یا اعتبار آن به پایان رسیده است."]);
    }

    private async Task<bool> AcquireUserLockAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // قفل اختصاصی فقط برای SQL Server است.
        // تست‌های SQLite بدون اجرای sp_getapplock ادامه پیدا می‌کنند.
        if (!_context.Database.IsSqlServer())
            return true;

        var connection = _context.Database.GetDbConnection();

        await using var command = connection.CreateCommand();

        command.Transaction = _context.Database
            .CurrentTransaction!
            .GetDbTransaction();

        command.CommandText = """
        DECLARE @result int;

        EXEC @result = sys.sp_getapplock
            @Resource = @resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 10000;

        SELECT @result;
        """;

        var resource = "PasswordResetOtp:" +
            Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(userId)));

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);

        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));

        return result >= 0;
    }
}