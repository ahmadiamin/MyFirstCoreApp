using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Domain.Enums;

public enum AccountType
{
    [Display(Name = "بانکی")]
    Bank = 1,

    [Display(Name = "نقدی")]
    Cash = 2,

    [Display(Name = "دریافتنی (طلب از مشتری)")]
    Receivable = 3,

    [Display(Name = "پرداختنی (بدهی به فروشنده)")]
    Payable = 4,

    [Display(Name = "سرمایه")]
    Capital = 5,

    [Display(Name = "درآمد")]
    Income = 6,

    [Display(Name = "هزینه")]
    Expense = 7,

    [Display(Name = "واسط (چک دریافتنی)")]
    Contra = 8
}