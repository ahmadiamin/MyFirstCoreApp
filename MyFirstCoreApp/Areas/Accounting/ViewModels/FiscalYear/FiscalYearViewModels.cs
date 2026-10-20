using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.FiscalYear;

public class FiscalYearFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "نام")]
    [StringLength(100, ErrorMessage = "نام حداکثر ۱۰۰ کاراکتر است.")]
    public string? Name { get; set; }

    [Display(Name = "تاریخ شروع")]
    [Required(ErrorMessage = "تاریخ شروع الزامی است.")]
    public string StartDate { get; set; } = string.Empty;

    [Display(Name = "تاریخ پایان")]
    [Required(ErrorMessage = "تاریخ پایان الزامی است.")]
    public string EndDate { get; set; } = string.Empty;
}

public class FiscalYearListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public bool IsCurrent { get; set; }
    public int TransactionCount { get; set; }
    public string? ClosedAt { get; set; }
}
