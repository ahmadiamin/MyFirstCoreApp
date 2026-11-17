namespace MyFirstCoreApp.Application.Security;

/// <summary>یک گره‌ی منوی «قابل نمایش» برای کاربر (خروجی فیلتر منو؛ مستقل از لایه‌ی وب).</summary>
public sealed class MenuNode
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Icon { get; set; }
    public List<MenuNode> Children { get; set; } = new();
}
