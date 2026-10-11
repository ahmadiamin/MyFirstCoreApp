using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Application.Interfaces;

public interface ITransactionService
{
    /// <summary>
    /// تولید شماره سند بعدی برای Tenant جاری
    /// </summary>
    Task<int> GetNextNumberAsync(int tenantId);

    /// <summary>
    /// ثبت تراکنش جدید (با چک مجموع بدهکار/بستانکار)
    /// </summary>
    Task<Transaction> CreateAsync(Transaction transaction);

    /// <summary>
    /// ویرایش تراکنش
    /// </summary>
    Task UpdateAsync(Transaction transaction);

    /// <summary>
    /// حذف تراکنش (Soft Delete)
    /// </summary>
    Task DeleteAsync(int id, int tenantId);

    /// <summary>
    /// دریافت تراکنش با خطوط
    /// </summary>
    Task<Transaction?> GetByIdAsync(int id, int tenantId);
}