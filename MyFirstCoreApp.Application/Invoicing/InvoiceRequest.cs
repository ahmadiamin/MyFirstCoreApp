using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Invoicing;

/// <summary>
/// ورودی ثبت/ویرایش فاکتور. فقط فیلدهایی که کاربر اجازه‌ی تعیین‌شان را دارد.
/// (مبالغ، وضعیت، شماره، PaidAmount و ... هرگز از ورودی گرفته نمی‌شوند؛ سرویس محاسبه می‌کند.)
/// </summary>
public sealed class InvoiceRequest
{
    /// <summary>فقط برای ویرایش.</summary>
    public int Id { get; set; }

    public DateOnly Date { get; set; }
    public InvoiceType InvoiceType { get; set; }

    /// <summary>طرف حساب موجود. اگر خالی باشد باید NewContactName پر باشد.</summary>
    public int? ContactId { get; set; }

    /// <summary>نام طرف حساب جدید (ساخت سریع) — فقط وقتی ContactId خالی است.</summary>
    public string? NewContactName { get; set; }

    public string? Description { get; set; }

    public List<InvoiceItemRequest> Items { get; set; } = new();
}

public sealed class InvoiceItemRequest
{
    /// <summary>کالای موجود. اگر 0 باشد باید NewProductName پر باشد.</summary>
    public int ProductId { get; set; }

    /// <summary>انبار مقصد خرید یا مبدأ فروش؛ برای ورودی‌های قدیمی اختیاری است.</summary>
    public int? WarehouseId { get; set; }

    /// <summary>نام کالای جدید (ساخت سریع).</summary>
    public string? NewProductName { get; set; }

    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxRate { get; set; }
}
