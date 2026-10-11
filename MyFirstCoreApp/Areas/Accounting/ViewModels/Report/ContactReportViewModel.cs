using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Report;

public class ContactReportViewModel
{
    // فیلترها
    public int? ContactId { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public bool OnlyPosted { get; set; } = true;

    // اطلاعات طرف حساب
    public string ContactName { get; set; } = string.Empty;
    public string? ContactCode { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public ContactType ContactType { get; set; }
    public string? ContactTypeName { get; set; }

    // خطوط
    public List<ContactReportLine> Lines { get; set; } = new();

    // جمع‌ها
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }   // بدهکار (طلب ما از طرف حساب)
    public decimal TotalCredit { get; set; }  // بستانکار (بدهی ما به طرف حساب)
    public decimal ClosingBalance { get; set; }

    // Dropdowns
    public List<SelectListItem> ContactOptions { get; set; } = new();
}

public class ContactReportLine
{
    public int TransactionId { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime Date { get; set; }
    public string DateShamsi { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? ReferenceType { get; set; }   // Invoice, Cheque, Payment
    public int? ReferenceId { get; set; }
    public string? AccountName { get; set; }     // حساب مقابل
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}