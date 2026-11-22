using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.CompanyAssets;

public sealed class CompanyAssetFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "قلم ابزار یا دارایی را انتخاب کنید.")]
    public int ProductId { get; set; }
    [MaxLength(100)]
    public string? SerialNumber { get; set; }
    public string? AcquisitionDateText { get; set; }
    public string? AcquisitionCostText { get; set; }
    [MaxLength(2000)]
    public string? Description { get; set; }
    public List<SelectListItem> ProductOptions { get; set; } = new();
}

public sealed class CompanyAssetAssignmentFormViewModel
{
    public int CompanyAssetId { get; set; }
    public int? UserTenantId { get; set; }
    [Required(ErrorMessage = "تاریخ تحویل الزامی است.")]
    public string AssignedDateText { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Notes { get; set; }
    public List<SelectListItem> UserOptions { get; set; } = new();
}

public sealed class CompanyAssetReturnFormViewModel
{
    public int AssignmentId { get; set; }
    [Required(ErrorMessage = "تاریخ برگشت الزامی است.")]
    public string ReturnedDateText { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? ReturnNotes { get; set; }
}
