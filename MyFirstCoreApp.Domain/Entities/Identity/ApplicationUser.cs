using Microsoft.AspNetCore.Identity;

namespace MyFirstCoreApp.Domain.Entities.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? PasswordChangedAt { get; set; }
        public ICollection<UserProfileImage> ProfileImages { get; set; } = new List<UserProfileImage>();
    }
}
