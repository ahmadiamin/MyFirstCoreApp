using System.ComponentModel.DataAnnotations;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Contacts.ViewModels.Contact;

public class ContactPersonNumberFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "نوع شماره")]
    public ContactPersonNumberType NumberType { get; set; } = ContactPersonNumberType.Mobile;

    [Display(Name = "عنوان / اپراتور")]
    [MaxLength(50)]
    public string? Label { get; set; }

    [Required(ErrorMessage = "شماره تماس را وارد کنید")]
    [Display(Name = "شماره تماس")]
    [MaxLength(30)]
    public string Number { get; set; } = string.Empty;

    [Display(Name = "داخلی")]
    [MaxLength(10)]
    public string? Extension { get; set; }

    [Display(Name = "شماره اصلی")]
    public bool IsPrimary { get; set; }
}
