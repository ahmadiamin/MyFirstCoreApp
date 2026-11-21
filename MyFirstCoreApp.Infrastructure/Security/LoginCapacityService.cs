using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Security;

public class LoginCapacityService : ILoginCapacityService
{
    private readonly ApplicationDbContext _context;

    public LoginCapacityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LoginCapacityInfo> GetAsync(string userId)
    {
        var info = new LoginCapacityInfo();

        if (string.IsNullOrEmpty(userId))
            return info;

        // عضویت‌های فعالِ کاربر در شرکت‌های فعال و حذف‌نشده
        var rows = await _context.UserTenants
            .AsNoTracking()
            .Where(ut => ut.UserId == userId && !ut.IsDeleted)
            .Join(
                _context.Tenants.Where(t => t.IsActive && !t.IsDeleted),
                ut => ut.TenantId,
                t => t.Id,
                (ut, t) => new { ut.IsOwner, t.Id, t.Name })
            .OrderBy(x => x.Name)
            .ToListAsync();

        foreach (var row in rows)
        {
            var tenant = new TenantRef(row.Id, row.Name);

            if (row.IsOwner)
                info.OwnerTenants.Add(tenant);
            else
                info.EmployeeTenants.Add(tenant);
        }

        return info;
    }
}
