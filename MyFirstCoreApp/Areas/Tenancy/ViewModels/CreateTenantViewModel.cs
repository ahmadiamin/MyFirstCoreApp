using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Tenancy.ViewModels;

public class CreateTenantViewModel
{
    [Required(ErrorMessage = "نام شرکت الزامی است")]
    [Display(Name = "نام شرکت / کسب و کار")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "نام حقوقی")]
    [MaxLength(300)]
    public string? LegalName { get; set; }

    [Display(Name = "شناسه ملی")]
    [MaxLength(20)]
    public string? NationalId { get; set; }

    [Display(Name = "کد اقتصادی")]
    [MaxLength(30)]
    public string? EconomicCode { get; set; }

    [Display(Name = "تلفن")]
    [MaxLength(20)]
    public string? Phone { get; set; }

    [Display(Name = "آدرس")]
    [MaxLength(1000)]
    public string? Address { get; set; }

    [Display(Name = "کلید API پیامک")]
    [MaxLength(1000)]
    public string? SmsApiKeyInput { get; set; }

    [Display(Name = "شماره خط پیامک")]
    [MaxLength(50)]
    public string SmsLineNumber { get; set; } = "90008361";

    [Display(Name = "الگوی پیامک لینک پرتال")]
    [MaxLength(1000)]
    public string? SmsPortalLinkText { get; set; }

    public bool SmsApiKeyConfigured { get; set; }
    public bool ClearSmsApiKey { get; set; }

    [Display(Name = "فعال‌سازی یادآوری سررسید چک‌ها")]
    public bool SmsChequeReminderEnabled { get; set; }

    [Display(Name = "ارسال یک روز قبل")]
    public bool SmsChequeReminderDayBeforeEnabled { get; set; }

    [Display(Name = "ارسال در روز سررسید")]
    public bool SmsChequeReminderDueDayEnabled { get; set; }

    [Display(Name = "ساعت ارسال")]
    public string SmsChequeReminderTime { get; set; } = "09:00";

    [Display(Name = "موبایل دریافت‌کنندهٔ یادآوری‌ها")]
    [MaxLength(30)]
    public string? SmsChequeReminderMobile { get; set; }

    [Display(Name = "الگوی پیامک یادآوری چک")]
    [MaxLength(1000)]
    public string? SmsChequeReminderText { get; set; }
}
