using System.Security.Cryptography;

namespace MyFirstCoreApp.Infrastructure.Security;

/// <summary>
/// رمز تصادفیِ قوی برای حساب‌هایی که «کسی رمزشان را نمی‌داند» (کارمند تازه تا وقتی با کد پیامکی رمز بگذارد).
/// از RandomNumberGenerator امن استفاده می‌کند؛ هرگز نمایش یا لاگ نمی‌شود.
/// حداقل یک حرف بزرگ، کوچک، رقم و نماد دارد (با سیاست پیش‌فرض Identity سازگار است).
/// </summary>
public static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+";

    public static string Generate(int length = 20)
    {
        if (length < 12)
            throw new ArgumentOutOfRangeException(nameof(length), "طول رمز تصادفی حداقل ۱۲ است.");

        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[length];

        // یکی از هر دسته تضمین شود
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);

        for (var i = 4; i < length; i++)
            chars[i] = Pick(all);

        // جابه‌جایی تصادفی (Fisher–Yates) تا جای دسته‌ها قابل‌پیش‌بینی نباشد
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
