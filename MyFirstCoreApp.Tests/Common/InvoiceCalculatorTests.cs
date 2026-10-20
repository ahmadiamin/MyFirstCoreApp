using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class InvoiceCalculatorTests
{
    [Fact]
    public void CalculateItem_RoundsEachComponentToWholeRial()
    {
        // پایه 3×1٬234 = 3٬702 ؛ تخفیف 7.5٪ = 277.65 → 278 ؛ مالیات 9٪ از 3٬424 = 308.16 → 308
        var item = new InvoiceItem { Quantity = 3, UnitPrice = 1_234, DiscountPercent = 7.5m, TaxRate = 9 };

        InvoiceCalculator.CalculateItem(item);

        Assert.Equal(278, item.DiscountAmount);
        Assert.Equal(308, item.TaxAmount);
        Assert.Equal(3_732, item.LineTotal);
    }

    [Fact]
    public void CalculateItem_RoundsMidpointAwayFromZero_NotToEven()
    {
        // 5 × 10٪ = 0.5 ← باید 1 شود (گرد بانکی 0 می‌داد)
        var item = new InvoiceItem { Quantity = 1, UnitPrice = 5, DiscountPercent = 10, TaxRate = 0 };

        InvoiceCalculator.CalculateItem(item);

        Assert.Equal(1, item.DiscountAmount);
        Assert.Equal(4, item.LineTotal);
    }

    [Fact]
    public void CalculateItem_SupportsFractionalQuantity()
    {
        var item = new InvoiceItem { Quantity = 2.5m, UnitPrice = 1_000, DiscountPercent = 0, TaxRate = 0 };

        InvoiceCalculator.CalculateItem(item);

        Assert.Equal(2_500, item.LineTotal);
    }

    [Fact]
    public void Calculate_InvoiceTotalEqualsSumOfLineTotals()
    {
        var invoice = new Invoice
        {
            Items =
            {
                new InvoiceItem { Quantity = 3, UnitPrice = 1_234, DiscountPercent = 7.5m, TaxRate = 9 },
                new InvoiceItem { Quantity = 1, UnitPrice = 1_000, DiscountPercent = 0, TaxRate = 9 }
            }
        };

        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(4_702, invoice.SubTotal);
        Assert.Equal(278, invoice.DiscountAmount);
        Assert.Equal(398, invoice.TaxAmount);
        Assert.Equal(4_822, invoice.TotalAmount);
        Assert.Equal(invoice.Items.Sum(i => i.LineTotal), invoice.TotalAmount);
    }

    [Fact]
    public void Calculate_Invariants_HoldForManyCombinations()
    {
        var quantities = new[] { 0.5m, 1m, 2.5m, 3m, 7m, 12.75m };
        var prices = new[] { 1m, 999m, 1_234m, 45_678m, 1_000_000m };
        var discounts = new[] { 0m, 2.5m, 7.5m, 10m, 33.33m };
        var taxes = new[] { 0m, 9m, 10m };

        foreach (var q in quantities)
        foreach (var p in prices)
        foreach (var d in discounts)
        foreach (var t in taxes)
        {
            var invoice = new Invoice
            {
                Items =
                {
                    new InvoiceItem { Quantity = q, UnitPrice = p, DiscountPercent = d, TaxRate = t },
                    new InvoiceItem { Quantity = 1, UnitPrice = 777, DiscountPercent = d, TaxRate = t }
                }
            };

            InvoiceCalculator.Calculate(invoice);

            // همه‌ی مبالغ صحیح‌اند و جمع فاکتور با جمع اقلام و با فرمول کلی برابر است
            Assert.All(invoice.Items, i =>
            {
                Assert.Equal(decimal.Truncate(i.LineTotal), i.LineTotal);
                Assert.Equal(decimal.Truncate(i.DiscountAmount), i.DiscountAmount);
                Assert.Equal(decimal.Truncate(i.TaxAmount), i.TaxAmount);
            });

            Assert.Equal(invoice.Items.Sum(i => i.LineTotal), invoice.TotalAmount);
            Assert.Equal(invoice.SubTotal - invoice.DiscountAmount + invoice.TaxAmount, invoice.TotalAmount);
        }
    }

    [Fact]
    public void Calculate_WithExplicitItems_IgnoresItemsOfTheInvoiceCollection()
    {
        // سناریوی ویرایش: Items فاکتور هنوز اقلام در حال حذف را دارد
        var invoice = new Invoice
        {
            Items = { new InvoiceItem { Quantity = 1, UnitPrice = 999_999 } }
        };
        var newItems = new List<InvoiceItem>
        {
            new() { Quantity = 2, UnitPrice = 500 }
        };

        InvoiceCalculator.Calculate(invoice, newItems);

        Assert.Equal(1_000, invoice.TotalAmount);
    }
}
