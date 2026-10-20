namespace MyFirstCoreApp.Domain.Enums;

/// <summary>
/// وضعیت سال مالی. در سال «بسته» ثبت، ویرایش، قطعی/پیش‌نویس کردن و حذف سند ممکن نیست.
/// </summary>
public enum FiscalYearStatus
{
    Open = 1,
    Closed = 2
}
