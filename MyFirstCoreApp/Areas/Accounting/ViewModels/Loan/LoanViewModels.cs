using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Loan;

public class LoanFormViewModel
{
    [Display(Name = "نوع")]
    public LoanDirection Direction { get; set; } = LoanDirection.Given;

    [Display(Name = "طرف حساب")]
    [Required(ErrorMessage = "طرف حساب الزامی است.")]
    public int? ContactId { get; set; }

    [Display(Name = "از/به حساب (بانک یا صندوق)")]
    [Required(ErrorMessage = "حساب بانکی یا صندوق الزامی است.")]
    public int? AccountId { get; set; }

    /// <summary>مبلغ به‌صورت متن (ارقام فارسی/عربی و جداکننده‌ی هزارگان پذیرفته می‌شود).</summary>
    [Display(Name = "مبلغ (ریال)")]
    [Required(ErrorMessage = "مبلغ الزامی است.")]
    public string AmountText { get; set; } = string.Empty;

    [Display(Name = "تاریخ")]
    [Required(ErrorMessage = "تاریخ الزامی است.")]
    public string Date { get; set; } = string.Empty;

    [Display(Name = "توضیحات")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "رسید (اختیاری)")]
    public IFormFile? Receipt { get; set; }

    public List<SelectListItem> ContactOptions { get; set; } = new();
    public List<SelectListItem> AccountOptions { get; set; } = new();
}

public class LoanRepayFormViewModel
{
    public int LoanId { get; set; }

    [Required(ErrorMessage = "حساب بانکی یا صندوق الزامی است.")]
    public int? AccountId { get; set; }

    [Required(ErrorMessage = "مبلغ الزامی است.")]
    public string AmountText { get; set; } = string.Empty;

    [Required(ErrorMessage = "تاریخ الزامی است.")]
    public string Date { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public IFormFile? Receipt { get; set; }
}

public class LoanListItemViewModel
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Date { get; set; } = string.Empty;
    public bool IsGiven { get; set; }
    public string? ContactName { get; set; }
    public decimal Amount { get; set; }
    public decimal Repaid { get; set; }
    public decimal Remaining { get; set; }
    public string? Description { get; set; }
}

public class LoanIndexViewModel
{
    public int? Direction { get; set; }
    public bool OnlyOpen { get; set; } = true;
    public List<LoanListItemViewModel> Items { get; set; } = new();

    public decimal TotalGivenRemaining { get; set; }
    public decimal TotalTakenRemaining { get; set; }
}

public class LoanRepaymentViewModel
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class LoanDetailsViewModel
{
    public LoanListItemViewModel Loan { get; set; } = new();
    public List<LoanRepaymentViewModel> Repayments { get; set; } = new();
    public LoanRepayFormViewModel RepayForm { get; set; } = new();
    public List<SelectListItem> AccountOptions { get; set; } = new();
}
