using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Contacts.ViewModels.Contact;

public class ContactFormViewModel
{
    public int Id { get; set; }

    // ---- اطلاعات پایه ----

    [Required(ErrorMessage = "نام الزامی است")]
    [Display(Name = "نام کامل")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "کد")]
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required(ErrorMessage = "نوع طرف حساب الزامی است")]
    [Display(Name = "نوع طرف حساب")]
    public ContactType ContactType { get; set; } = ContactType.Customer;

    // ---- اطلاعات تماس ----

    [Display(Name = "تلفن ثابت")]
    [MaxLength(30)]
    public string? Phone { get; set; }

    [Display(Name = "موبایل")]
    [MaxLength(30)]
    public string? Mobile { get; set; }

    [Display(Name = "ایمیل")]
    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
    [MaxLength(200)]
    public string? Email { get; set; }

    [Display(Name = "آدرس")]
    [MaxLength(1000)]
    public string? Address { get; set; }

    [Display(Name = "کد پستی")]
    [MaxLength(20)]
    public string? PostalCode { get; set; }

    // ---- اطلاعات حقوقی ----

    [Display(Name = "کد ملی / شناسه ملی")]
    [MaxLength(20)]
    public string? NationalId { get; set; }

    [Display(Name = "کد اقتصادی")]
    [MaxLength(30)]
    public string? EconomicCode { get; set; }

    [Display(Name = "شماره ثبت")]
    [MaxLength(30)]
    public string? RegistrationNumber { get; set; }

    // ---- مالی ----

    [Display(Name = "مانده اولیه")]
    public decimal CurrentBalance { get; set; }

    [Display(Name = "سقف اعتبار")]
    public decimal? CreditLimit { get; set; }

    // ---- وضعیت ----

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "یادداشت")]
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // ---- Dropdown ----

    public List<SelectListItem> ContactTypeOptions { get; set; } = new();
}