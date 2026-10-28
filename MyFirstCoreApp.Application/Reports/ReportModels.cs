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

// ============================================
// بدهکاران / بستانکاران
// ============================================

public enum ContactBalanceMode
{
    /// <summary>بدهکاران: طرف حساب‌هایی که به ما بدهکارند (مانده‌ی بدهکار).</summary>
    Debtors = 1,

    /// <summary>بستانکاران: طرف حساب‌هایی که ما به آن‌ها بدهکاریم (مانده‌ی بستانکار).</summary>
    Creditors = 2
}

public enum ContactBalanceSort
{
    /// <summary>بیشترین مانده اول.</summary>
    LargestBalance = 1,

    /// <summary>بر اساس نام.</summary>
    Name = 2,

    /// <summary>قدیمی‌ترین «آخرین فعالیت» اول (طرف حساب‌های راکد).</summary>
    OldestActivity = 3
}

public sealed class ContactBalancesQuery
{
    public ContactBalanceMode Mode { get; set; } = ContactBalanceMode.Debtors;

    /// <summary>مانده‌ی دفتر تا این تاریخ (دو سر شامل).</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>فقط طرف حساب‌هایی که مانده‌شان حداقل این مقدار است.</summary>
    public decimal MinAmount { get; set; }

    /// <summary>جستجو در نام، کد و موبایل.</summary>
    public string? Search { get; set; }

    public ContactBalanceSort SortBy { get; set; } = ContactBalanceSort.LargestBalance;

    public int PageNumber { get; set; } = 1;

    /// <summary>0 = همه‌ی ردیف‌ها (برای خروجی Excel/چاپ).</summary>
    public int PageSize { get; set; } = 50;
}

public sealed class ContactBalanceRow
{
    public int ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactCode { get; set; }
    public string? Mobile { get; set; }

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    /// <summary>مانده‌ی مثبت در جهتِ گزارش (بدهکاران: بدهکار−بستانکار؛ بستانکاران: بستانکار−بدهکار).</summary>
    public decimal Amount { get; set; }

    public DateOnly? LastTransactionDate { get; set; }

    // فاکتورهای بازِ هم‌نوع (وضعیت فعلی)
    public int OpenInvoiceCount { get; set; }
    public decimal OpenInvoiceRemaining { get; set; }
    public decimal Age0To30 { get; set; }
    public decimal Age31To60 { get; set; }
    public decimal Age61To90 { get; set; }
    public decimal Age90Plus { get; set; }

    /// <summary>چک‌های در انتظار وصول/پاس (مبلغ‌شان قبلاً از مانده کم شده است).</summary>
    public decimal PendingChequeAmount { get; set; }

    /// <summary>
    /// سایر = مانده − فاکتورهای باز؛ قرض، پیش‌پرداخت تخصیص‌نیافته و اسناد دستی.
    /// منفی یعنی فاکتورهای باز از مانده بیشتر است (مثلاً پیش‌پرداخت).
    /// </summary>
    public decimal OtherAmount => Amount - OpenInvoiceRemaining;

    /// <summary>مانده با فرض اینکه همه‌ی چک‌های در انتظار برگردند.</summary>
    public decimal AmountIfChequesBounce => Amount + PendingChequeAmount;
}

public sealed class ContactBalancesReport
{
    public ContactBalanceMode Mode { get; set; }
    public DateOnly AsOf { get; set; }

    public List<ContactBalanceRow> Rows { get; set; } = new();

    // جمع «همه‌ی ردیف‌های فیلترشده» (نه فقط صفحه‌ی جاری)
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal TotalOpenInvoiceRemaining { get; set; }
    public decimal TotalAge0To30 { get; set; }
    public decimal TotalAge31To60 { get; set; }
    public decimal TotalAge61To90 { get; set; }
    public decimal TotalAge90Plus { get; set; }
    public decimal TotalPendingCheque { get; set; }

    public decimal TotalOther => TotalAmount - TotalOpenInvoiceRemaining;
    public decimal TotalIfChequesBounce => TotalAmount + TotalPendingCheque;

    public int PageNumber { get; set; }

    /// <summary>0 = همه.</summary>
    public int PageSize { get; set; }

    public int TotalPages => PageSize <= 0 ? (Count > 0 ? 1 : 0) : (int)Math.Ceiling(Count / (double)PageSize);
}
