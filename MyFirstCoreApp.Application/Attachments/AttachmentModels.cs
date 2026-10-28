using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Attachments;

/// <summary>تنظیمات پیوست (بخش "Attachments" در appsettings).</summary>
public class AttachmentOptions
{
    /// <summary>
    /// ریشه‌ی ذخیره‌ی فایل‌ها؛ باید «بیرون از wwwroot» باشد. مسیر نسبی به پوشه‌ی کاری برنامه وصل می‌شود؛
    /// در محیط واقعی یک مسیر مطلق بده و از آن Backup بگیر.
    /// </summary>
    public string RootPath { get; set; } = "App_Data/Attachments";

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>سقف تعداد پیوست برای هر موجودیت (جلوگیری از سوء‌استفاده).</summary>
    public int MaxFilesPerEntity { get; set; } = 10;
}

/// <summary>فایل آپلودشده، مستقل از ASP.NET (لایه‌ی Application به IFormFile وابسته نیست).</summary>
public sealed class AttachmentUpload
{
    public string FileName { get; set; } = string.Empty;
    public long Length { get; set; }
    public Stream Content { get; set; } = Stream.Null;
    public string? Description { get; set; }
}

/// <summary>فایلِ اعتبارسنجی‌شده و درون‌حافظه‌ای (حداکثر MaxFileSizeBytes)؛ آماده‌ی ذخیره.</summary>
public sealed class PreparedAttachment
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string Extension { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class AttachmentInfo
{
    public int Id { get; set; }
    public AttachmentEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Description { get; set; }

    /// <summary>لحظه‌ی آپلود (UTC).</summary>
    public DateTime UploadedAt { get; set; }
}

public sealed class AttachmentFile
{
    public Stream Content { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public AttachmentEntityType EntityType { get; init; }
    public int EntityId { get; init; }
}