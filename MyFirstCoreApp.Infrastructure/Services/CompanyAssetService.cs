using System.Data;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class CompanyAssetService : ICompanyAssetService
{
    private readonly ApplicationDbContext _db;

    public CompanyAssetService(ApplicationDbContext db) => _db = db;

    public async Task<List<CompanyAssetListItemDto>> GetListAsync(int tenantId)
    {
        var assets = await _db.CompanyAssets.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Product)
            .Include(x => x.Assignments.Where(a => a.ReturnedDate == null))
                .ThenInclude(a => a.UserTenant)
            .Include(x => x.Assignments.Where(a => a.ReturnedDate == null))
                .ThenInclude(a => a.Project)
            .OrderByDescending(x => x.Number)
            .ToListAsync();

        var names = await LoadUserNamesAsync(assets.SelectMany(x => x.Assignments).ToList());
        return assets.Select(x => MapListItem(x, names)).ToList();
    }

    public async Task<CompanyAssetDto?> GetByIdAsync(int id, int tenantId)
    {
        var asset = await _db.CompanyAssets.AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId)
            .Include(x => x.Product)
            .Include(x => x.Assignments.OrderByDescending(a => a.AssignedDate))
                .ThenInclude(a => a.UserTenant)
            .Include(x => x.Assignments.OrderByDescending(a => a.AssignedDate))
                .ThenInclude(a => a.Project)
            .FirstOrDefaultAsync();
        if (asset == null) return null;

        var names = await LoadUserNamesAsync(asset.Assignments.ToList());
        var current = asset.Assignments.FirstOrDefault(x => x.ReturnedDate == null);
        return new CompanyAssetDto
        {
            Id = asset.Id,
            Number = asset.Number,
            ProductId = asset.ProductId,
            ProductName = asset.Product.Name,
            ProductType = asset.Product.ProductType,
            SerialNumber = asset.SerialNumber,
            AcquisitionDate = asset.AcquisitionDate,
            AcquisitionCost = asset.AcquisitionCost,
            Description = asset.Description,
            Status = current == null ? asset.Status : CompanyAssetStatus.Assigned,
            AssignedUserName = current == null ? null : GetUserName(current.UserTenant, names),
            ProjectTitle = current?.Project?.Title,
            Assignments = asset.Assignments.OrderByDescending(x => x.AssignedDate).ThenByDescending(x => x.Id)
                .Select(x => new CompanyAssetAssignmentDto
                {
                    Id = x.Id,
                    UserTenantId = x.UserTenantId,
                    UserName = GetUserName(x.UserTenant, names),
                    ProjectId = x.ProjectId,
                    ProjectTitle = x.Project?.Title,
                    AssignedDate = x.AssignedDate,
                    ReturnedDate = x.ReturnedDate,
                    Notes = x.Notes,
                    ReturnNotes = x.ReturnNotes
                }).ToList()
        };
    }

    public async Task<int> CreateAsync(CompanyAssetRequest request, int tenantId)
    {
        if (request.AcquisitionCost < 0)
            throw new InvalidOperationException("بهای خرید نمی‌تواند منفی باشد.");

        var product = await _db.Products.FirstOrDefaultAsync(x =>
            x.Id == request.ProductId && x.TenantId == tenantId && x.IsActive);
        if (product == null || product.ProductType is not (ProductType.Tool or ProductType.FixedAsset))
            throw new InvalidOperationException("برای ثبت موردی، یک ابزار یا دارایی ثابت فعال از شرکت جاری انتخاب کنید.");

        var serialNumber = Clean(request.SerialNumber);
        if (serialNumber != null && await _db.CompanyAssets.AnyAsync(x =>
                x.TenantId == tenantId && x.SerialNumber == serialNumber))
            throw new InvalidOperationException("این شماره سریال قبلاً برای ابزار یا دارایی دیگری ثبت شده است.");

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var number = (await _db.CompanyAssets.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .MaxAsync(x => (int?)x.Number) ?? 0) + 1;
        var asset = new CompanyAsset
        {
            TenantId = tenantId,
            Number = number,
            ProductId = product.Id,
            SerialNumber = serialNumber,
            AcquisitionDate = request.AcquisitionDate,
            AcquisitionCost = request.AcquisitionCost,
            Description = Clean(request.Description)
        };
        _db.CompanyAssets.Add(asset);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return asset.Id;
    }

    public async Task<int> AssignAsync(CompanyAssetAssignmentRequest request, int tenantId)
    {
        if (request.AssignedDate == default)
            throw new InvalidOperationException("تاریخ تحویل الزامی است.");
        if (!request.UserTenantId.HasValue)
            throw new InvalidOperationException("برای تحویل ابزار، کارمند مسئول را انتخاب کنید.");

        var asset = await _db.CompanyAssets
            .Include(x => x.Assignments.Where(a => a.ReturnedDate == null))
            .FirstOrDefaultAsync(x => x.Id == request.CompanyAssetId && x.TenantId == tenantId);
        if (asset == null) throw new InvalidOperationException("ابزار یا دارایی موردنظر پیدا نشد.");
        if (asset.Status != CompanyAssetStatus.Available || asset.Assignments.Count > 0)
            throw new InvalidOperationException("این مورد در حال حاضر تحویل شخص یا پروژه دیگری است یا قابل تحویل نیست.");

        var membership = await _db.UserTenants.FirstOrDefaultAsync(x =>
            x.Id == request.UserTenantId.Value && x.TenantId == tenantId);
        if (membership == null || !await _db.Users.AnyAsync(x => x.Id == membership.UserId && x.IsActive))
            throw new InvalidOperationException("کارمند انتخاب‌شده عضو فعال شرکت جاری نیست.");

        var assignment = new CompanyAssetAssignment
        {
            TenantId = tenantId,
            CompanyAssetId = asset.Id,
            UserTenantId = request.UserTenantId,
            AssignedDate = request.AssignedDate,
            AssignedByUserId = Clean(request.AssignedByUserId),
            Notes = Clean(request.Notes)
        };
        _db.CompanyAssetAssignments.Add(assignment);
        await _db.SaveChangesAsync();
        return assignment.Id;
    }

    public async Task ReturnAsync(CompanyAssetReturnRequest request, int tenantId)
    {
        if (request.ReturnedDate == default)
            throw new InvalidOperationException("تاریخ برگشت الزامی است.");

        var assignment = await _db.CompanyAssetAssignments
            .Include(x => x.CompanyAsset)
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId && x.TenantId == tenantId);
        if (assignment == null || assignment.ReturnedDate.HasValue)
            throw new InvalidOperationException("تحویل فعال برای برگشت پیدا نشد.");
        if (request.ReturnedDate < assignment.AssignedDate)
            throw new InvalidOperationException("تاریخ برگشت نمی‌تواند قبل از تاریخ تحویل باشد.");

        assignment.ReturnedDate = request.ReturnedDate;
        assignment.ReturnedByUserId = Clean(request.ReturnedByUserId);
        assignment.ReturnNotes = Clean(request.ReturnNotes);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, int tenantId, string? deletedByUserId)
    {
        var asset = await _db.CompanyAssets
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
        if (asset == null)
            throw new InvalidOperationException("ابزار یا دارایی موردنظر پیدا نشد.");
        if (await _db.CompanyAssetAssignments.AnyAsync(x =>
                x.CompanyAssetId == asset.Id && x.TenantId == tenantId && x.ReturnedDate == null))
            throw new InvalidOperationException("این ابزار تحویل‌شده است؛ ابتدا برگشت آن را ثبت کنید.");

        asset.IsDeleted = true;
        asset.DeletedAt = DateTime.UtcNow;
        asset.DeletedBy = Clean(deletedByUserId);
        await _db.SaveChangesAsync();
    }

    private async Task<Dictionary<string, string>> LoadUserNamesAsync(IReadOnlyCollection<CompanyAssetAssignment> assignments)
    {
        var userIds = assignments.Where(x => x.UserTenant != null).Select(x => x.UserTenant!.UserId).Distinct().ToList();
        if (userIds.Count == 0) return new Dictionary<string, string>();
        return await _db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName }).ToDictionaryAsync(x => x.Id, x => x.FullName ?? "");
    }

    private static CompanyAssetListItemDto MapListItem(CompanyAsset asset, IReadOnlyDictionary<string, string> names)
    {
        var current = asset.Assignments.FirstOrDefault(x => x.ReturnedDate == null);
        return new CompanyAssetListItemDto
        {
            Id = asset.Id,
            Number = asset.Number,
            ProductName = asset.Product.Name,
            ProductType = asset.Product.ProductType,
            SerialNumber = asset.SerialNumber,
            AcquisitionDate = asset.AcquisitionDate,
            AcquisitionCost = asset.AcquisitionCost,
            Status = current == null ? asset.Status : CompanyAssetStatus.Assigned,
            AssignedUserName = current == null ? null : GetUserName(current.UserTenant, names),
            ProjectTitle = current?.Project?.Title
        };
    }

    private static string? GetUserName(UserTenant? membership, IReadOnlyDictionary<string, string> names)
    {
        if (membership == null) return null;
        var employeeName = string.Join(" ", new[] { membership.FirstName, membership.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        return employeeName.Length > 0 ? employeeName : names.GetValueOrDefault(membership.UserId);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
