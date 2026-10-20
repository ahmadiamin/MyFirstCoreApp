using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Account;

public class AccountFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام حساب الزامی است")]
    [Display(Name = "نام حساب")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "کد حساب")]
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required(ErrorMessage = "دسته‌بندی حساب الزامی است")]
    [Display(Name = "دسته‌بندی حساب")]
    public int AccountCategoryId { get; set; }

    [Required(ErrorMessage = "نوع حساب الزامی است")]
    [Display(Name = "نوع حساب")]
    public AccountType AccountType { get; set; } = AccountType.Bank;

    // ---- اطلاعات بانکی ----

    [Display(Name = "نام بانک")]
    [MaxLength(200)]
    public string? BankName { get; set; }

    [Display(Name = "شماره حساب")]
    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [Display(Name = "شماره کارت")]
    [MaxLength(30)]
    public string? CardNumber { get; set; }

    [Display(Name = "شماره شبا")]
    [MaxLength(32)]
    public string? ShebaNumber { get; set; }

    // ---- مالی ----

    [Display(Name = "مانده اولیه")]
    public decimal CurrentBalance { get; set; }

    // ---- وضعیت ----

    [Display(Name = "حساب پیش‌فرض این نوع")]
    public bool IsDefault { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "توضیحات")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    // ---- Dropdown ----

    public List<SelectListItem> CategoryOptions { get; set; } = new();
    public List<SelectListItem> AccountTypeOptions { get; set; } = new();
}