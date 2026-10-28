using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Loans;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// قرض داده‌شده/گرفته‌شده و بازپرداخت‌ها روی همان دفتر حسابداری.
///   قرض داده‌شده : دریافتنی-طرف‌حساب (بدهکار) / بانک یا صندوق (بستانکار)
///   قرض گرفته‌شده: بانک یا صندوق (بدهکار) / پرداختنی-طرف‌حساب (بستانکار)
///   بازپرداخت    : معکوس همان‌ها.
/// همه‌ی اسناد از مسیر ITransactionService می‌گذرند (سال مالیِ بسته = رد).
/// قاعده‌ی نقض‌شده با InvalidOperationException و پیام فارسی اعلام می‌شود.
/// </summary>
public interface ILoanService
{
    /// <summary>
    /// ثبت قرض. اگر رسید داده شود، «قبل از ساخت سند» اعتبارسنجی می‌شود و در همان تراکنش به سند پیوست می‌شود؛
    /// رسید نامعتبر یعنی هیچ قرضی ثبت نمی‌شود. شناسه‌ی قرض (= سند) را برمی‌گرداند.
    /// </summary>
    Task<int> CreateAsync(LoanRequest request, int tenantId, AttachmentUpload? receipt = null,
        CancellationToken cancellationToken = default);

    /// <summary>ثبت بازپرداخت (حداکثر تا باقی‌مانده‌ی قرض). شناسه‌ی سند بازپرداخت را برمی‌گرداند.</summary>
    Task<int> RepayAsync(int loanId, LoanRepaymentRequest request, int tenantId,
        AttachmentUpload? receipt = null, CancellationToken cancellationToken = default);

    /// <summary>حذف قرضِ بدون بازپرداخت.</summary>
    Task DeleteAsync(int loanId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>حذف یک بازپرداخت.</summary>
    Task DeleteRepaymentAsync(int repaymentId, int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoanListItem>> GetLoansAsync(LoanDirection? direction, bool onlyOpen,
        CancellationToken cancellationToken = default);

    /// <summary>جزئیات قرض با بازپرداخت‌ها؛ اگر نبود null.</summary>
    Task<LoanDetails?> GetAsync(int loanId, CancellationToken cancellationToken = default);
}