namespace MyFirstCoreApp.Domain.Enums;

/// <summary>
/// نوع طرف حساب
/// </summary>
[Flags]
public enum ContactType
{
    None = 0,

    /// <summary>مشتری</summary>
    Customer = 1,

    /// <summary>فروشنده</summary>
    Supplier = 2,

    /// <summary>هم مشتری هم فروشنده</summary>
    Both = Customer | Supplier
}