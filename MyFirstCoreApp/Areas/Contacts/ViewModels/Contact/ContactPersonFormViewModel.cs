using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Contacts.ViewModels.Contact;

public class ContactPersonFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام شخص رابط را وارد کنید")]
    [Display(Name = "نام شخص")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "سمت")]
    [MaxLength(100)]
    public string? Position { get; set; }

    [Display(Name = "ایمیل")]
    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
    [MaxLength(200)]
    public string? Email { get; set; }

    [Display(Name = "یادداشت")]
    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Display(Name = "شخص اصلی")]
    public bool IsPrimary { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    public List<ContactPersonNumberFormViewModel> Numbers { get; set; } = new();
}
