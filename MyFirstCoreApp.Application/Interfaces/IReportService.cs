using MyFirstCoreApp.Application.Reports;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// گزارش‌ها با Stored Procedure (Dapper). همه‌ی متدها فقط روی «شرکت فعال» کار می‌کنند و
/// در نبود شرکت فعال یا ورودی نامعتبر InvalidOperationException با پیام فارسی می‌دهند.
///
/// مانده‌ی اول دوره:
///   - حساب‌های دائمی (بانک، دریافتنی، پرداختنی، ...): تجمعی از ابتدا.
///   - حساب‌های درآمد و هزینه: از ابتدای سال مالیِ شامل تاریخ شروع گزارش (در شروع سال مالی صفر می‌شوند).
///   - صورت‌حساب طرف حساب: همیشه تجمعی.
/// </summary>
public interface IReportService
{
    Task<LedgerReport> GetLedgerAsync(LedgerReportQuery query, CancellationToken cancellationToken = default);

    Task<DetailedReport> GetDetailedAsync(DetailedReportQuery query, CancellationToken cancellationToken = default);

    Task<ContactReport> GetContactAsync(ContactReportQuery query, CancellationToken cancellationToken = default);

    Task<InvoiceReport> GetInvoicesAsync(InvoiceReportQuery query, CancellationToken cancellationToken = default);
}
