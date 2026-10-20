namespace MyFirstCoreApp.Domain.Enums;

public enum PartyTransactionType
{
    InvoiceSale = 1,        // فاکتور فروش
    InvoicePurchase = 2,    // فاکتور خرید
    Payment = 3,            // پرداخت به طرف حساب
    Receipt = 4,            // دریافت از طرف حساب
    ChequeReceived = 5,     // چک دریافتی
    ChequePaid = 6,         // چک پرداختی
    SettlementWithDiscount = 7, // تسویه با تخفیف
    Loan = 8,               // قرض
    Other = 99
}