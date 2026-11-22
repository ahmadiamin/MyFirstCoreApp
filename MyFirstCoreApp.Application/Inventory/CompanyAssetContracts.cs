using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Inventory;

public sealed class CompanyAssetRequest
{
    public int ProductId { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public string? Description { get; set; }
}

public sealed class CompanyAssetAssignmentRequest
{
    public int CompanyAssetId { get; set; }
    public int? UserTenantId { get; set; }
    public DateOnly AssignedDate { get; set; }
    public string? Notes { get; set; }
    public string? AssignedByUserId { get; set; }
}

public sealed class CompanyAssetReturnRequest
{
    public int AssignmentId { get; set; }
    public DateOnly ReturnedDate { get; set; }
    public string? ReturnNotes { get; set; }
    public string? ReturnedByUserId { get; set; }
}

public class CompanyAssetListItemDto
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string AssetTag => $"AST-{Number:000000}";
    public string ProductName { get; set; } = string.Empty;
    public ProductType ProductType { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public CompanyAssetStatus Status { get; set; }
    public string? AssignedUserName { get; set; }
    public string? ProjectTitle { get; set; }
}

public sealed class CompanyAssetDto : CompanyAssetListItemDto
{
    public int ProductId { get; set; }
    public string? Description { get; set; }
    public List<CompanyAssetAssignmentDto> Assignments { get; set; } = new();
}

public sealed class CompanyAssetAssignmentDto
{
    public int Id { get; set; }
    public int? UserTenantId { get; set; }
    public string? UserName { get; set; }
    public int? ProjectId { get; set; }
    public string? ProjectTitle { get; set; }
    public DateOnly AssignedDate { get; set; }
    public DateOnly? ReturnedDate { get; set; }
    public bool IsCurrent => !ReturnedDate.HasValue;
    public string? Notes { get; set; }
    public string? ReturnNotes { get; set; }
}

public interface ICompanyAssetService
{
    Task<List<CompanyAssetListItemDto>> GetListAsync(int tenantId);
    Task<CompanyAssetDto?> GetByIdAsync(int id, int tenantId);
    Task<int> CreateAsync(CompanyAssetRequest request, int tenantId);
    Task<int> AssignAsync(CompanyAssetAssignmentRequest request, int tenantId);
    Task ReturnAsync(CompanyAssetReturnRequest request, int tenantId);
    Task DeleteAsync(int id, int tenantId, string? deletedByUserId);
}
