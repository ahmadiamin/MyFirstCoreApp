using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Application.Common.Utilities;

/// <summary>
/// تنها جای محاسبه‌ی مبالغ فاکتور.
///
/// قاعده‌ی گرد کردن (چون ریال اعشار ندارد و دیتابیس decimal(18,0) است):
///  - هر مؤلفه‌ی هر قلم جداگانه گرد می‌شود: مبلغ پایه، تخفیف، مالیات
///    (به نزدیک‌ترین عدد صحیح، نیم به سمت دور از صفر).
///  - جمع فاکتور = جمع مقادیر «گردشده»ی اقلام؛ پس همیشه
///        TotalAmount = Σ LineTotal  و  SubTotal − Discount + Tax = TotalAmount
///    و هیچ اختلافی بین جمع اقلام و جمع فاکتور رخ نمی‌دهد.
///
/// کد جاوااسکریپت فرم (updateTotals) باید همین ترتیب را رعایت کند.
/// </summary>
public static class InvoiceCalculator
{
    public static decimal RoundMoney(decimal value)
        => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    /// <summary>مبالغ اقلام را محاسبه و جمع‌های فاکتور را از روی همان اقلام تنظیم می‌کند.</summary>
    public static void Calculate(Invoice invoice)
        => Calculate(invoice, invoice.Items);

    /// <summary>
    /// مبالغ «items» را محاسبه می‌کند و جمع‌های فاکتور را از روی آن‌ها می‌گذارد
    /// (وقتی مجموعه‌ی Items فاکتور هنوز اقلام قدیمیِ در حال حذف را دارد، لازم است).
    /// </summary>
    public static void Calculate(Invoice invoice, IEnumerable<InvoiceItem> items)
    {
        decimal subTotal = 0;
        decimal discountTotal = 0;
        decimal taxTotal = 0;

        foreach (var item in items)
        {
            CalculateItem(item);

            subTotal += RoundMoney(item.Quantity * item.UnitPrice);
            discountTotal += item.DiscountAmount;
            taxTotal += item.TaxAmount;
        }

        invoice.SubTotal = subTotal;
        invoice.DiscountAmount = discountTotal;
        invoice.TaxAmount = taxTotal;
        invoice.TotalAmount = subTotal - discountTotal + taxTotal;
    }

    public static void CalculateItem(InvoiceItem item)
    {
        var lineBase = RoundMoney(item.Quantity * item.UnitPrice);

        item.DiscountAmount = RoundMoney(lineBase * item.DiscountPercent / 100m);

        var afterDiscount = lineBase - item.DiscountAmount;

        item.TaxAmount = RoundMoney(afterDiscount * item.TaxRate / 100m);
        item.LineTotal = afterDiscount + item.TaxAmount;
    }
}
