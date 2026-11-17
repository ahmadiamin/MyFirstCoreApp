using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Tenancy;

/// <summary>
/// ارتباط چند-به-چند بین User و Tenant
/// (یک کاربر می‌تونه توی چند شرکت عضو باشه)
///
/// مشخصات پرسنلی (نام، فامیل، سمت، تلفن داخلی) «مخصوص همین شرکت» هستند و روی همین عضویت نگه داشته می‌شوند،
/// نه روی حساب سراسری کاربر: یک نفر ممکن است در دو شرکت مشخصات متفاوت داشته باشد و مالک یک شرکت
/// نباید بتواند مشخصات حساب سراسریِ شخصی را که عضو شرکت‌های دیگر هم هست تغییر دهد.
/// </summary>
public class UserTenant : BaseEntity
{
    public string UserId { get; set; } = string.Empty;    // FK به AspNetUsers
    public int TenantId { get; set; }                     // FK به Tenants

    public bool IsOwner { get; set; } = false;            // آیا مالک این Tenant هست؟
    public bool IsDefault { get; set; } = false;          // Tenant پیش‌فرض کاربر
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // ---------- مشخصات پرسنلی در این شرکت ----------

    /// <summary>نام (در این شرکت). خالی = از FullName حساب کاربری استفاده می‌شود.</summary>
    public string? FirstName { get; set; }

    /// <summary>فامیل (در این شرکت).</summary>
    public string? LastName { get; set; }

    /// <summary>عنوان شغلی (متن آزاد، مثلاً «منشی»)؛ دسترسی‌ها از «نقش‌ها» می‌آید نه از این عنوان.</summary>
    public string? JobTitle { get; set; }

    /// <summary>تلفن داخلی (فقط رقم لاتین؛ در یک شرکت یکتا).</summary>
    public string? InternalPhone { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}