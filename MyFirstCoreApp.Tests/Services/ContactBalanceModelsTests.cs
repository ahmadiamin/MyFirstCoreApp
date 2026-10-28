using MyFirstCoreApp.Application.Reports;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>منطق خالص مدل گزارش بدهکاران (بدون دیتابیس).</summary>
public class ContactBalanceModelsTests
{
    [Fact]
    public void Row_OtherAmount_IsTheBalanceMinusOpenInvoices()
    {
        var row = new ContactBalanceRow { Amount = 700, OpenInvoiceRemaining = 500 };
        Assert.Equal(200, row.OtherAmount);                 // قرض، پیش‌پرداخت‌نشده، سند دستی

        var advance = new ContactBalanceRow { Amount = 300, OpenInvoiceRemaining = 800 };
        Assert.Equal(-500, advance.OtherAmount);            // فاکتورهای باز بیشتر از مانده (پیش‌پرداخت)
    }

    [Fact]
    public void Row_AmountIfChequesBounce_AddsThePendingCheques()
    {
        var row = new ContactBalanceRow { Amount = 700, PendingChequeAmount = 200 };

        Assert.Equal(900, row.AmountIfChequesBounce);
    }

    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(1, 50, 1)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    [InlineData(101, 50, 3)]
    public void Report_TotalPages_RoundsUp(int count, int pageSize, int expected)
    {
        var report = new ContactBalancesReport { Count = count, PageSize = pageSize };

        Assert.Equal(expected, report.TotalPages);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 1)]
    public void Report_PageSizeZero_MeansASinglePage_WhenThereAreRows(int count, int expected)
    {
        var report = new ContactBalancesReport { Count = count, PageSize = 0 };

        Assert.Equal(expected, report.TotalPages);
    }

    [Fact]
    public void Report_Totals_AreDerivedConsistently()
    {
        var report = new ContactBalancesReport
        {
            TotalAmount = 1200,
            TotalOpenInvoiceRemaining = 1000,
            TotalPendingCheque = 200
        };

        Assert.Equal(200, report.TotalOther);
        Assert.Equal(1400, report.TotalIfChequesBounce);
    }
}
