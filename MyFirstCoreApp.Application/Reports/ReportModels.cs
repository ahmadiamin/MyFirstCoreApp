using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Reports;

// ============================================
// معین
// ============================================

public sealed class LedgerReportQuery
{
    public int AccountId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public sealed class LedgerLine
{
    public int TransactionId { get; set; }
    public int TransactionNumber { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class LedgerReport
{
    public string AccountName { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public AccountType AccountType { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }

    public List<LedgerLine> Lines { get; set; } = new();
}

// ============================================
// تفصیلی
// ============================================

public sealed class DetailedReportQuery
{
    public int AccountId { get; set; }
    public int? ContactId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public bool OnlyPosted { get; set; } = true;
}

public sealed class DetailedLine
{
    public int TransactionId { get; set; }
    public int TransactionNumber { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public string? ContactName { get; set; }
    public string? Reference { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class DetailedReport
{
    public string AccountName { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public AccountType AccountType { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }

    public List<DetailedLine> Lines { get; set; } = new();
}

// ============================================
// صورت‌حساب طرف حساب
// ============================================

public sealed class ContactReportQuery
{
    public int ContactId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public bool OnlyPosted { get; set; } = true;
}

public sealed class ContactLine
{
    public int TransactionId { get; set; }
    public int TransactionNumber { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? AccountName { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class ContactReport
{
    public string ContactName { get; set; } = string.Empty;
    public string? ContactCode { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public ContactType ContactType { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }

    public List<ContactLine> Lines { get; set; } = new();
}

// ============================================
// فاکتورها
// ============================================

public sealed class InvoiceReportQuery
{
    public InvoiceType? InvoiceType { get; set; }
    public InvoiceStatus? Status { get; set; }
    public int? ContactId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>فقط فاکتورهای دارای باقی‌مانده‌ی مثبت.</summary>
    public bool OnlyOpen { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class InvoiceReportRow
{
    public int Id { get; set; }
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public InvoiceStatus Status { get; set; }
    public int? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal SettlementDiscount { get; set; }
    public decimal RemainingAmount { get; set; }
}

public sealed class InvoiceReport
{
    public List<InvoiceReportRow> Rows { get; set; } = new();

    // جمع «همه‌ی ردیف‌های فیلترشده» (نه فقط صفحه‌ی جاری)
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalSettlementDiscount { get; set; }
    public decimal TotalRemaining { get; set; }

    public int PageNumber { get; set; }
    public int PageSize { get; set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Count / (double)PageSize);
}
