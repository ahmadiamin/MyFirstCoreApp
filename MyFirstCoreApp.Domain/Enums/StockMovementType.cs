namespace MyFirstCoreApp.Domain.Enums;

/// <summary>
/// نوع گردش انبار
/// </summary>
public enum StockMovementType
{
    /// <summary>موجودی اول دوره یا افتتاحیه</summary>
    Opening = 1,

    /// <summary>رسید کالا به انبار</summary>
    Receipt = 2,

    /// <summary>حواله خروج کالا از انبار</summary>
    Issue = 3,

    /// <summary>انتقال کالا بین دو انبار</summary>
    Transfer = 4
}