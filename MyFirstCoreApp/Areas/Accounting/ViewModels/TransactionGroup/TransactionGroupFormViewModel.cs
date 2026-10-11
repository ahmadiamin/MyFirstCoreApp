using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.TransactionGroup;

public class TransactionGroupFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام گروه الزامی است")]
    [Display(Name = "نام گروه")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "کد")]
    [MaxLength(50)]
    public string? Code { get; set; }

    [Display(Name = "گروه والد")]
    public int? ParentId { get; set; }

    [Display(Name = "ترتیب نمایش")]
    public int SortOrder { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "توضیحات")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    // برای dropdown
    public List<TransactionGroupListItem> ParentOptions { get; set; } = new();
}

public class TransactionGroupListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}