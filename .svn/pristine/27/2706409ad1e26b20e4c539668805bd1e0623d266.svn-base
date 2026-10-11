using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _context;

    public TransactionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetNextNumberAsync(int tenantId)
    {
        // 🆕 نادیده گرفتن Soft Delete (چون Index یکتاست)
        var maxNumber = await _context.Transactions
            .IgnoreQueryFilters()   // 🆕 این خط کلیدیه
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (int?)t.Number) ?? 0;

        return maxNumber + 1;
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        // چک خطوط
        if (transaction.Lines.Count < 2)
        {
            throw new InvalidOperationException("تراکنش باید حداقل ۲ خط داشته باشد.");
        }

        // چک مجموع
        var totalDebit = transaction.Lines.Sum(l => l.Debit);
        var totalCredit = transaction.Lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException(
                $"مجموع بدهکار ({totalDebit:N0}) با مجموع بستانکار ({totalCredit:N0}) برابر نیست.");
        }

        if (totalDebit == 0)
        {
            throw new InvalidOperationException("مبلغ تراکنش نمی‌تواند صفر باشد.");
        }

        // شماره سند خودکار
        if (transaction.Number == 0)
        {
            transaction.Number = await GetNextNumberAsync(transaction.TenantId);
        }

        // TotalAmount
        transaction.TotalAmount = totalDebit;

        // 🆕 **IsPosted رو دست نزن!**
        // transaction.IsPosted = false;  ← حذف کن

        // LineNumber و TenantId خودکار
        var lineNumber = 1;
        foreach (var line in transaction.Lines)
        {
            line.LineNumber = lineNumber++;
            line.TenantId = transaction.TenantId;
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task UpdateAsync(Transaction transaction)
    {
        // چک خطوط
        if (transaction.Lines.Count < 2)
        {
            throw new InvalidOperationException("تراکنش باید حداقل ۲ خط داشته باشد.");
        }

        // چک مجموع
        var totalDebit = transaction.Lines.Sum(l => l.Debit);
        var totalCredit = transaction.Lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException(
                $"مجموع بدهکار ({totalDebit:N0}) با مجموع بستانکار ({totalCredit:N0}) برابر نیست.");
        }

        transaction.TotalAmount = totalDebit;
        transaction.UpdatedAt = DateTime.UtcNow;

        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, int tenantId)
    {
        var entity = await _context.Transactions
            .IgnoreQueryFilters()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (entity == null)
            throw new InvalidOperationException("سند پیدا نشد.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        foreach (var line in entity.Lines)
        {
            line.IsDeleted = true;
            line.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Transaction?> GetByIdAsync(int id, int tenantId)
    {
        return await _context.Transactions
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
    }
}