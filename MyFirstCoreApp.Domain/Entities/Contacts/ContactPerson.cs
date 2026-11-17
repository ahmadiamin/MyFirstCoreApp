using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Contacts;

/// <summary>
/// شخص رابط یک طرف حساب؛ مانند مدیرعامل، مدیر مالی یا کارشناس فروش.
/// </summary>
public class ContactPerson : TenantEntity
{
    public int ContactId { get; set; }
    public Contact Contact { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ContactPersonNumber> Numbers { get; set; } = new List<ContactPersonNumber>();
}
