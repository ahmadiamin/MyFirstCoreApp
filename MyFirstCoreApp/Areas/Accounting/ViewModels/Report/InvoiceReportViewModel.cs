using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Report;

public class InvoiceReportViewModel
{
    // فیلترها
    public int? InvoiceType { get; set; }
    public int? Status { get; set; }
    public int? ContactId { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public bool OnlyOpen { get; set; }

    // صفحه‌بندی
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalPages { get; set; }

    // جمع «همه‌ی ردیف‌های فیلترشده»
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalSettlementDiscount { get; set; }
    public decimal TotalRemaining { get; set; }

    // ردیف‌های صفحه‌ی جاری
    public List<InvoiceReportRowViewModel> Rows { get; set; } = new();

    // Dropdowns
    public List<SelectListItem> ContactOptions { get; set; } = new();
    public List<SelectListItem> InvoiceTypeOptions { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();
}

public class InvoiceReportRowViewModel
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string DateShamsi { get; set; } = string.Empty;
    public string InvoiceTypeName { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal SettlementDiscount { get; set; }
    public decimal RemainingAmount { get; set; }
}
