namespace MyFirstCoreApp.Models.ViewModels
{
    public class UserListViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; }

        public List<string> Roles { get; set; } = new();
        public bool EmailConfirmed { get;   set; }
    }
}