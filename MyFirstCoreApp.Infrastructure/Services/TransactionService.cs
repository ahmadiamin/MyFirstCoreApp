using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private const int MaxNumberingAttempts = 3;

    private readonly ApplicationDbContext _context;

    public TransactionService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================
    // شماره‌گذاری
    // ============================================

    public async Task<int> GetNextNumberAsync(int tenantId)
    {
        // رکوردهای حذف‌شده‌ی نرم هم شمرده می‌شوند تا شماره‌ی سند هرگز تکرار نشود.
        var maxNumber = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (int?)t.Number) ?? 0;

        return maxNumber + 1;
    }

    // ============================================
    // ثبت
    // ============================================

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        var totalDebit = Validate(transaction);
        await ValidateReferencesAsync(transaction.Lines);

        transaction.TotalAmount = totalDebit;
        NormalizeLines(transaction.Lines, transaction.TenantId);

        // Number = 0 یعنی خودکار. اگر دو کاربر هم‌زمان ثبت کنند، ایندکس یکتای
        // (TenantId, Number) یکی را رد می‌کند و اینجا با شماره‌ی بعدی دوباره تلاش می‌شود.
        var autoNumber = transaction.Number == 0;

        for (var attempt = 1; ; attempt++)
        {
            if (autoNumber)
                transaction.Number = await GetNextNumberAsync(transaction.TenantId);

            _context.Transactions.Add(transaction);

            try
            {
                await _context.SaveChangesAsync();
                return transaction;
            }
            catch (DbUpdateException ex) when (autoNumber && attempt < MaxNumberingAttempts && IsDuplicateKey(ex))
            {
                DetachNew(transaction);
            }
        }
    }

    // ============================================
    // ویرایش
    // ============================================

    public async Task UpdateAsync(Transaction transaction)
    {
        var existing = await LoadWithLinesAsync(transaction.Id, transaction.TenantId);

        EnsureManual(existing);
        if (existing.IsPosted)
            throw new InvalidOperationException("سند قطعی قابل ویرایش نیست.");

        var totalDebit = Validate(transaction);
        await ValidateReferencesAsync(transaction.Lines);

        // فقط فیلدهای مجاز کپی می‌شوند (نه Number، IsPosted، ReferenceType و ...)
        existing.Date = transaction.Date;
        existing.Description = transaction.Description;
        existing.TransactionGroupId = transaction.TransactionGroupId;
        existing.Reference = transaction.Reference;
        existing.AttachmentPath = transaction.AttachmentPath;
        existing.TotalAmount = totalDebit;

        // جایگزینی خطوط: قدیمی‌ها حذف نرم (Interceptor)، جدیدها اضافه
        _context.TransactionLines.RemoveRange(existing.Lines.ToList());

        var newLines = transaction.Lines
            .Select(l => new TransactionLine
            {
                AccountId = l.AccountId,
                ContactId = l.ContactId,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description
            })
            .ToList();

        NormalizeLines(newLines, existing.TenantId);

        foreach (var line in newLines)
            existing.Lines.Add(line);

        await _context.SaveChangesAsync();
    }

    // ============================================
    // قطعی / پیش‌نویس
    // ============================================

    public async Task PostAsync(int id, int tenantId)
    {
        var entity = await LoadWithLinesAsync(id, tenantId);

        EnsureManual(entity);
        if (entity.IsPosted)
            throw new InvalidOperationException("سند قبلاً قطعی شده است.");

        Validate(entity);

        entity.IsPosted = true;
        await _context.SaveChangesAsync();
    }

    public async Task UnpostAsync(int id, int tenantId)
    {
        var entity = await LoadWithLinesAsync(id, tenantId);

        EnsureManual(entity);
        if (!entity.IsPosted)
            throw new InvalidOperationException("این سند پیش‌نویس است.");

        entity.IsPosted = false;
        await _context.SaveChangesAsync();
    }

    // ============================================
    // حذف
    // ============================================

    public async Task DeleteAsync(int id, int tenantId)
    {
        var entity = await LoadWithLinesAsync(id, tenantId);
        await RemoveAsync(entity);
    }

    public async Task DeleteManualAsync(int id, int tenantId)
    {
        var entity = await LoadWithLinesAsync(id, tenantId);

        EnsureManual(entity);
        if (entity.IsPosted)
            throw new InvalidOperationException("سند قطعی قابل حذف نیست. ابتدا آن را به پیش‌نویس برگردانید.");

        await RemoveAsync(entity);
    }

    public async Task<Transaction?> GetByIdAsync(int id, int tenantId)
    {
        return await _context.Transactions
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
    }

    // ============================================
    // Helpers
    // ============================================

    private async Task<Transaction> LoadWithLinesAsync(int id, int tenantId)
    {
        var entity = await _context.Transactions
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        return entity ?? throw new InvalidOperationException("سند پیدا نشد.");
    }

    private static void EnsureManual(Transaction transaction)
    {
        if (!string.IsNullOrEmpty(transaction.ReferenceType))
            throw new InvalidOperationException(
                "این سند خودکار است و از صفحه‌ی مربوطه (فاکتور، پرداخت یا چک) باید مدیریت شود.");
    }

    private async Task RemoveAsync(Transaction entity)
    {
        // حذف واقعی را Interceptor به حذف نرم (UPDATE) تبدیل می‌کند و DeletedBy/DeletedAt را پر می‌کند.
        _context.TransactionLines.RemoveRange(entity.Lines.ToList());
        _context.Transactions.Remove(entity);

        await _context.SaveChangesAsync();
    }

    /// <summary>قواعد ساختاری سند. مجموع بدهکار را برمی‌گرداند.</summary>
    private static decimal Validate(Transaction transaction)
    {
        if (transaction.Date == default)
            throw new InvalidOperationException("تاریخ سند الزامی است.");

        if (transaction.Lines.Count < 2)
            throw new InvalidOperationException("تراکنش باید حداقل ۲ خط داشته باشد.");

        foreach (var line in transaction.Lines)
        {
            if (line.AccountId <= 0)
                throw new InvalidOperationException("برای هر خط باید حساب انتخاب شود.");

            if (line.Debit < 0 || line.Credit < 0)
                throw new InvalidOperationException("مبلغ نمی‌تواند منفی باشد.");

            if (line.Debit > 0 && line.Credit > 0)
                throw new InvalidOperationException("هر خط فقط می‌تواند بدهکار یا بستانکار باشد، نه هر دو.");

            if (line.Debit == 0 && line.Credit == 0)
                throw new InvalidOperationException("هر خط باید مبلغ داشته باشد.");

            // ریال اعشار ندارد؛ دیتابیس decimal(18,0) است و اعشار را بی‌صدا گرد می‌کند.
            if (decimal.Truncate(line.Debit) != line.Debit || decimal.Truncate(line.Credit) != line.Credit)
                throw new InvalidOperationException("مبلغ باید عدد صحیح (ریال) باشد.");
        }

        var totalDebit = transaction.Lines.Sum(l => l.Debit);
        var totalCredit = transaction.Lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException(
                $"مجموع بدهکار ({totalDebit:N0}) با مجموع بستانکار ({totalCredit:N0}) برابر نیست.");
        }

        return totalDebit;
    }

    /// <summary>
    /// حساب‌ها و طرف‌حساب‌های خطوط باید در شرکت جاری وجود داشته باشند
    /// (Query Filter شرکت اعمال می‌شود، پس شناسه‌ی شرکت دیگر پیدا نمی‌شود).
    /// </summary>
    private async Task ValidateReferencesAsync(IEnumerable<TransactionLine> lines)
    {
        var lineList = lines.ToList();

        var accountIds = lineList.Select(l => l.AccountId).Distinct().ToList();
        var existingAccounts = await _context.Accounts.CountAsync(a => accountIds.Contains(a.Id));
        if (existingAccounts != accountIds.Count)
            throw new InvalidOperationException("حساب انتخاب‌شده معتبر نیست.");

        var contactIds = lineList
            .Where(l => l.ContactId.HasValue)
            .Select(l => l.ContactId!.Value)
            .Distinct()
            .ToList();

        if (contactIds.Count > 0)
        {
            var existingContacts = await _context.Contacts.CountAsync(c => contactIds.Contains(c.Id));
            if (existingContacts != contactIds.Count)
                throw new InvalidOperationException("طرف‌حساب انتخاب‌شده معتبر نیست.");
        }
    }

    private static void NormalizeLines(IEnumerable<TransactionLine> lines, int tenantId)
    {
        var lineNumber = 1;
        foreach (var line in lines)
        {
            line.LineNumber = lineNumber++;
            line.TenantId = tenantId;
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };

    private void DetachNew(Transaction transaction)
    {
        foreach (var line in transaction.Lines)
            _context.Entry(line).State = EntityState.Detached;

        _context.Entry(transaction).State = EntityState.Detached;
    }
}
