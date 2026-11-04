using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Identity;

/// <summary>
/// کد یک‌بارمصرف بازیابی رمز عبور کاربران
/// </summary>
public sealed class PasswordResetOtp : BaseEntity
{
    /// <summary>
    /// شناسه کاربر در جدول AspNetUsers
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// هش کد یک‌بارمصرف؛ کد خام ذخیره نمی‌شود
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>
    /// زمان انقضای کد
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// تعداد تلاش‌های ناموفق
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// زمان تأیید موفق کد
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// زمان مصرف کد پس از تغییر موفق رمز عبور
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>
    /// زمان آخرین ارسال پیامک
    /// </summary>
    public DateTime SentAt { get; set; }
}