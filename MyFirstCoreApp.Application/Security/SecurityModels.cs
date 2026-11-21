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

/// <summary>
/// افزودن یک نفر به شرکت با «شماره‌ی موبایل» (هویت کارمند = موبایل).
/// رمز را مالک نمی‌دهد: برای حساب جدید، کدِ تعیین رمز به همین موبایل پیامک می‌شود و کارمند خودش رمز می‌گذارد.
/// </summary>
public sealed class MemberCreateRequest
{
    /// <summary>09xxxxxxxxx (ارقام فارسی/عربی نرمال می‌شود). شناسه‌ی یکتای حساب.</summary>
    public string Mobile { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? InternalPhone { get; set; }

    public List<int> RoleIds { get; set; } = new();
}

public sealed class MemberCreateResult
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>true = حساب تازه ساخته شد؛ false = حسابِ موجود (مثلاً مدیر شرکتی دیگر) به این شرکت اضافه شد.</summary>
    public bool CreatedNewAccount { get; set; }

    /// <summary>فقط برای حساب جدید: آیا پیامک کد تعیین رمز پذیرفته شد؟</summary>
    public bool PasswordSmsAccepted { get; set; }

    /// <summary>موبایل با ماسک (0912***4567) برای نمایش.</summary>
    public string MaskedMobile { get; set; } = string.Empty;
}

public sealed class StaffPasswordCodeResult
{
    public bool Accepted { get; set; }
    public string MaskedMobile { get; set; } = string.Empty;
}
