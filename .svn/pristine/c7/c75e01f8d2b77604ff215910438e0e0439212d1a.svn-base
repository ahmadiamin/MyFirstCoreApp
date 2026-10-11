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
}