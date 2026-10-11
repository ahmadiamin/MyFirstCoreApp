using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Banking.ViewModels.Payment;

public class PaymentFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "شماره")]
    public int Number { get; set; }

    [Required(ErrorMessage = "نوع الزامی است")]
    [Display(Name = "نوع")]
    public PaymentType PaymentType { get; set; } = PaymentType.Received;

    [Required(ErrorMessage = "تاریخ الزامی است")]
    [Display(Name = "تاریخ")]
    public string Date { get; set; } = string.Empty;

    [Required(ErrorMessage = "طرف حساب الزامی است")]
    [Display(Name = "طرف حساب")]
    public int? ContactId { get; set; }

    [Display(Name = "حساب بانکی")]
    public int? BankAccountId { get; set; }

    [Required(ErrorMessage = "روش پرداخت الزامی است")]
    [Display(Name = "روش پرداخت")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    [Required(ErrorMessage = "مبلغ الزامی است")]
    [Display(Name = "مبلغ کل")]
    [Range(1, long.MaxValue, ErrorMessage = "مبلغ باید بیشتر از صفر باشد")]
    public decimal Amount { get; set; }

    [Display(Name = "شماره مرجع")]
    [MaxLength(100)]
    public string? Reference { get; set; }

    [Display(Name = "توضیحات")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    // تخصیص‌ها
    public List<PaymentAllocationViewModel> Allocations { get; set; } = new();

    // Dropdowns
    public List<SelectListItem> ContactOptions { get; set; } = new();
    public List<SelectListItem> BankAccountOptions { get; set; } = new();
}