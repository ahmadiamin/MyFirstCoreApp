using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Security;

public class MenuTreeService : IMenuTreeService
{
    private readonly ApplicationDbContext _context;
    private readonly IPermissionEvaluator _evaluator;

    public MenuTreeService(ApplicationDbContext context, IPermissionEvaluator evaluator)
    {
        _context = context;
        _evaluator = evaluator;
    }

    public async Task<List<MenuNode>> GetVisibleTreeAsync(PermissionSubject subject)
    {
        if (!subject.IsAuthenticated || string.IsNullOrEmpty(subject.UserId))
            return new List<MenuNode>();

        var menus = await _context.Menus
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        // یک بار برای همه‌ی منوها (قبلاً برای هر منو یک پرس‌وجوی جدا بود)
        var allowed = await _evaluator.GetAllowedAsync(subject, menus.Select(m => m.Permission));

        var visible = menus
            .Where(m => string.IsNullOrWhiteSpace(m.Permission) || allowed.Contains(m.Permission!))
            .ToList();

        var byParent = visible.ToLookup(m => m.ParentId);

        List<MenuNode> Build(int? parentId)
            => byParent[parentId]
                .OrderBy(m => m.SortOrder)
                .Select(m => new MenuNode
                {
                    Id = m.Id,
                    Title = m.Title,
                    Url = m.Url,
                    Icon = m.Icon,
                    Children = Build(m.Id)
                })
                // گروهِ بدون آدرس و بدون فرزندِ قابل‌نمایش، نمایش داده نمی‌شود
                .Where(n => !string.IsNullOrWhiteSpace(n.Url) || n.Children.Count > 0)
                .ToList();

        return Build(null);
    }
}
