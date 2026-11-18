using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Projects.ViewModels.Project;

public class ProjectFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "عنوان پروژه الزامی است.")]
    [Display(Name = "عنوان پروژه")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "کد پروژه")]
    [MaxLength(50)]
    public string? Code { get; set; }

    [Display(Name = "طرف حساب")]
    public int? ContactId { get; set; }

    [Display(Name = "مدیر پروژه")]
    public int? ProjectManagerUserTenantId { get; set; }

    [Display(Name = "آدرس پروژه")]
    [MaxLength(1000)]
    public string? Address { get; set; }

    [Display(Name = "توضیحات")]
    [MaxLength(4000)]
    public string? Description { get; set; }

    [Display(Name = "تاریخ شروع")]
    public string? StartDate { get; set; }
    [Display(Name = "تاریخ پایان برنامه‌ریزی‌شده")]
    public string? PlannedEndDate { get; set; }

    public List<SelectListItem> ContactOptions { get; set; } = new();

    public List<SelectListItem> ProjectManagerOptions { get; set; } = new();
}