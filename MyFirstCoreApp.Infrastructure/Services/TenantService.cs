using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly ApplicationDbContext _context;
    private readonly ILoginCapacityAccessor? _capacity;

    // capacity اختیاری است: بدون آن (مثلاً در تست‌ها) رفتار همان رفتار قبلی است
    public TenantService(ApplicationDbContext context, ILoginCapacityAccessor? capacity = null)
    {
        _context = context;
        _capacity = capacity;
    }

    public async Task<Tenant?> GetByIdAsync(int id)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// شرکت‌های فعالِ کاربر. اگر کاربر «هر دو سمت» (مدیر و کارمند) را دارد و در این نشست یکی را انتخاب کرده،
    /// فقط شرکت‌های همان سمت برمی‌گردد (صفحه‌ی انتخاب شرکت بدون تغییر همین را نشان می‌دهد).
    /// </summary>
    public async Task<List<Tenant>> GetUserTenantsAsync(string userId)
    {
        var relations = await GetUserTenantRelationsAsync(userId);
        return relations.Select(r => r.Tenant).ToList();
    }

    public async Task<Tenant> CreateAsync(Tenant tenant, string ownerUserId)
    {
        // 1) ساخت Tenant
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        // 2) اضافه کردن مالک به‌عنوان عضو
        var userTenant = new UserTenant
        {
            UserId = ownerUserId,
            TenantId = tenant.Id,
            IsOwner = true,
            IsDefault = true,
            JoinedAt = DateTime.UtcNow
        };

        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();

        // کسی که با سمت «کارمند» وارد شده و شرکت تازه می‌سازد، مدیرِ همین شرکت است؛
        // شرکت تازه نباید به‌خاطر فیلتر سمت از لیستش غایب باشد.
        if (_capacity?.GetChosen(ownerUserId) == LoginCapacity.Employee)
            _capacity.Set(ownerUserId, LoginCapacity.Owner);

        return tenant;
    }

    public async Task<bool> IsUserMemberAsync(int tenantId, string userId)
    {
        return await _context.UserTenants
            .AnyAsync(ut => ut.TenantId == tenantId
                         && ut.UserId == userId
                         && !ut.IsDeleted);
    }

    public async Task<bool> IsUserOwnerAsync(int tenantId, string userId)
    {
        return await _context.UserTenants
            .AnyAsync(ut => ut.TenantId == tenantId
                         && ut.UserId == userId
                         && ut.IsOwner
                         && !ut.IsDeleted);
    }

    public async Task AddUserToTenantAsync(int tenantId, string userId, bool isOwner = false, bool isDefault = false)
    {
        var exists = await _context.UserTenants
            .AnyAsync(ut => ut.TenantId == tenantId
                         && ut.UserId == userId
                         && !ut.IsDeleted);

        if (exists) return;

        var userTenant = new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            IsOwner = isOwner,
            IsDefault = isDefault,
            JoinedAt = DateTime.UtcNow
        };

        _context.UserTenants.Add(userTenant);
        await _context.SaveChangesAsync();
    }

    public async Task SetDefaultTenantAsync(string userId, int tenantId)
    {
        var userTenants = await _context.UserTenants
            .Where(ut => ut.UserId == userId && !ut.IsDeleted)
            .ToListAsync();

        foreach (var ut in userTenants)
        {
            ut.IsDefault = (ut.TenantId == tenantId);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<UserTenant>> GetUserTenantRelationsAsync(string userId)
    {
        var relations = await _context.UserTenants
            .Where(ut => ut.UserId == userId && !ut.IsDeleted)
            .Include(ut => ut.Tenant)
            .Where(ut => !ut.Tenant.IsDeleted && ut.Tenant.IsActive)
            .ToListAsync();

        return _capacity?.GetChosen(userId) switch
        {
            LoginCapacity.Owner => relations.Where(r => r.IsOwner).ToList(),
            LoginCapacity.Employee => relations.Where(r => !r.IsOwner).ToList(),
            _ => relations
        };
    }

    public async Task UpdateAsync(Tenant tenant)
    {
        _context.Tenants.Update(tenant);
        await _context.SaveChangesAsync();
    }
}