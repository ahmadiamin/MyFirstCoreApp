using MyFirstCoreApp.Application.Dashboard;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class DashboardMonthBucketsTests
{
    private static readonly DateOnly Start1405 = new(2026, 3, 21);
    private static readonly DateOnly End1405 = new(2027, 3, 20);

    [Fact]
    public void Build_ForAPersianFiscalYear_ReturnsTwelveMonths_WithCorrectBoundaries()
    {
        var buckets = DashboardMonthBuckets.Build(Start1405, End1405);

        Assert.Equal(12, buckets.Count);

        Assert.Equal("فروردین", buckets[0].MonthName);
        Assert.Equal("1405/01", buckets[0].Label);
        Assert.Equal(new DateOnly(2026, 3, 21), buckets[0].Start);
        Assert.Equal(new DateOnly(2026, 4, 20), buckets[0].End);     // فروردین 31 روز

        Assert.Equal("اردیبهشت", buckets[1].MonthName);
        Assert.Equal(new DateOnly(2026, 4, 21), buckets[1].Start);

        Assert.Equal("اسفند", buckets[11].MonthName);
        Assert.Equal("1405/12", buckets[11].Label);
        Assert.Equal(End1405, buckets[11].End);                        // 1405 کبیسه نیست (29 روز)
    }

    [Fact]
    public void Build_Buckets_AreContiguous_WithoutGapsOrOverlaps()
    {
        var buckets = DashboardMonthBuckets.Build(Start1405, End1405);

        for (var i = 1; i < buckets.Count; i++)
            Assert.Equal(buckets[i - 1].End.AddDays(1), buckets[i].Start);
    }

    [Fact]
    public void Build_ForAFiscalYearStartingMidMonth_Returns13PartialEdgeMonths()
    {
        // 2026-06-01 = 1405/03/11 تا 2027-05-31 = 1406/03/10
        var buckets = DashboardMonthBuckets.Build(new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31));

        Assert.Equal(13, buckets.Count);
        Assert.Equal(new DateOnly(2026, 5, 22), buckets[0].Start);     // ابتدای خرداد 1405
        Assert.Equal("1405/03", buckets[0].Label);
        Assert.Equal("1406/03", buckets[^1].Label);
    }

    [Fact]
    public void Build_ForInvalidRanges_ReturnsEmpty()
    {
        Assert.Empty(DashboardMonthBuckets.Build(default, End1405));
        Assert.Empty(DashboardMonthBuckets.Build(End1405, Start1405));
    }

    [Fact]
    public void Fill_AssignsEachDayToItsPersianMonth_AndSumsMultipleDays()
    {
        var buckets = DashboardMonthBuckets.Build(Start1405, End1405);

        DashboardMonthBuckets.Fill(buckets, new[]
        {
            new DailyFlow(new DateOnly(2026, 4, 20), Sales: 100, Purchases: 0, Received: 0, Paid: 0),   // آخرین روز فروردین
            new DailyFlow(new DateOnly(2026, 4, 21), Sales: 200, Purchases: 50, Received: 10, Paid: 5), // اولین روز اردیبهشت
            new DailyFlow(new DateOnly(2026, 5, 10), Sales: 300, Purchases: 0, Received: 20, Paid: 0),  // اردیبهشت
        });

        Assert.Equal(100, buckets[0].Sales);

        Assert.Equal(500, buckets[1].Sales);
        Assert.Equal(50, buckets[1].Purchases);
        Assert.Equal(30, buckets[1].Received);
        Assert.Equal(5, buckets[1].Paid);

        Assert.Equal(0, buckets[2].Sales);
    }

    [Fact]
    public void Fill_IgnoresDaysOutsideAllBuckets()
    {
        var buckets = DashboardMonthBuckets.Build(Start1405, End1405);

        DashboardMonthBuckets.Fill(buckets, new[]
        {
            new DailyFlow(new DateOnly(2026, 3, 20), 999, 999, 999, 999),   // روز قبل از سال
            new DailyFlow(new DateOnly(2027, 3, 21), 999, 999, 999, 999),   // روز بعد از سال
        });

        Assert.All(buckets, b =>
        {
            Assert.Equal(0, b.Sales);
            Assert.Equal(0, b.Purchases);
            Assert.Equal(0, b.Received);
            Assert.Equal(0, b.Paid);
        });
    }

    [Fact]
    public void Summary_MaxMonthlyValue_IsTheLargestOfAllFourSeries()
    {
        var summary = new DashboardSummary
        {
            Months =
            {
                new MonthBucket { Sales = 10, Purchases = 20, Received = 5, Paid = 7 },
                new MonthBucket { Sales = 1, Purchases = 2, Received = 90, Paid = 7 }
            }
        };

        Assert.Equal(90, summary.MaxMonthlyValue);
        Assert.Equal(0, new DashboardSummary().MaxMonthlyValue);
    }
}
