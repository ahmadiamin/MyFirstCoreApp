using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// پیوست عمومی. فقط روی «شرکت فعال» کار می‌کند و پیوستِ شرکت دیگر هرگز دیده/دانلود نمی‌شود.
/// قاعده‌ی نقض‌شده با InvalidOperationException و پیام فارسی اعلام می‌شود.
/// کنترل دسترسی (چه کسی ببیند/ویرایش کند) با کنترلرها و بر اساس موجودیتِ مالک است.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// فایل را می‌خواند و اعتبارسنجی می‌کند: پسوند مجاز، حجم، امضای واقعی. بدون ذخیره.
    /// برای سرویس‌هایی که باید «قبل از ساخت چیزی» فایل را بسنجند (مثل ثبت قرض با رسید).
    /// </summary>
    Task<PreparedAttachment> PrepareAsync(AttachmentUpload upload, CancellationToken cancellationToken = default);

    /// <summary>ذخیره‌ی فایل آماده‌شده و ثبت رکورد. وجود موجودیتِ مالک در شرکت فعال بررسی می‌شود.</summary>
    Task<AttachmentInfo> AddAsync(AttachmentEntityType entityType, int entityId, PreparedAttachment file,
        CancellationToken cancellationToken = default);

    /// <summary>Prepare + Add.</summary>
    Task<AttachmentInfo> AddAsync(AttachmentEntityType entityType, int entityId, AttachmentUpload upload,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttachmentInfo>> ListAsync(AttachmentEntityType entityType, int entityId,
        CancellationToken cancellationToken = default);

    /// <summary>مشخصات یک پیوست (برای بررسی دسترسی پیش از دانلود/حذف).</summary>
    Task<AttachmentInfo> GetInfoAsync(int attachmentId, CancellationToken cancellationToken = default);

    /// <summary>فایل را برای دانلود باز می‌کند.</summary>
    Task<AttachmentFile> OpenAsync(int attachmentId, CancellationToken cancellationToken = default);

    /// <summary>حذف نرم رکورد (فایل فیزیکی برای سابقه می‌ماند).</summary>
    Task DeleteAsync(int attachmentId, CancellationToken cancellationToken = default);
}
