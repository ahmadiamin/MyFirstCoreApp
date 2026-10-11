using System.ComponentModel.DataAnnotations;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.AccountCategory;

public class AccountCategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام دسته الزامی است")]
    [Display(Name = "نام دسته")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "کد")]
    [MaxLength(50)]
    public string? Code { get; set; }

    [Display(Name = "توضیحات")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "دسته والد")]
    public int? ParentId { get; set; }

    [Display(Name = "نوع پیش‌فرض حساب")]
    public AccountType DefaultAccountType { get; set; } = AccountType.Bank;

    [Display(Name = "ترتیب نمایش")]
    public int SortOrder { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    // برای dropdown
    public List<AccountCategoryListItem> ParentOptions { get; set; } = new();
}

public class AccountCategoryListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}