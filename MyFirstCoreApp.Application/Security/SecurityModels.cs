namespace MyFirstCoreApp.Application.Security;

/// <summary>چه کسی (و در کدام شرکت) دسترسی می‌خواهد.</summary>
public sealed record PermissionSubject(bool IsAuthenticated, bool IsSuperAdmin, string? UserId, int? TenantId);

public sealed class PermissionItem
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>دسترسی‌های قابل‌اختصاص به نقش شرکت، گروه‌بندی‌شده (برای فرم نقش).</summary>
public sealed class PermissionGroupInfo
{
    public string GroupName { get; set; } = string.Empty;
    public List<PermissionItem> Permissions { get; set; } = new();
}

public sealed class TenantRoleSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int PermissionCount { get; set; }
    public int MemberCount { get; set; }
}

public sealed class TenantRoleDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<string> PermissionNames { get; set; } = new();
}

public sealed class TenantRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> PermissionNames { get; set; } = new();
}

public sealed class TenantMemberRole
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public sealed class TenantMemberInfo
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>نام حساب کاربری (سراسری).</summary>
    public string FullName { get; set; } = string.Empty;

    // مشخصات پرسنلی در همین شرکت
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? JobTitle { get; set; }
    public string? InternalPhone { get; set; }

    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsOwner { get; set; }
    public bool UserIsActive { get; set; }
    public List<TenantMemberRole> Roles { get; set; } = new();

    /// <summary>نام نمایشی: «نام فامیل» همین شرکت؛ اگر خالی بود نام حساب کاربری.</summary>
    public string DisplayName
    {
        get
        {
            var name = $"{FirstName} {LastName}".Trim();
            return name.Length > 0 ? name : FullName;
        }
    }
}

/// <summary>مشخصات پرسنلی یک عضو در یک شرکت.</summary>
public sealed class MemberProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? InternalPhone { get; set; }
}

/// <summary>افزودن یک نفر به شرکت (حساب جدید یا کاربرِ موجود).</summary>
public sealed class MemberCreateRequest
{
    public string Email { get; set; } = string.Empty;

    /// <summary>فقط برای حساب «جدید» لازم است؛ برای کاربر موجود نادیده گرفته می‌شود.</summary>
    public string? Password { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? InternalPhone { get; set; }
    public string? PhoneNumber { get; set; }

    public List<int> RoleIds { get; set; } = new();
}

public sealed class MemberCreateResult
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>true = حساب تازه ساخته شد؛ false = کاربرِ موجود به این شرکت اضافه شد.</summary>
    public bool CreatedNewAccount { get; set; }
}
