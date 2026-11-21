namespace MyFirstCoreApp.Application.Security;

public static class PhoneMask
{
    /// <summary>09121234567 ← 0912***4567 (برای نمایش به مالک؛ شماره‌ی کامل کارمند افشا نمی‌شود).</summary>
    public static string Mask(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;

        var digits = mobile.Trim();

        return digits.Length < 8
            ? new string('*', digits.Length)
            : digits[..4] + new string('*', digits.Length - 8) + digits[^4..];
    }
}
