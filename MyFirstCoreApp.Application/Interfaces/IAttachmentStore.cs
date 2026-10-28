namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// ذخیره‌ی فیزیکی فایل. جدا از IAttachmentService تا بعداً بشود به فضای ابری هم وصل کرد
/// و در تست‌ها نسخه‌ی درون‌حافظه‌ای گذاشت.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>فایل را ذخیره می‌کند و مسیر «نسبی» آن را برمی‌گرداند (نام تصادفی).</summary>
    Task<string> SaveAsync(int tenantId, byte[] content, string extension, CancellationToken cancellationToken = default);

    /// <summary>برای خواندن باز می‌کند؛ اگر نبود FileNotFoundException.</summary>
    Stream OpenRead(string relativePath);

    /// <summary>حذف فیزیکی (بهترین تلاش؛ خطا نمی‌دهد).</summary>
    void Delete(string relativePath);
}
