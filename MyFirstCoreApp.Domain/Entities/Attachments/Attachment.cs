using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Attachments;

/// <summary>
/// پیوست عمومی (رسید، تصویر چک، ...) برای هر موجودیتِ فهرست AttachmentEntityType.
/// خودِ فایل در Store ذخیره می‌شود؛ اینجا فقط مشخصات آن است.
/// </summary>
public class Attachment : TenantEntity
{
    public AttachmentEntityType EntityType { get; set; }
    public int EntityId { get; set; }

    /// <summary>نام نمایشی (پاک‌سازی‌شده؛ هرگز به‌عنوان مسیر استفاده نمی‌شود).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>مسیر نسبی در Store (نام تصادفی؛ از نام اصلیِ کاربر ساخته نمی‌شود).</summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>از روی پسوند تعیین می‌شود، نه از مقدار ارسالیِ کلاینت.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
    public string? Description { get; set; }
}