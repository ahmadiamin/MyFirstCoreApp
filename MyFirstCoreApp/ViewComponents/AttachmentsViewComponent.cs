using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Controllers;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.ViewComponents;

public class AttachmentsViewModel
{
    public AttachmentEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public IReadOnlyList<AttachmentInfo> Items { get; set; } = Array.Empty<AttachmentInfo>();
    public bool CanEdit { get; set; }
    public string ReturnUrl { get; set; } = "/";
}

/// <summary>
/// فهرست پیوست‌ها + فرم آپلود برای هر صفحه‌ی جزئیات:
///   @await Component.InvokeAsync("Attachments", new { entityType = AttachmentEntityType.Cheque, entityId = Model.Id })
/// دسترسی دیدن/ویرایش از روی موجودیتِ مالک تعیین می‌شود (AttachmentPolicies).
/// </summary>
public class AttachmentsViewComponent : ViewComponent
{
    private readonly IAttachmentService _attachments;
    private readonly IAuthorizationService _authorization;

    public AttachmentsViewComponent(IAttachmentService attachments, IAuthorizationService authorization)
    {
        _attachments = attachments;
        _authorization = authorization;
    }

    public async Task<IViewComponentResult> InvokeAsync(AttachmentEntityType entityType, int entityId)
    {
        if (!await HasAnyAsync(AttachmentPolicies.View(entityType)))
            return Content(string.Empty);

        try
        {
            var items = await _attachments.ListAsync(entityType, entityId);

            return View(new AttachmentsViewModel
            {
                EntityType = entityType,
                EntityId = entityId,
                Items = items,
                CanEdit = await HasAnyAsync(AttachmentPolicies.Edit(entityType)),
                ReturnUrl = $"{HttpContext.Request.Path}{HttpContext.Request.QueryString}"
            });
        }
        catch (InvalidOperationException)
        {
            // مثلاً شرکت فعال نیست؛ صفحه‌ی اصلی نباید به‌خاطر پیوست بشکند
            return Content(string.Empty);
        }
    }

    private async Task<bool> HasAnyAsync(string[] policies)
    {
        foreach (var policy in policies)
        {
            if ((await _authorization.AuthorizeAsync(UserClaimsPrincipal, policy)).Succeeded)
                return true;
        }

        return false;
    }
}
