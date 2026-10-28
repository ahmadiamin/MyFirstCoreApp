using MyFirstCoreApp.Application.Reports;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Report;

public class ContactBalancesViewModel
{
    public ContactBalanceMode Mode { get; set; } = ContactBalanceMode.Debtors;
    public bool IsDebtors => Mode == ContactBalanceMode.Debtors;

    // فیلترها
    public string AsOf { get; set; } = string.Empty;
    public string? MinAmountText { get; set; }
    public string? Search { get; set; }
    public int Sort { get; set; } = 1;

    // صفحه‌بندی
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    /// <summary>تاریخ گزارش گذشته است؛ ستون‌های فاکتور باز و چک «وضعیت فعلی» را نشان می‌دهند.</summary>
    public bool AsOfIsPast { get; set; }

    /// <summary>null یعنی هنوز گزارشی ساخته نشده (فیلتر نامعتبر یا خطا).</summary>
    public ContactBalancesReport? Report { get; set; }
}