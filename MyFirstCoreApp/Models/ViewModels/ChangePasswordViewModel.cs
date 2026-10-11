using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Models.ViewModels
{
    public class ChangePasswordViewModel
    {
        [Display(Name = "رمز عبور فعلی")]
        [Required(ErrorMessage = "رمز عبور فعلی الزامی است.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;


        [Display(Name = "رمز عبور جدید")]
        [Required(ErrorMessage = "رمز عبور جدید الزامی است.")]
        [StringLength(
            100,
            MinimumLength = 6,
            ErrorMessage = "رمز عبور باید حداقل 6 کاراکتر باشد.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;


        [Display(Name = "تکرار رمز عبور جدید")]
        [Required(ErrorMessage = "تکرار رمز عبور الزامی است.")]
        [DataType(DataType.Password)]
        [Compare(
            nameof(NewPassword),
            ErrorMessage = "تکرار رمز عبور با رمز عبور جدید یکسان نیست.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}