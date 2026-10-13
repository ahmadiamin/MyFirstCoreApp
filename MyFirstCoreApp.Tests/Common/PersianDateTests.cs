using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Infrastructure.Services;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class PersianDateTests
{
    // اول فروردین سال‌های شناخته‌شده
    [Theory]
    [InlineData("1400/01/01", 2021, 3, 21)]
    [InlineData("1403/01/01", 2024, 3, 20)]
    [InlineData("1404/01/01", 2025, 3, 21)]
    public void TryParse_KnownNowruzDates_ConvertsToGregorian(string text, int year, int month, int day)
    {
        Assert.True(PersianDate.TryParse(text, out var date));
        Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Theory]
    [InlineData("۱۴۰۴/۰۱/۰۱")]          // ارقام فارسی
    [InlineData("١٤٠٤/٠١/٠١")]          // ارقام عربی
    [InlineData("1404-01-01")]          // جداکننده‌ی -
    [InlineData("1404.1.1")]            // جداکننده‌ی . و بدون صفر
    [InlineData(" 1404 / 01 / 01 ")]    // فاصله
    [InlineData("\u200F1404/01/01")]    // RLM هنگام کپی‌پیست
    public void TryParse_AcceptsDigitsSeparatorsAndInvisibleMarks(string text)
    {
        Assert.True(PersianDate.TryParse(text, out var date));
        Assert.Equal(new DateOnly(2025, 3, 21), date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1404/01")]
    [InlineData("1404/01/01/05")]
    [InlineData("1404/13/01")]   // ماه ۱۳
    [InlineData("1404/00/10")]   // ماه صفر
    [InlineData("1404/01/32")]   // روز ۳۲
    [InlineData("1404/07/31")]   // ماه‌های ۷ تا ۱۲ سی‌روزه‌اند
    [InlineData("1404/12/30")]   // ۱۴۰۴ کبیسه نیست
    [InlineData("04/01/01")]     // سال دو رقمی
    public void TryParse_RejectsInvalidInput_WithoutThrowing(string? text)
    {
        Assert.False(PersianDate.TryParse(text, out var date));
        Assert.Equal(default, date);
    }

    [Fact]
    public void TryParse_30thOfEsfand_IsValidOnlyInLeapYears()
    {
        Assert.True(PersianDate.TryParse("1403/12/30", out _));   // کبیسه
        Assert.False(PersianDate.TryParse("1404/12/30", out _));
    }

    [Fact]
    public void Format_ThenTryParse_RoundTrips_ForTenYears()
    {
        var end = new DateOnly(2031, 3, 21);

        for (var day = new DateOnly(2021, 3, 21); day <= end; day = day.AddDays(1))
        {
            var text = PersianDate.Format(day);

            Assert.True(PersianDate.TryParse(text, out var parsed), $"parse failed: {text}");
            Assert.Equal(day, parsed);
        }
    }

    [Fact]
    public void Format_ProducesLatinDigitsByDefault_AndPersianOnRequest()
    {
        var date = new DateOnly(2025, 3, 21);

        Assert.Equal("1404/01/01", PersianDate.Format(date));
        Assert.Equal("۱۴۰۴/۰۱/۰۱", PersianDate.Format(date, persianDigits: true));
    }

    [Fact]
    public void Format_OfUnsetDate_ReturnsEmptyInsteadOfThrowing()
    {
        Assert.Equal(string.Empty, PersianDate.Format(default(DateOnly)));
        Assert.Equal(string.Empty, PersianDate.Format((DateOnly?)null));
    }

    [Fact]
    public void Periods_AreCalculatedInPersianCalendar()
    {
        var day = PersianDate.Parse("1404/06/15");

        Assert.Equal("1404/06/01", PersianDate.Format(PersianDate.StartOfMonth(day)));
        Assert.Equal("1404/06/31", PersianDate.Format(PersianDate.EndOfMonth(day)));
        Assert.Equal("1404/01/01", PersianDate.Format(PersianDate.StartOfYear(day)));
        Assert.Equal("1404/12/29", PersianDate.Format(PersianDate.EndOfYear(day)));

        var leap = PersianDate.Parse("1403/05/10");
        Assert.Equal("1403/12/30", PersianDate.Format(PersianDate.EndOfYear(leap)));
    }

    [Fact]
    public void Today_ShortlyAfterIranMidnight_IsNextDay_EvenThoughUtcIsStillPreviousDay()
    {
        // 2026-03-20 21:00 UTC  =  2026-03-21 00:30 به وقت ایران
        // این همان باگی است که با DateTime.UtcNow رخ می‌داد.
        var provider = new FixedUtcTimeProvider(
            new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 3, 21), provider.Today());
    }

    private sealed class FixedUtcTimeProvider : IranTimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
