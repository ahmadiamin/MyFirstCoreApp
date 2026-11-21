using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Projects.ViewModels.Project;

public class ProjectMemberFormViewModel
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    [Required(ErrorMessage = "انتخاب عضو الزامی است.")]
    [Display(Name = "عضو پروژه")]
    public int UserTenantId { get; set; }

    public string UserName { get; set; } = string.Empty;

    [Display(Name = "نقش در پروژه")]
    [MaxLength(100)]
    public string? RoleTitle { get; set; }

    [Display(Name = "توضیحات")]
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public List<SelectListItem> UserOptions { get; set; } = new();
}

public class ProjectStageFormViewModel
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    [Required(ErrorMessage = "عنوان مرحله الزامی است.")]
    [Display(Name = "عنوان مرحله")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "شرح مرحله")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "ترتیب نمایش")]
    [Range(1, 1000)]
    public int SortOrder { get; set; } = 1;

    [Display(Name = "مرحله الزامی است")]
    public bool IsRequired { get; set; } = true;

    [Display(Name = "مسئول مرحله")]
    public int? AssignedUserTenantId { get; set; }

    [Display(Name = "شروع برنامه‌ریزی‌شده")]
    public string? PlannedStartDate { get; set; }

    [Display(Name = "پایان برنامه‌ریزی‌شده")]
    public string? PlannedEndDate { get; set; }

    [Display(Name = "توضیحات تکمیلی")]
    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<SelectListItem> UserOptions { get; set; } = new();
}

public class ProjectStageStatusFormViewModel
{
    public int ProjectId { get; set; }

    public int ProjectStageId { get; set; }

    [Display(Name = "وضعیت جدید")]
    public ProjectStageStatus NewStatus { get; set; }

    [Display(Name = "شرح تغییر وضعیت")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "علت انتظار")]
    [MaxLength(1000)]
    public string? WaitingReason { get; set; }

    [Display(Name = "تاریخ پیگیری بعدی")]
    public string? NextFollowUpDate { get; set; }
}

public class ProjectStatusFormViewModel
{
    public int ProjectId { get; set; }

    [Display(Name = "وضعیت جدید پروژه")]
    public ProjectStatus NewStatus { get; set; }

    [Display(Name = "علت توقف")]
    [MaxLength(1000)]
    public string? HoldReason { get; set; }

    [Display(Name = "علت لغو")]
    [MaxLength(1000)]
    public string? CancellationReason { get; set; }
}