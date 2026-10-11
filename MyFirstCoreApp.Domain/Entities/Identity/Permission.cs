using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Domain.Entities.Identity
{
    public class Permission
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? DisplayName { get; set; }

        [MaxLength(100)]
        public string? GroupName { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<RolePermission> RolePermissions { get; set; }
            = new List<RolePermission>();
    }
}
