using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Tenancy;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;          // نام شرکت/بیزینس
    public string? LegalName { get; set; }                     // نام حقوقی
    public string? NationalId { get; set; }                    // شناسه ملی
    public string? EconomicCode { get; set; }                  // کد اقتصادی
    public string? Phone { get; set; }
    public string? Address { get; set; }
    // فایل‌های هویتی شرکت در زیرساخت عمومی پیوست‌ها ذخیره می‌شوند.
    public int? LogoAttachmentId { get; set; }
    public int? StampAttachmentId { get; set; }
    public int? SignatureAttachmentId { get; set; }

    // مسیر قدیمی برای سازگاری با داده‌های قبلی حفظ شده است.
    public string? LogoPath { get; set; }
    public bool IsActive { get; set; } = true;

    // تنظیمات نمایش
    public string DefaultCurrency { get; set; } = "IRR";       // ریال
    public string TimeZone { get; set; } = "Iran Standard Time";

    // Navigation
    public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
}