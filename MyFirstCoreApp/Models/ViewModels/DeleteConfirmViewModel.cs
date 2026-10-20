namespace MyFirstCoreApp.Models.ViewModels;

public class DeleteConfirmViewModel
{
    public string Title { get; set; } = "حذف";
    public string Message { get; set; } = "آیا از حذف این مورد مطمئن هستید؟";

    public string Area { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public int Id { get; set; }

    public List<DeleteConfirmItem> Items { get; set; } = new();

    // 🆕 بازگشت به مسیر دلخواه
    public string? ReturnUrl { get; set; }
}
public class DeleteConfirmItem
{
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }
}