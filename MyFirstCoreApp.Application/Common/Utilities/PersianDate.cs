using System.Globalization;
using System.Text;

namespace MyFirstCoreApp.Application.Common.Utilities;

/// <summary>
/// تنها نقطه‌ی تبدیل بین «تاریخ شمسی (متن)» و «DateOnly میلادی».
/// هیچ‌جای دیگر برنامه نباید PersianCalendar یا Replace ارقام را مستقیم صدا بزند.
///
/// قرارداد:
///  - ورودی: «1405/01/10» با ارقام فارسی/عربی/لاتین، جداکننده‌ی / یا - یا .
///  - TryParse هرگز Exception نمی‌دهد.
///  - Format همیشه با ارقام لاتین برمی‌گرداند، مگر persianDigits = true.
/// </summary>
public static class PersianDate
{
    private static readonly PersianCalendar Calendar = new();

    // PersianCalendar فقط تا 9378/10/10 (= 9999/12/31 میلادی) را پشتیبانی می‌کند
    private const int MaxYear = 9377;

    // ---------------------------------------------------------------
    // Parse
    // ---------------------------------------------------------------

    public static bool TryParse(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = Clean(text).Split('/', '-', '.');
        if (parts.Length != 3) return false;

        // سال باید ۴ رقمی باشد تا «05/01/10» به‌اشتباه پذیرفته نشود
        if (parts[0].Length != 4 || parts[1].Length is < 1 or > 2 || parts[2].Length is < 1 or > 2)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            return false;

        return TryFromParts(year, month, day, out date);
    }

    public static DateOnly? ParseOrNull(string? text)
        => TryParse(text, out var date) ? date : null;

    public static DateOnly Parse(string? text)
        => TryParse(text, out var date)
            ? date
            : throw new FormatException($"تاریخ شمسی نامعتبر است: '{text}'");

    public static bool TryFromParts(int year, int month, int day, out DateOnly date)
    {
        date = default;

        if (year < 1 || year > MaxYear) return false;
        if (month < 1 || month > 12) return false;
        if (day < 1 || day > Calendar.GetDaysInMonth(year, month)) return false;

        date = DateOnly.FromDateTime(Calendar.ToDateTime(year, month, day, 0, 0, 0, 0));
        return true;
    }

    // ---------------------------------------------------------------
    // Format
    // ---------------------------------------------------------------

    /// <summary>
    /// برای تاریخ نامعتبر/تنظیم‌نشده (مثل default) رشته‌ی خالی برمی‌گرداند تا View نشکند.
    /// </summary>
    public static string Format(DateOnly date, bool persianDigits = false)
    {
        if (!IsSupported(date)) return string.Empty;

        var (year, month, day) = ToParts(date);
        var text = string.Create(CultureInfo.InvariantCulture, $"{year:0000}/{month:00}/{day:00}");

        return persianDigits ? ToPersianDigits(text) : text;
    }

    public static string Format(DateOnly? date, bool persianDigits = false)
        => date.HasValue ? Format(date.Value, persianDigits) : string.Empty;

    public static (int Year, int Month, int Day) ToParts(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return (Calendar.GetYear(dateTime), Calendar.GetMonth(dateTime), Calendar.GetDayOfMonth(dateTime));
    }

    // ---------------------------------------------------------------
    // بازه‌ها (برای پیش‌فرض گزارش‌ها)
    // ---------------------------------------------------------------

    public static DateOnly StartOfMonth(DateOnly date)
    {
        var (year, month, _) = ToParts(date);
        return FromPartsOrThrow(year, month, 1);
    }

    public static DateOnly EndOfMonth(DateOnly date)
    {
        var (year, month, _) = ToParts(date);
        return FromPartsOrThrow(year, month, Calendar.GetDaysInMonth(year, month));
    }

    public static DateOnly StartOfYear(DateOnly date)
    {
        var (year, _, _) = ToParts(date);
        return FromPartsOrThrow(year, 1, 1);
    }

    public static DateOnly EndOfYear(DateOnly date)
    {
        var (year, _, _) = ToParts(date);
        return FromPartsOrThrow(year, 12, Calendar.GetDaysInMonth(year, 12));
    }

    /// <summary>
    /// ماه شمسی اضافه می‌کند؛ اگر روز در ماه مقصد وجود نداشته باشد به آخرین روز آن ماه می‌رود
    /// (مثلاً 1405/06/31 + ۱ ماه = 1405/07/30).
    /// پایان سال مالی ۱۲ ماهه: AddMonths(شروع, 12).AddDays(-1).
    /// </summary>
    public static DateOnly AddMonths(DateOnly date, int months)
    {
        var (year, month, day) = ToParts(date);

        var totalMonths = year * 12 + (month - 1) + months;
        var newYear = totalMonths / 12;
        var newMonth = totalMonths % 12 + 1;

        var lastDay = Calendar.GetDaysInMonth(newYear, newMonth);
        return FromPartsOrThrow(newYear, newMonth, Math.Min(day, lastDay));
    }

    // ---------------------------------------------------------------
    // ارقام
    // ---------------------------------------------------------------

    /// <summary>ارقام فارسی (۰-۹) و عربی (٠-٩) را به لاتین تبدیل می‌کند.</summary>
    public static string NormalizeDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(ch switch
            {
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                _ => ch
            });
        }

        return builder.ToString();
    }

    public static string ToPersianDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
            builder.Append(ch is >= '0' and <= '9' ? (char)('\u06F0' + (ch - '0')) : ch);

        return builder.ToString();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static bool IsSupported(DateOnly date)
        => date.ToDateTime(TimeOnly.MinValue) >= Calendar.MinSupportedDateTime;

    private static DateOnly FromPartsOrThrow(int year, int month, int day)
        => TryFromParts(year, month, day, out var date)
            ? date
            : throw new ArgumentOutOfRangeException(nameof(year), $"تاریخ شمسی نامعتبر: {year}/{month}/{day}");

    /// <summary>
    /// ارقام را لاتین می‌کند و فاصله‌ها و نشانه‌های جهت‌دهی نامرئی (RLM/LRM/…)
    /// را که هنگام کپی‌پیست وارد متن می‌شوند حذف می‌کند.
    /// </summary>
    private static string Clean(string text)
    {
        var normalized = NormalizeDigits(text);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch)) continue;
            if (ch is '\u200E' or '\u200F' or '\u200B' or '\u200C' or '\u200D') continue;
            if (ch is >= '\u202A' and <= '\u202E') continue;
            if (ch is >= '\u2066' and <= '\u2069') continue;

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
