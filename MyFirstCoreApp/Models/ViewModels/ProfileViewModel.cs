using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Models.ViewModels
{
    public class ProfileViewModel
    {
        [Display(Name = "نام و نام خانوادگی")]
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است.")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل")]
        [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست.")]
        public string? Email { get; set; }

        [Display(Name = "شماره موبایل")]
        [Phone(ErrorMessage = "شماره موبایل صحیح نیست.")]
        public string? PhoneNumber { get; set; }
        [Display(Name = "آخرین تغییر رمز عبور")]
        public DateTime? PasswordChangedAt { get; set; }

        public string? UserName { get; set; }
        public int? PrimaryImageId { get; set; }

    }
}