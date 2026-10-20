using MyFirstCoreApp.Application.Common.Utilities;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class PersianDateAddMonthsTests
{
    [Theory]
    [InlineData("1405/01/01", 12, "1406/01/01")]
    [InlineData("1405/11/10", 3, "1406/02/10")]    // عبور از مرز سال
    [InlineData("1405/01/15", -1, "1404/12/15")]   // ماه منفی
    [InlineData("1405/06/31", 1, "1405/07/30")]    // روز ۳۱ در ماه سی‌روزه ← آخرین روز ماه
    [InlineData("1405/06/31", 6, "1405/12/29")]    // 1405 کبیسه نیست
    [InlineData("1403/06/31", 6, "1403/12/30")]    // 1403 کبیسه است
    public void AddMonths_WorksInThePersianCalendar(string start, int months, string expected)
    {
        var result = PersianDate.AddMonths(PersianDate.Parse(start), months);

        Assert.Equal(expected, PersianDate.Format(result));
    }

    [Theory]
    [InlineData("1405/01/01", "1405/12/29")]   // 365 روز
    [InlineData("1403/01/01", "1403/12/30")]   // سال کبیسه، 366 روز
    [InlineData("1405/04/10", "1406/04/09")]   // سال مالی غیر فروردینی
    public void TwelveMonthsMinusOneDay_IsTheLastDayOfAFiscalYear(string start, string expectedEnd)
    {
        var startDate = PersianDate.Parse(start);

        var end = PersianDate.AddMonths(startDate, 12).AddDays(-1);

        Assert.Equal(expectedEnd, PersianDate.Format(end));
    }
}
