using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Projects.ViewModels.ProjectMaterials;

public sealed class ProjectMaterialFormViewModel
{
    public int ProjectId { get; set; }
    public string Date { get; set; } = string.Empty;
    public int? SourceWarehouseId { get; set; }
    public string? Description { get; set; }
    public List<ProjectMaterialItemFormViewModel> Items { get; set; } = [new()];
    public List<SelectListItem> Warehouses { get; set; } = [];
    public List<SelectListItem> Consumables { get; set; } = [];
}

public sealed class ProjectMaterialItemFormViewModel
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; } = 1;
}

public sealed class ProjectMaterialListViewModel
{
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public bool CanCreate { get; set; }
    public bool CanPost { get; set; }
    public bool CanDelete { get; set; }
    public List<ProjectMaterialMovementViewModel> Movements { get; set; } = [];
}

public sealed class ProjectMaterialMovementViewModel
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsReversal { get; set; }
    public bool HasPostedReversal { get; set; }
    public bool CanDelete { get; set; }
    public bool CanReverse { get; set; }
    public string Items { get; set; } = string.Empty;
    public string? Description { get; set; }
}
