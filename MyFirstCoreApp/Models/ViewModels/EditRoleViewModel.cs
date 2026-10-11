using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Models.ViewModels
{
    public class EditRoleViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام نقش الزامی است.")]
        [Display(Name = "نام نقش")]
        public string Name { get; set; } = string.Empty;

        public List<PermissionGroupViewModel> PermissionGroups { get; set; }
            = new();
    }

    public class PermissionGroupViewModel
    {
        public string GroupName { get; set; } = string.Empty;

        public List<PermissionItemViewModel> Permissions { get; set; }
            = new();
    }

    public class PermissionItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        public bool Selected { get; set; }
    }
}