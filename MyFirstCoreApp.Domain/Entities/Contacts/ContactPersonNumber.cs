using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Contacts;

/// <summary>
/// شماره تماس یک شخص رابط؛ هر شخص می‌تواند چند موبایل یا تلفن ثابت داشته باشد.
/// </summary>
public class ContactPersonNumber : TenantEntity
{
    public int ContactPersonId { get; set; }
    public ContactPerson ContactPerson { get; set; } = null!;

    public ContactPersonNumberType NumberType { get; set; } = ContactPersonNumberType.Mobile;
    /// <summary>عنوان دلخواه شماره؛ مانند همراه اول، ایرانسل یا دفتر مرکزی.</summary>
    public string? Label { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public bool IsPrimary { get; set; }
}
