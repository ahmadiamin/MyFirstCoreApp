using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Report
{


    public class DetailedReportViewModel
    {
        public int? AccountId { get; set; }
        public int? ContactId { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public bool OnlyPosted { get; set; } = true;

        // 🆕 اطلاعات حساب
        public string AccountName { get; set; } = string.Empty;
        public string? AccountCode { get; set; }
        public string? AccountTypeName { get; set; }

        // 🆕 خطوط
        public List<DetailedReportLine> Lines { get; set; } = new();

        // 🆕 جمع‌ها
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal ClosingBalance { get; set; }

        // 🆕 Dropdowns
        public List<SelectListItem> AccountOptions { get; set; } = new();
        public List<SelectListItem> ContactOptions { get; set; } = new();
    }

    public class DetailedReportLine
    {
        public int TransactionId { get; set; }
        public int TransactionNumber { get; set; }
        public DateOnly Date { get; set; }
        public string DateShamsi { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ContactName { get; set; }
        public string? Reference { get; set; }
        public string? ReferenceType { get; set; }   // Invoice, Cheque, Payment
        public int? ReferenceId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }
}