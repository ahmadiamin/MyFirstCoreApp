using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Attachments;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAttachmentStore _store;
    private readonly AttachmentOptions _options;

    public AttachmentService(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IAttachmentStore store,
        IOptions<AttachmentOptions> options)
    {
        _context = context;
        _currentTenant = currentTenant;
        _store = store;
        _options = options.Value;
    }

    // ============================================
    // آماده‌سازی و اعتبارسنجی
    // ============================================

    public async Task<PreparedAttachment> PrepareAsync(AttachmentUpload upload, CancellationToken cancellationToken = default)
    {
        if (upload.Length <= 0)
            throw new InvalidOperationException("فایل خالی است.");

        if (upload.Length > _options.MaxFileSizeBytes)
            throw new InvalidOperationException($"حجم فایل بیشتر از {FormatSize(_options.MaxFileSizeBytes)} است.");

        if (!AttachmentRules.TryGetExtension(upload.FileName, out var extension))
            throw new InvalidOperationException("فقط فایل‌های jpg، png، webp و pdf مجاز است.");

        // طول اعلام‌شده قابل اعتماد نیست؛ خود جریان با سقفِ سخت خوانده می‌شود
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await upload.Content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > _options.MaxFileSizeBytes)
                throw new InvalidOperationException($"حجم فایل بیشتر از {FormatSize(_options.MaxFileSizeBytes)} است.");

            buffer.Write(chunk, 0, read);
        }

        var content = buffer.ToArray();

        if (content.Length == 0)
            throw new InvalidOperationException("فایل خالی است.");

        // محتوای واقعی باید با پسوند بخواند (فایل اجرایی با پسوند عوض‌شده رد می‌شود)
        if (!AttachmentRules.MatchesSignature(extension, content))
            throw new InvalidOperationException("محتوای فایل با نوع آن (پسوند) نمی‌خواند.");

        return new PreparedAttachment
        {
            Content = content,
            Extension = extension,
            DisplayName = AttachmentRules.SanitizeFileName(upload.FileName),
            ContentType = AttachmentRules.ContentTypeFor(extension),
            Description = string.IsNullOrWhiteSpace(upload.Description) ? null : upload.Description.Trim()
        };
    }

    // ============================================
    // افزودن
    // ============================================

    public async Task<AttachmentInfo> AddAsync(
        AttachmentEntityType entityType, int entityId, AttachmentUpload upload, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(upload, cancellationToken);
        return await AddAsync(entityType, entityId, prepared, cancellationToken);
    }

    public async Task<AttachmentInfo> AddAsync(
        AttachmentEntityType entityType, int entityId, PreparedAttachment file, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();

        if (!Enum.IsDefined(entityType))
            throw new InvalidOperationException("نوع موجودیت نامعتبر است.");

        if (!await OwnerExistsAsync(entityType, entityId, cancellationToken))
            throw new InvalidOperationException("موجودیتِ مقصد برای پیوست پیدا نشد.");

        var count = await _context.Attachments
            .CountAsync(a => a.EntityType == entityType && a.EntityId == entityId, cancellationToken);

        if (count >= _options.MaxFilesPerEntity)
            throw new InvalidOperationException($"حداکثر {_options.MaxFilesPerEntity} پیوست برای هر مورد مجاز است.");

        var storedPath = await _store.SaveAsync(tenantId, file.Content, file.Extension, cancellationToken);

        var attachment = new Attachment
        {
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            FileName = file.DisplayName,
            StoredPath = storedPath,
            ContentType = file.ContentType,
            SizeBytes = file.Content.LongLength,
            Description = file.Description
        };

        try
        {
            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // رکورد ذخیره نشد؛ فایل یتیم نماند
            _store.Delete(storedPath);
            throw;
        }

        return ToInfo(attachment);
    }

    // ============================================
    // خواندن
    // ============================================

    public async Task<IReadOnlyList<AttachmentInfo>> ListAsync(
        AttachmentEntityType entityType, int entityId, CancellationToken cancellationToken = default)
    {
        RequireTenant();

        var rows = await _context.Attachments
            .AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToInfo).ToList();
    }

    public async Task<AttachmentInfo> GetInfoAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        RequireTenant();

        var attachment = await _context.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("پیوست پیدا نشد.");

        return ToInfo(attachment);
    }

    public async Task<AttachmentFile> OpenAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        RequireTenant();

        // فیلتر سراسریِ شرکت: پیوست شرکت دیگر همین‌جا «پیدا نشد» می‌شود
        var attachment = await _context.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("پیوست پیدا نشد.");

        Stream stream;
        try
        {
            stream = _store.OpenRead(attachment.StoredPath);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException("فایل این پیوست روی سرور پیدا نشد.");
        }

        return new AttachmentFile
        {
            Content = stream,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            EntityType = attachment.EntityType,
            EntityId = attachment.EntityId
        };
    }

    // ============================================
    // حذف
    // ============================================

    public async Task DeleteAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        RequireTenant();

        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new InvalidOperationException("پیوست پیدا نشد.");

        // حذف نرم (Interceptor)؛ فایل فیزیکی برای سابقه می‌ماند
        _context.Attachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ============================================
    // Helpers
    // ============================================

    private static string FormatSize(long bytes)
        => bytes >= 1024 * 1024
            ? $"{bytes / (1024 * 1024)} مگابایت"
            : $"{Math.Max(bytes / 1024, 1)} کیلوبایت";

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("شرکت فعال مشخص نیست.");

    /// <summary>
    /// موجودیتِ مالک باید در «شرکت فعال» وجود داشته باشد (فیلتر سراسری) تا نشود به رکورد
    /// شرکت دیگر پیوست زد.
    /// </summary>
    private Task<bool> OwnerExistsAsync(AttachmentEntityType type, int id, CancellationToken ct) => type switch
    {
        AttachmentEntityType.Transaction => _context.Transactions.AnyAsync(x => x.Id == id, ct),
        AttachmentEntityType.Cheque => _context.Cheques.AnyAsync(x => x.Id == id, ct),
        AttachmentEntityType.Payment => _context.Payments.AnyAsync(x => x.Id == id, ct),
        AttachmentEntityType.Invoice => _context.Invoices.AnyAsync(x => x.Id == id, ct),
        AttachmentEntityType.Product => _context.Products.AnyAsync(x => x.Id == id, ct),
        AttachmentEntityType.Contact => _context.Contacts.AnyAsync(x => x.Id == id, ct),
        _ => Task.FromResult(false)
    };

    private static AttachmentInfo ToInfo(Attachment attachment) => new()
    {
        Id = attachment.Id,
        EntityType = attachment.EntityType,
        EntityId = attachment.EntityId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        SizeBytes = attachment.SizeBytes,
        Description = attachment.Description,
        UploadedAt = attachment.CreatedAt
    };
}