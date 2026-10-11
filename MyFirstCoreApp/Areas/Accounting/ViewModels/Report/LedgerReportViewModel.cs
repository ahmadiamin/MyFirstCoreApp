using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Report;

public class LedgerReportViewModel
{
    // فیلترها
    public int? AccountId { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }

    // اطلاعات حساب
    public string AccountName { get; set; } = string.Empty;
    public string? AccountCode { get; set; }

    // مانده اول دوره
    public decimal OpeningBalance { get; set; }

    // خطوط گزارش
    public List<LedgerReportLine> Lines { get; set; } = new();

    // جمع‌ها
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }

    // برای dropdown
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AccountOptions { get; set; } = new();
}

public class LedgerReportLine
{
    public int TransactionId { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime Date { get; set; }
    public string DateShamsi { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }   // مانده تجمعی
}