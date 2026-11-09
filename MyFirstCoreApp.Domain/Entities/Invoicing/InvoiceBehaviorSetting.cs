using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Invoicing;

public class InvoiceBehaviorSetting : TenantEntity
{
    // اجازه ثبت فروش با موجودی ناکافی
    public bool AllowNegativeStockOnSales { get; set; } = false;

    // اجازه ثبت برگشت از خرید با موجودی ناکافی
    public bool AllowNegativeStockOnPurchaseReturns { get; set; } = false;

    // اجازه برگشت فاکتور خرید به پیش‌نویس با موجودی ناکافی
    public bool AllowNegativeStockOnPurchaseReversal { get; set; } = false;

    // نمایش موجودی کالا در فرم ثبت فاکتور
    public bool ShowStockInInvoice { get; set; } = true;
}