using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;

namespace MyFirstCoreApp.Infrastructure.Security;

public class TenantSecurityService : ITenantSecurityService
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 300;
    private const int MaxInternalPhoneLength = 10;

    private readonly ApplicationDbContext _context;

    public TenantSecurityService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================
    // سمت‌ها (نقش‌های شرکت)
    // ============================================

    public async Task<IReadOnlyList<PermissionGroupInfo>> GetAssignablePermissionsAsync()
    {
        var items = await _context.Permissions
            .Where(p => p.IsActive)
            .Select(p => new { p.Name, p.DisplayName, p.GroupName })
            .ToListAsync();

        return items
            .Where(x => PermissionCatalog.IsTenantScope(x.Name))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.GroupName) ? "سایر" : x.GroupName)
            .OrderBy(g => g.Key)
            .Select(g => new PermissionGroupInfo
            {
                GroupName = g.Key,
                Permissions = g.OrderBy(x => x.Name)
                    .Select(x => new PermissionItem { Name = x.Name, DisplayName = x.DisplayName ?? x.Name })
                    .ToList()
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TenantRoleSummary>> GetRolesAsync(int tenantId)
    {
        return await _context.TenantRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .Select(r => new TenantRoleSummary
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                IsActive = r.IsActive,
                PermissionCount = r.Permissions.Count(),
                MemberCount = _context.UserTenantRoles.Count(a => a.TenantId == tenantId && a.TenantRoleId == r.Id)
            })
            .ToListAsync();
    }

    public async Task<TenantRoleDetails?> GetRoleAsync(int tenantId, int roleId)
    {
        var role = await _context.TenantRoles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId);

        if (role == null) return null;

        return new TenantRoleDetails
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            PermissionNames = role.Permissions.Select(p => p.PermissionName).OrderBy(n => n).ToList()
        };
    }

    public Task<int> CreateRoleAsync(int tenantId, TenantRoleRequest request)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var (name, description, permissions) = await ValidateRoleAsync(tenantId, request, excludeRoleId: null);

            var role = new TenantRole
            {
                TenantId = tenantId,
                Name = name,
                Description = description,
                IsActive = request.IsActive
            };

            foreach (var permission in permissions)
                role.Permissions.Add(new TenantRolePermission { TenantId = tenantId, PermissionName = permission });

            _context.TenantRoles.Add(role);
            await _context.SaveChangesAsync();

            return role.Id;
        });
    }

    public Task UpdateRoleAsync(int tenantId, int roleId, TenantRoleRequest request)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var role = await _context.TenantRoles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId)
                ?? throw new InvalidOperationException("نقش پیدا نشد.");

            var (name, description, permissions) = await ValidateRoleAsync(tenantId, request, excludeRoleId: roleId);

            role.Name = name;
            role.Description = description;
            role.IsActive = request.IsActive;

            // همگام‌سازی دسترسی‌ها: حذف نرمِ حذف‌شده‌ها، افزودنِ تازه‌ها
            var desired = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in role.Permissions.ToList())
            {
                if (!desired.Contains(existing.PermissionName))
                    _context.TenantRolePermissions.Remove(existing);
            }

            var current = role.Permissions.Select(p => p.PermissionName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var permission in desired.Where(p => !current.Contains(p)))
                role.Permissions.Add(new TenantRolePermission { TenantId = tenantId, PermissionName = permission });

            await _context.SaveChangesAsync();
        });
    }

    public Task DeleteRoleAsync(int tenantId, int roleId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var role = await _context.TenantRoles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId)
                ?? throw new InvalidOperationException("نقش پیدا نشد.");

            var assignments = await _context.UserTenantRoles
                .Where(a => a.TenantId == tenantId && a.TenantRoleId == roleId)
                .ToListAsync();

            // همه حذف نرم‌اند (Interceptor)؛ افرادِ دارای این نقش حذف نمی‌شوند، فقط نقش از آن‌ها برداشته می‌شود
            _context.UserTenantRoles.RemoveRange(assignments);
            _context.TenantRolePermissions.RemoveRange(role.Permissions);
            _context.TenantRoles.Remove(role);

            await _context.SaveChangesAsync();
        });
    }

    public Task<bool> IsPermissionInUseAsync(string permissionName)
    {
        // عمداً در همه‌ی شرکت‌ها (IgnoreQueryFilters) و فقط رکوردهای حذف‌نشده
        return _context.TenantRolePermissions
            .IgnoreQueryFilters()
            .AnyAsync(p => p.PermissionName == permissionName && !p.IsDeleted);
    }

    // ============================================
    // افراد
    // ============================================

    public async Task<IReadOnlyList<TenantMemberInfo>> GetMembersAsync(int tenantId)
    {
        var members = await QueryMembers(tenantId).ToListAsync();
        var rolesByUser = await GetRolesByUserAsync(tenantId);

        return members
            .Select(m => ToInfo(m, rolesByUser))
            .OrderByDescending(m => m.IsOwner)
            .ThenBy(m => m.DisplayName)
            .ToList();
    }

    public async Task<TenantMemberInfo?> GetMemberAsync(int tenantId, string userId)
    {
        var member = await QueryMembers(tenantId, userId).FirstOrDefaultAsync();

        if (member == null)
            return null;

        return ToInfo(member, await GetRolesByUserAsync(tenantId));
    }

    public Task UpdateMemberProfileAsync(int tenantId, string userId, MemberProfileRequest request)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var membership = await GetMembershipAsync(tenantId, userId);

            var firstName = RequireText(request.FirstName, "نام", MaxNameLength);
            var lastName = RequireText(request.LastName, "نام خانوادگی", MaxNameLength);
            var jobTitle = OptionalText(request.JobTitle, "عنوان شغلی", MaxNameLength);
            var phone = NormalizeInternalPhone(request.InternalPhone);

            if (phone != null)
            {
                var taken = await _context.UserTenants.AnyAsync(ut =>
                    ut.TenantId == tenantId && !ut.IsDeleted && ut.UserId != userId && ut.InternalPhone == phone);

                if (taken)
                    throw new InvalidOperationException($"تلفن داخلی {phone} قبلاً به شخص دیگری در این شرکت داده شده است.");
            }

            membership.FirstName = firstName;
            membership.LastName = lastName;
            membership.JobTitle = jobTitle;
            membership.InternalPhone = phone;

            await _context.SaveChangesAsync();
        });
    }

    public Task SetMemberRolesAsync(int tenantId, string userId, IReadOnlyCollection<int> roleIds)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var membership = await GetMembershipAsync(tenantId, userId);

            if (membership.IsOwner)
                throw new InvalidOperationException("مالک شرکت همه‌ی دسترسی‌ها را دارد و نیازی به نقش ندارد.");

            var wanted = roleIds.Distinct().ToList();

            var roles = await _context.TenantRoles
                .Where(r => r.TenantId == tenantId && wanted.Contains(r.Id))
                .ToListAsync();

            if (roles.Count != wanted.Count)
                throw new InvalidOperationException("یکی از نقش‌های انتخاب‌شده معتبر نیست.");

            var inactive = roles.FirstOrDefault(r => !r.IsActive);
            if (inactive != null)
                throw new InvalidOperationException($"نقش «{inactive.Name}» غیرفعال است.");

            var existing = await _context.UserTenantRoles
                .Where(a => a.TenantId == tenantId && a.UserId == userId)
                .ToListAsync();

            foreach (var assignment in existing.Where(a => !wanted.Contains(a.TenantRoleId)))
                _context.UserTenantRoles.Remove(assignment);

            var already = existing.Select(a => a.TenantRoleId).ToHashSet();
            foreach (var roleId in wanted.Where(id => !already.Contains(id)))
            {
                _context.UserTenantRoles.Add(new UserTenantRole
                {
                    TenantId = tenantId,
                    UserId = userId,
                    TenantRoleId = roleId
                });
            }

            await _context.SaveChangesAsync();
        });
    }

    public Task RemoveMemberAsync(int tenantId, string userId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var membership = await GetMembershipAsync(tenantId, userId);

            if (membership.IsOwner)
                throw new InvalidOperationException("مالک شرکت حذف نمی‌شود.");

            var assignments = await _context.UserTenantRoles
                .Where(a => a.TenantId == tenantId && a.UserId == userId)
                .ToListAsync();

            _context.UserTenantRoles.RemoveRange(assignments);

            // حذف نرم؛ حساب کاربری سر جایش می‌ماند (ممکن است عضو شرکت‌های دیگر باشد)
            _context.UserTenants.Remove(membership);

            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // Helpers
    // ============================================

    private sealed record MemberRow(
        string UserId, bool IsOwner, string? FirstName, string? LastName, string? JobTitle, string? InternalPhone,
        string FullName, string? Email, string? PhoneNumber, bool UserIsActive);

    private IQueryable<MemberRow> QueryMembers(int tenantId, string? userId = null)
    {
        var query = _context.UserTenants
            .AsNoTracking()
            .Where(ut => ut.TenantId == tenantId && !ut.IsDeleted);

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(ut => ut.UserId == userId);

        return query.Join(
            _context.Users,
            ut => ut.UserId,
            u => u.Id,
            (ut, u) => new MemberRow(
                ut.UserId,
                ut.IsOwner,
                ut.FirstName,
                ut.LastName,
                ut.JobTitle,
                ut.InternalPhone,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.IsActive));
    }

    private async Task<Dictionary<string, List<TenantMemberRole>>> GetRolesByUserAsync(int tenantId)
    {
        var rows = await (
                from assignment in _context.UserTenantRoles.AsNoTracking()
                join role in _context.TenantRoles on assignment.TenantRoleId equals role.Id
                where assignment.TenantId == tenantId
                orderby role.Name
                select new { assignment.UserId, RoleId = role.Id, RoleName = role.Name })
            .ToListAsync();

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new TenantMemberRole { RoleId = r.RoleId, RoleName = r.RoleName }).ToList());
    }

    private static TenantMemberInfo ToInfo(MemberRow m, Dictionary<string, List<TenantMemberRole>> rolesByUser) => new()
    {
        UserId = m.UserId,
        FullName = m.FullName,
        FirstName = m.FirstName,
        LastName = m.LastName,
        JobTitle = m.JobTitle,
        InternalPhone = m.InternalPhone,
        Email = m.Email,
        PhoneNumber = m.PhoneNumber,
        IsOwner = m.IsOwner,
        UserIsActive = m.UserIsActive,
        Roles = rolesByUser.TryGetValue(m.UserId, out var roles) ? roles : new()
    };

    /// <summary>فقط «عضوِ همین شرکت»؛ کاربرِ شرکت دیگر (حتی با شناسه‌ی درست) پیدا نمی‌شود.</summary>
    private async Task<UserTenant> GetMembershipAsync(int tenantId, string userId)
    {
        var membership = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.TenantId == tenantId && ut.UserId == userId && !ut.IsDeleted);

        return membership ?? throw new InvalidOperationException("این شخص عضو شرکت نیست.");
    }


    private async Task<(string Name, string? Description, List<string> Permissions)> ValidateRoleAsync(
        int tenantId, TenantRoleRequest request, int? excludeRoleId)
    {
        var name = RequireText(request.Name, "نام نقش", MaxNameLength);
        var description = OptionalText(request.Description, "توضیحات", MaxDescriptionLength);

        var lowered = name.ToLower();

        var rolesQuery = _context.TenantRoles
            .Where(r => r.TenantId == tenantId);

        if (excludeRoleId.HasValue)
        {
            var roleId = excludeRoleId.Value;
            rolesQuery = rolesQuery.Where(r => r.Id != roleId);
        }

        var duplicate = await rolesQuery.AnyAsync(r =>
            r.Name.ToLower() == lowered);

        if (duplicate)
            throw new InvalidOperationException("نقشی با این نام قبلاً وجود دارد.");

        var catalog = (await _context.Permissions
                .Where(p => p.IsActive)
                .Select(p => p.Name)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accepted = new List<string>();

        foreach (var raw in request.PermissionNames
                     .Select(p => p?.Trim())
                     .Where(p => !string.IsNullOrEmpty(p))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var permission = raw!;

            // دسترسی سطح حساب برای همه‌ی کاربران واردشده هست؛ دادنش به نقش بی‌معناست
            if (PermissionCatalog.IsAccountLevel(permission))
                continue;

            // جلوی ارتقای دسترسی: نقش شرکت هرگز دسترسی سراسری نمی‌گیرد
            if (PermissionCatalog.IsPlatform(permission))
                throw new InvalidOperationException($"دسترسی «{permission}» مخصوص مدیر سیستم است و به نقش شرکت داده نمی‌شود.");

            if (!catalog.Contains(permission))
                throw new InvalidOperationException($"دسترسی «{permission}» وجود ندارد یا غیرفعال است.");

            // نام را به شکلِ کاتالوگ ذخیره می‌کنیم (حروف بزرگ/کوچک یکدست)
            accepted.Add(catalog.First(c => string.Equals(c, permission, StringComparison.OrdinalIgnoreCase)));
        }

        return (name, description, accepted);
    }

    private static string RequireText(string? value, string title, int maxLength)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException($"{title} الزامی است.");

        if (text.Length > maxLength)
            throw new InvalidOperationException($"{title} حداکثر {maxLength} کاراکتر است.");

        return text;
    }

    private static string? OptionalText(string? value, string title, int maxLength)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text)) return null;

        if (text.Length > maxLength)
            throw new InvalidOperationException($"{title} حداکثر {maxLength} کاراکتر است.");

        return text;
    }

    /// <summary>تلفن داخلی: ارقام فارسی/عربی به لاتین؛ فقط رقم، حداکثر ۱۰ رقم؛ خالی = ندارد.</summary>
    private static string? NormalizeInternalPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var digits = PersianDate.NormalizeDigits(value).Replace(" ", "").Trim();

        if (digits.Length == 0) return null;

        if (digits.Length > MaxInternalPhoneLength || !digits.All(char.IsAsciiDigit))
            throw new InvalidOperationException($"تلفن داخلی فقط عدد و حداکثر {MaxInternalPhoneLength} رقم است.");

        return digits;
    }
}