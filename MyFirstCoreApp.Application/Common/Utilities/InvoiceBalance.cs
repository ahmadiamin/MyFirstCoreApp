using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Application.Common.Utilities;

/// <summary>
/// تنها محل تعریف محاسبه باقی‌مانده فاکتور:
/// باقی‌مانده = مبلغ کل − پرداخت‌شده − تخفیف تسویه − اعتبار تخصیص‌یافته
/// </summary>
public static class InvoiceBalance
{
    public static decimal Remaining(
        decimal totalAmount,
        decimal paidAmount,
        decimal settlementDiscount,
        decimal appliedCreditAmount)
        => totalAmount - paidAmount - settlementDiscount - appliedCreditAmount;

    public static decimal Remaining(Invoice invoice)
        => Remaining(
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.SettlementDiscount,
            invoice.AppliedCreditAmount);

    /// <summary>
    /// فاکتورهایی که هنوز مانده مثبت دارند؛ قابل ترجمه به SQL توسط EF Core.
    /// </summary>
    public static IQueryable<Invoice> WithOpenBalance(
        this IQueryable<Invoice> invoices)
        => invoices.Where(i =>
            i.TotalAmount
            - i.PaidAmount
            - i.SettlementDiscount
            - i.AppliedCreditAmount > 0);
}