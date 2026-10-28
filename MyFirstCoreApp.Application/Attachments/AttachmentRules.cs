using System.Text;

namespace MyFirstCoreApp.Application.Attachments;

/// <summary>
/// قواعد خالص پیوست (بدون دیتابیس/دیسک؛ قابل تست مستقیم).
///
///  - فقط jpg، jpeg، png، webp و pdf.
///  - نوع فایل از «امضای بایت‌ها» هم بررسی می‌شود؛ پسوند تنها کافی نیست
///    (فایل اجرایی با پسوند .jpg نباید ذخیره شود).
///  - ContentType همیشه از پسوندِ تأییدشده ساخته می‌شود، نه از مقدار ارسالیِ کلاینت.
/// </summary>
public static class AttachmentRules
{
    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    /// <summary>
    /// پسوندِ کوچک‌شده‌ی مجاز را برمی‌گرداند (با نقطه)؛ برای پسوند نامجاز یا بدون پسوند false.
    /// </summary>
    public static bool TryGetExtension(string? fileName, out string extension)
    {
        extension = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var ext = Path.GetExtension(fileName.Trim()).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return false;

        extension = ext;
        return true;
    }

    private static ReadOnlySpan<byte> PngSignature => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static ReadOnlySpan<byte> PdfSignature => new byte[] { 0x25, 0x50, 0x44, 0x46 };

    public static string ContentTypeFor(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    /// <summary>محتوای واقعیِ فایل (بایت‌های ابتدایی) باید با پسوند بخواند.</summary>
    public static bool MatchesSignature(string extension, ReadOnlySpan<byte> content)
    {
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                return content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;

            case ".png":
                return content.Length >= PngSignature.Length && content[..PngSignature.Length].SequenceEqual(PngSignature);

            case ".webp":
                // "RIFF" ???? "WEBP"
                return content.Length >= 12
                       && content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F'
                       && content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P';

            case ".pdf":
                // نشانه‌ی %PDF طبق استاندارد باید در 1024 بایت اول باشد
                var head = content.Length > 1024 ? content[..1024] : content;
                return head.IndexOf(PdfSignature) >= 0;

            default:
                return false;
        }
    }

    /// <summary>
    /// نام نمایشی امن: فقط نامِ فایل (بدون مسیر)، بدون نویسه‌های نامعتبر/کنترلی/جهت‌دهی، حداکثر ۱۵۰ کاراکتر
    /// با حفظ پسوند. این نام فقط نمایش و نام دانلود است؛ هرگز مسیر دیسک از آن ساخته نمی‌شود.
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        const int maxLength = 150;

        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var invalid = Path.GetInvalidFileNameChars();

        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsControl(ch) || invalid.Contains(ch) || IsDirectionalMark(ch))
                builder.Append('_');
            else
                builder.Append(ch);
        }

        var cleaned = builder.ToString().Trim().Trim('.');
        if (cleaned.Length == 0) return "file";

        if (cleaned.Length <= maxLength) return cleaned;

        var extension = Path.GetExtension(cleaned);
        var stem = cleaned[..(maxLength - extension.Length)];
        return stem + extension;
    }

    private static bool IsDirectionalMark(char ch)
        => ch is '\u200E' or '\u200F' or '\u202A' or '\u202B' or '\u202C' or '\u202D' or '\u202E'
            or '\u2066' or '\u2067' or '\u2068' or '\u2069';
}