using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IChequeService
{
    // ============================================
    // CRUD
    // ============================================

    Task<Cheque> CreateAsync(Cheque cheque);
    Task UpdateAsync(Cheque cheque);
    Task DeleteAsync(int id, int tenantId);

    // ============================================
    // گردش کار
    // ============================================

    /// <summary>به بانک داده شد</summary>
    Task MarkAsDepositedAsync(int chequeId, int tenantId, string? description);

    /// <summary>پاس شد</summary>
    Task MarkAsClearedAsync(int chequeId, int tenantId, DateTime? clearedDate, string? bankTrackingNumber);

    /// <summary>برگشت خورد</summary>
    Task MarkAsBouncedAsync(int chequeId, int tenantId, string? bounceReason, DateTime? bouncedDate);

    /// <summary>عودت داده شد (به مشتری برگشت)</summary>
    Task MarkAsReturnedAsync(int chequeId, int tenantId, string? description);

    /// <summary>مجدداً ارائه شد</summary>
    Task MarkAsRePresentedAsync(int chequeId, int tenantId, DateTime newDueDate, string? description);

    /// <summary>ابطال شد</summary>
    Task MarkAsCancelledAsync(int chequeId, int tenantId, string? reason);

    /// <summary>ارجاع به صاحب چک</summary>
    Task MarkAsReferredAsync(int chequeId, int tenantId, string? description);

    /// <summary>نقد شد</summary>
    Task MarkAsCashedAsync(int chequeId, int tenantId, DateTime? cashedDate, string? description);

    /// <summary>ثبت یادداشت</summary>
    Task AddNoteAsync(int chequeId, int tenantId, string note);

    /// <summary>
    /// برگشت از پاس (اشتباه پاس شده)
    /// </summary>
    Task UnclearAsync(int chequeId, int tenantId, string? reason);
    Task UncashAsync(int chequeId, int tenantId, string? reason);
}