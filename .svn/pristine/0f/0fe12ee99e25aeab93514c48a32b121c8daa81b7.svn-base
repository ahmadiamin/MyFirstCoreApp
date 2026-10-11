using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Banking.ViewModels.Cheque;

public class ChequeFormViewModel
{
    public int Id { get; set; }

    // ============================================
    // اطلاعات اصلی
    // ============================================

    [Required(ErrorMessage = "شماره چک الزامی است")]
    [Display(Name = "شماره چک")]
    [MaxLength(50)]
    public string ChequeNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "جهت چک")]
    public ChequeDirection Direction { get; set; } = ChequeDirection.Received;

    [Required(ErrorMessage = "مبلغ الزامی است")]
    [Display(Name = "مبلغ")]
    [Range(1, long.MaxValue, ErrorMessage = "مبلغ باید بیشتر از صفر باشد")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "تاریخ صدور الزامی است")]
    [Display(Name = "تاریخ صدور")]
    public string IssueDate { get; set; } = string.Empty;

    [Required(ErrorMessage = "تاریخ سررسید الزامی است")]
    [Display(Name = "تاریخ سررسید")]
    public string DueDate { get; set; } = string.Empty;

    // ============================================
    // طرف حساب
    // ============================================

    [Display(Name = "طرف حساب")]
    public int? ContactId { get; set; }

    // ============================================
    // اطلاعات بانکی
    // ============================================

    [Display(Name = "حساب بانکی")]
    public int? BankAccountId { get; set; }

    [Display(Name = "نام بانک")]
    [MaxLength(200)]
    public string? BankName { get; set; }

    [Display(Name = "شماره حساب")]
    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    [Display(Name = "کد شعبه")]
    [MaxLength(20)]
    public string? BranchCode { get; set; }

    [Display(Name = "نام شعبه")]
    [MaxLength(200)]
    public string? BranchName { get; set; }

    // ============================================
    // صاحب چک
    // ============================================

    [Display(Name = "نام صاحب چک")]
    [MaxLength(200)]
    public string? DrawerName { get; set; }

    [Display(Name = "کد ملی صاحب چک")]
    [MaxLength(20)]
    public string? DrawerNationalId { get; set; }

    // ============================================
    // سایر
    // ============================================

    [Display(Name = "توضیحات")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "فاکتور مرتبط")]
    public int? InvoiceId { get; set; }

    public string? ImagePath { get; set; }

    // ============================================
    // Dropdowns
    // ============================================

    public List<SelectListItem> ContactOptions { get; set; } = new();
    public List<SelectListItem> BankAccountOptions { get; set; } = new();
    public List<SelectListItem> InvoiceOptions { get; set; } = new();
}