using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Models.ViewModels
{
    public class MenuEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان منو الزامی است.")]
        [Display(Name = "عنوان")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "آدرس")]
        public string? Url { get; set; }

        [Display(Name = "آیکون")]
        public string? Icon { get; set; }

        [Display(Name = "دسترسی")]
        public string? Permission { get; set; }

        [Display(Name = "منوی والد")]
        public int? ParentId { get; set; }

        [Display(Name = "ترتیب نمایش")]
        public int SortOrder { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        public List<MenuParentViewModel> Parents { get; set; } = new();

        public List<MenuPermissionViewModel> Permissions { get; set; } = new();
    }

    public class MenuParentViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    public class MenuPermissionViewModel
    {
        public string Name { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public string? GroupName { get; set; }
    }
}