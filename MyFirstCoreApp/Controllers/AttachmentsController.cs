using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Controllers;

/// <summary>
/// تنها راه دسترسی به فایل‌های پیوست (خودِ فایل‌ها بیرون از wwwroot و بدون URL مستقیم‌اند).
/// هر درخواست: ورود کاربر + شرکت فعال (فیلتر سراسری) + دسترسیِ موجودیتِ مالک.
/// </summary>
[Authorize]
public class AttachmentsController : Controller
{
    //private const long MaxRequestBytes = 10 * 1024 * 1024;   // کمی بیشتر از سقف فایل (5MB) برای سربار فرم

    //private readonly IAttachmentService _attachments;
    //private readonly IAuthorizationService _authorization;
    //private readonly ILogger<AttachmentsController> _logger;

    //public AttachmentsController(
    //    IAttachmentService attachments,
    //    IAuthorizationService authorization,
    //    ILogger<AttachmentsController> logger)
    //{
    //    _attachments = attachments;
    //    _authorization = authorization;
    //    _logger = logger;
    //}

    //// GET: /Attachments/Download/5?download=true
    //[HttpGet]
    //public async Task<IActionResult> Download(int id, bool download = false)
    //{
    //    AttachmentFile file;
    //    try
    //    {
    //        file = await _attachments.OpenAsync(id);
    //    }
    //    catch (InvalidOperationException)
    //    {
    //        return NotFound();
    //    }

    //    if (!await HasAnyAsync(AttachmentPolicies.View(file.EntityType)))
    //    {
    //        await file.Content.DisposeAsync();
    //        return Forbid();
    //    }

    //    // نوع فایل فقط از فهرست مجاز (jpg/png/webp/pdf) و از روی پسوند است؛ مرورگر حدس نزند
    //    Response.Headers["X-Content-Type-Options"] = "nosniff";

    //    var disposition = new ContentDispositionHeaderValue(download ? "attachment" : "inline");
    //    disposition.SetHttpFileName(file.FileName);
    //    Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();

    //    return File(file.Content, file.ContentType);
    //}

    //// POST: /Attachments/Upload
    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //[RequestSizeLimit(MaxRequestBytes)]
    //[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    //public async Task<IActionResult> Upload(
    //    AttachmentEntityType entityType, int entityId, IFormFile? file, string? description, string? returnUrl)
    //{
    //    if (!await HasAnyAsync(AttachmentPolicies.Edit(entityType)))
    //        return Forbid();

    //    if (file == null || file.Length == 0)
    //    {
    //        TempData["Error"] = "فایلی انتخاب نشده است.";
    //        return RedirectBack(returnUrl);
    //    }

    //    try
    //    {
    //        await using var stream = file.OpenReadStream();

    //        await _attachments.AddAsync(entityType, entityId, new AttachmentUpload
    //        {
    //            FileName = file.FileName,
    //            Length = file.Length,
    //            Content = stream,
    //            Description = description
    //        });

    //        TempData["Success"] = "پیوست با موفقیت اضافه شد.";
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        TempData["Error"] = ex.Message;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Attachment upload failed");
    //        TempData["Error"] = "خطای غیرمنتظره در ذخیره‌ی پیوست رخ داد.";
    //    }

    //    return RedirectBack(returnUrl);
    //}

    //// POST: /Attachments/Delete/5
    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> Delete(int id, string? returnUrl)
    //{
    //    AttachmentInfo info;
    //    try
    //    {
    //        info = await _attachments.GetInfoAsync(id);
    //    }
    //    catch (InvalidOperationException)
    //    {
    //        return NotFound();
    //    }

    //    if (!await HasAnyAsync(AttachmentPolicies.Edit(info.EntityType)))
    //        return Forbid();

    //    try
    //    {
    //        await _attachments.DeleteAsync(id);
    //        TempData["Success"] = "پیوست حذف شد.";
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        TempData["Error"] = ex.Message;
    //    }

    //    return RedirectBack(returnUrl);
    //}

    //private IActionResult RedirectBack(string? returnUrl)
    //    => Url.IsLocalUrl(returnUrl)
    //        ? LocalRedirect(returnUrl!)
    //        : RedirectToAction("Index", "Dashboard");

    ///// <summary>دسترسی به «هر یک» از سیاست‌ها کافی است (برای سند: Transaction یا Loan).</summary>
    //private async Task<bool> HasAnyAsync(string[] policies)
    //{
    //    foreach (var policy in policies)
    //    {
    //        if ((await _authorization.AuthorizeAsync(User, policy)).Succeeded)
    //            return true;
    //    }

    //    return false;
    //}
}
