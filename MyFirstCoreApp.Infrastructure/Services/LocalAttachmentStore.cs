using Microsoft.Extensions.Options;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Interfaces;

namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// ذخیره‌ی فایل روی دیسک: {Root}/{tenantId}/{yyyyMM}/{guid}{ext}
/// نام فایل کاملاً تصادفی است و هیچ بخشی از ورودی کاربر در مسیر نمی‌آید؛
/// برای خواندن/حذف هم مسیر کامل باید «داخل ریشه» بماند (جلوگیری از ../ و مسیر مطلق).
/// </summary>
public class LocalAttachmentStore : IAttachmentStore
{
    private readonly string _root;

    public LocalAttachmentStore(IOptions<AttachmentOptions> options)
    {
        var rootPath = options.Value.RootPath;

        _root = Path.GetFullPath(
            Path.IsPathRooted(rootPath)
                ? rootPath
                : Path.Combine(Directory.GetCurrentDirectory(), rootPath));
    }

    public async Task<string> SaveAsync(
        int tenantId, byte[] content, string extension, CancellationToken cancellationToken = default)
    {
        var relativePath = Path.Combine(
            tenantId.ToString(),
            DateTime.UtcNow.ToString("yyyyMM"),
            Guid.NewGuid().ToString("N") + extension);

        var fullPath = Resolve(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        return relativePath;
    }

    public Stream OpenRead(string relativePath)
    {
        var fullPath = Resolve(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("فایل پیوست روی دیسک پیدا نشد.", relativePath);

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
    }

    public void Delete(string relativePath)
    {
        try
        {
            var fullPath = Resolve(relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch
        {
            // حذف فیزیکی «بهترین تلاش» است؛ فایل یتیم خطری ندارد (از بیرون قابل دسترسی نیست)
        }
    }

    /// <summary>مسیر نسبی را به مسیر کامل تبدیل می‌کند و اگر از ریشه بیرون بزند خطا می‌دهد.</summary>
    private string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("مسیر پیوست نامعتبر است.");

        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("مسیر پیوست نامعتبر است.");

        return fullPath;
    }
}