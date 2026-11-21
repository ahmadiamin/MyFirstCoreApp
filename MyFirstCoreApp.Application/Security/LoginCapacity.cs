namespace MyFirstCoreApp.Application.Security;

/// <summary>
/// «با چه سمتی وارد می‌شوی؟» برای کسی که هم مدیر یک کسب‌وکار است و هم کارمند جای دیگر.
/// سمت از روی عضویت‌ها تعیین می‌شود: مالکِ شرکت ← Owner؛ عضوِ غیرمالک ← Employee.
/// </summary>
public enum LoginCapacity
{
    /// <summary>مدیر کسب‌وکار (مالک شرکت).</summary>
    Owner = 1,

    /// <summary>کارمند (عضو غیرمالک شرکتی).</summary>
    Employee = 2
}

public sealed record TenantRef(int Id, string Name);

public sealed class LoginCapacityInfo
{
    public List<TenantRef> OwnerTenants { get; set; } = new();
    public List<TenantRef> EmployeeTenants { get; set; } = new();

    public bool HasOwner => OwnerTenants.Count > 0;
    public bool HasEmployee => EmployeeTenants.Count > 0;

    /// <summary>فقط وقتی کاربر «هر دو» سمت را دارد باید پرسید؛ با یک سمت یا بدون سمت، سؤالی نیست.</summary>
    public bool NeedsChoice => HasOwner && HasEmployee;

    /// <summary>اگر فقط یک سمت دارد همان؛ وگرنه null.</summary>
    public LoginCapacity? OnlyCapacity
        => HasOwner && !HasEmployee ? LoginCapacity.Owner
         : HasEmployee && !HasOwner ? LoginCapacity.Employee
         : null;

    public List<TenantRef> TenantsFor(LoginCapacity capacity)
        => capacity == LoginCapacity.Owner ? OwnerTenants : EmployeeTenants;
}
