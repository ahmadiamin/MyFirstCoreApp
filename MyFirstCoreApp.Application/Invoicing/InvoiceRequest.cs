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
    
    /// <summary>
    /// تاریخ سررسید
    /// </summary>
    public DateOnly? DueDate { get; set; }
    public InvoiceType InvoiceType { get; set; }

    /// <summary>شماره فاکتور فروشنده؛ فقط برای فاکتور خرید.</summary>
    public string? SupplierInvoiceNumber { get; set; }

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
    /// <summary>
    /// آیا این قلم خرید، دارایی ثابت شرکت محسوب می‌شود؟
    /// فقط در فاکتور خرید کاربرد دارد.
    /// </summary>
    public bool IsCapitalAsset { get; set; }
    public bool IsService { get; set; }
}

/// <summary>
/// درخواست ایجاد برگشت از فروش یا برگشت از خرید.
/// قیمت، تخفیف و مالیات از فاکتور اصلی استخراج می‌شوند.
/// </summary>
public sealed class InvoiceReturnRequest
{
    public int OriginalInvoiceId { get; set; }

    public DateOnly Date { get; set; }

    public string? Description { get; set; }

    public List<InvoiceReturnItemRequest> Items { get; set; } = new();
}

/// <summary>
/// ردیف برگشت؛ فقط شناسه ردیف اصلی و مقدار برگشتی از کاربر دریافت می‌شود.
/// </summary>
public sealed class InvoiceReturnItemRequest
{
    public int OriginalInvoiceItemId { get; set; }

    public decimal Quantity { get; set; }
}

/// <summary>
/// اطلاعات یک ردیف فاکتور اصلی و مقدار قابل برگشت آن.
/// </summary>
public sealed class InvoiceReturnableItemDto
{
    public int OriginalInvoiceItemId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? WarehouseId { get; set; }

    public decimal OriginalQuantity { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal TaxRate { get; set; }

    public bool TrackInventory { get; set; }
}

