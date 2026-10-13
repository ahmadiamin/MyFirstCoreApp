using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Application.Common.Utilities;

/// <summary>
/// تنها جایی که «باقی‌مانده‌ی فاکتور» تعریف می‌شود:
///     باقی‌مانده = مبلغ کل − پرداخت‌شده − تخفیف تسویه
///
/// قبلاً PaymentService بدون تخفیف تسویه حساب می‌کرد و InvoiceService با آن؛
/// نتیجه این بود که بعد از تخفیف تسویه، می‌شد بیش از بدهی واقعی پرداخت تخصیص داد.
/// </summary>
public static class InvoiceBalance
{
    public static decimal Remaining(decimal totalAmount, decimal paidAmount, decimal settlementDiscount)
        => totalAmount - paidAmount - settlementDiscount;

    public static decimal Remaining(Invoice invoice)
        => Remaining(invoice.TotalAmount, invoice.PaidAmount, invoice.SettlementDiscount);

    /// <summary>فاکتورهایی که هنوز باقی‌مانده‌ی مثبت دارند (قابل ترجمه به SQL).</summary>
    public static IQueryable<Invoice> WithOpenBalance(this IQueryable<Invoice> invoices)
        => invoices.Where(i => i.TotalAmount - i.PaidAmount - i.SettlementDiscount > 0);
}
