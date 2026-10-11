namespace MyFirstCoreApp.Application.Common.Models;

/// <summary>
/// مدل درخواست AG Grid (Infinite Row Model)
/// </summary>
public class AgGridRequest
{
    /// <summary>شماره رکورد شروع</summary>
    public int StartRow { get; set; }

    /// <summary>شماره رکورد پایان</summary>
    public int EndRow { get; set; }

    /// <summary>متن جستجو</summary>
    public string? Search { get; set; }

    /// <summary>نام ستون مرتب‌سازی</summary>
    public string? SortBy { get; set; }

    /// <summary>جهت مرتب‌سازی (asc / desc)</summary>
    public string? SortDir { get; set; }
}