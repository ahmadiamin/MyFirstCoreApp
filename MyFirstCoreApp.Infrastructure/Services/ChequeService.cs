using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Domain.Entities.Accounting;
namespace MyFirstCoreApp.Infrastructure.Services;

public class ChequeService : IChequeService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public ChequeService(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // ============================================
    // CRUD
    // ============================================

    public async Task<Cheque> CreateAsync(Cheque cheque)
    {
        if (string.IsNullOrWhiteSpace(cheque.ChequeNumber))
            throw new InvalidOperationException("شماره چک الزامی است.");

        if (cheque.Amount <= 0)
            throw new InvalidOperationException("مبلغ چک باید بیشتر از صفر باشد.");

        _context.Cheques.Add(cheque);
        await _context.SaveChangesAsync();

        AddHistory(cheque, null, ChequeStatus.InProgress,
            ChequeHistoryAction.Created, "ایجاد چک");

        await _context.SaveChangesAsync();

        return cheque;
    }

    public async Task UpdateAsync(Cheque cheque)
    {
        var existing = await _context.Cheques
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cheque.Id);

        if (existing == null)
            throw new InvalidOperationException("چک پیدا نشد.");

        // 🆕 چک پاس/برگشت خورده قابل ویرایش نیست
        if (existing.Status == ChequeStatus.Cleared ||
            existing.Status == ChequeStatus.Bounced ||
            existing.Status == ChequeStatus.Cancelled)
        {
            throw new InvalidOperationException("چک در وضعیت فعلی قابل ویرایش نیست.");
        }

        cheque.UpdatedAt = DateTime.UtcNow;
        _context.Cheques.Update(cheque);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, int tenantId)
    {
        var entity = await _context.Cheques
            .IgnoreQueryFilters()
            .Include(c => c.Histories)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);

        if (entity == null)
            throw new InvalidOperationException("چک پیدا نشد.");

        if (entity.Status != ChequeStatus.InProgress)
            throw new InvalidOperationException("فقط چک در جریان قابل حذف است.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        foreach (var h in entity.Histories)
        {
            h.IsDeleted = true;
            h.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    // ============================================
    // گردش کار
    // ============================================

    public async Task MarkAsDepositedAsync(int chequeId, int tenantId, string? description)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status != ChequeStatus.InProgress)
            throw new InvalidOperationException("فقط چک در جریان قابل ارائه به بانک است.");

        AddHistory(cheque, cheque.Status, cheque.Status,
            ChequeHistoryAction.Deposited, description ?? "به بانک داده شد");

        await _context.SaveChangesAsync();
    }

    public async Task MarkAsClearedAsync(int chequeId, int tenantId, DateTime? clearedDate, string? bankTrackingNumber)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status == ChequeStatus.Cleared)
            throw new InvalidOperationException("چک قبلاً پاس شده است.");

        if (cheque.Status == ChequeStatus.Cancelled)
            throw new InvalidOperationException("چک ابطال شده قابل پاس نیست.");

        var oldStatus = cheque.Status;

        cheque.Status = ChequeStatus.Cleared;
        cheque.ClearedDate = clearedDate ?? DateTime.UtcNow;
        cheque.BankTrackingNumber = bankTrackingNumber;
        cheque.UpdatedAt = DateTime.UtcNow;

        // 🆕 ساخت سند حسابداری
        var transaction = await CreateClearedTransactionAsync(cheque, tenantId);
        cheque.TransactionId = transaction.Id;

        AddHistory(cheque, oldStatus, ChequeStatus.Cleared,
            ChequeHistoryAction.Cleared, "چک پاس شد");

        await _context.SaveChangesAsync();
    }

    public async Task MarkAsBouncedAsync(int chequeId, int tenantId, string? bounceReason, DateTime? bouncedDate)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status == ChequeStatus.Cleared)
            throw new InvalidOperationException("چک پاس شده قابل برگشت نیست.");

        var oldStatus = cheque.Status;

        cheque.Status = ChequeStatus.Bounced;
        cheque.BouncedDate = bouncedDate ?? DateTime.UtcNow;
        cheque.BounceReason = bounceReason;
        cheque.UpdatedAt = DateTime.UtcNow;

        AddHistory(cheque, oldStatus, ChequeStatus.Bounced,
            ChequeHistoryAction.Bounced, bounceReason ?? "چک برگشت خورد");

        await _context.SaveChangesAsync();
    }

    public async Task MarkAsReturnedAsync(int chequeId, int tenantId, string? description)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status != ChequeStatus.Bounced)
            throw new InvalidOperationException("فقط چک برگشتی قابل عودت است.");

        var oldStatus = cheque.Status;

        cheque.Status = ChequeStatus.Returned;
        cheque.UpdatedAt = DateTime.UtcNow;

        AddHistory(cheque, oldStatus, ChequeStatus.Returned,
            ChequeHistoryAction.Returned, description ?? "به مشتری عودت داده شد");

        await _context.SaveChangesAsync();
    }

    public async Task MarkAsRePresentedAsync(int chequeId, int tenantId, DateTime newDueDate, string? description)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status != ChequeStatus.Bounced && cheque.Status != ChequeStatus.Returned)
            throw new InvalidOperationException("فقط چک برگشتی/عودت‌شده قابل ارائه مجدد است.");

        var oldStatus = cheque.Status;

        cheque.Status = ChequeStatus.RePresented;
        cheque.DueDate = newDueDate;
        cheque.BouncedDate = null;
        cheque.UpdatedAt = DateTime.UtcNow;

        // 🆕 تاریخ شمسی
        var pc = new System.Globalization.PersianCalendar();
        var shamsiDate = $"{pc.GetYear(newDueDate):0000}/{pc.GetMonth(newDueDate):00}/{pc.GetDayOfMonth(newDueDate):00}";

        AddHistory(cheque, oldStatus, ChequeStatus.RePresented,
            ChequeHistoryAction.RePresented,
            $"ارائه مجدد با سررسید {shamsiDate} - {description}");

        await _context.SaveChangesAsync();
    }

    public async Task MarkAsCancelledAsync(int chequeId, int tenantId, string? reason)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status == ChequeStatus.Cleared)
            throw new InvalidOperationException("چک پاس شده قابل ابطال نیست.");

        var oldStatus = cheque.Status;

        cheque.Status = ChequeStatus.Cancelled;
        cheque.UpdatedAt = DateTime.UtcNow;

        AddHistory(cheque, oldStatus, ChequeStatus.Cancelled,
            ChequeHistoryAction.Cancelled, reason ?? "ابطال شد");

        await _context.SaveChangesAsync();
    }

    public async Task MarkAsReferredAsync(int chequeId, int tenantId, string? description)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status != ChequeStatus.Bounced)
            throw new InvalidOperationException("فقط چک برگشتی قابل ارجاع است.");

        var oldStatus = cheque.Status;

        cheque.Status = ChequeStatus.Referred;
        cheque.UpdatedAt = DateTime.UtcNow;

        AddHistory(cheque, oldStatus, ChequeStatus.Referred,
            ChequeHistoryAction.Referred, description ?? "ارجاع به صاحب چک");

        await _context.SaveChangesAsync();
    }

    public async Task MarkAsCashedAsync(int chequeId, int tenantId, DateTime? cashedDate, string? description)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status != ChequeStatus.InProgress)
            throw new InvalidOperationException("فقط چک در جریان قابل نقد شدن است.");

        var oldStatus = cheque.Status;

        cheque.Status = ChequeStatus.Cashed;
        cheque.ClearedDate = cashedDate ?? DateTime.UtcNow;
        cheque.UpdatedAt = DateTime.UtcNow;

        AddHistory(cheque, oldStatus, ChequeStatus.Cashed,
            ChequeHistoryAction.Cashed, description ?? "چک نقد شد");

        await _context.SaveChangesAsync();
    }

    public async Task AddNoteAsync(int chequeId, int tenantId, string note)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        AddHistory(cheque, cheque.Status, cheque.Status,
            ChequeHistoryAction.Note, note);

        await _context.SaveChangesAsync();
    }

    // ============================================
    // Helpers
    // ============================================

    private async Task<Cheque> GetChequeAsync(int chequeId, int tenantId)
    {
        var cheque = await _context.Cheques
            .FirstOrDefaultAsync(c => c.Id == chequeId && c.TenantId == tenantId);

        if (cheque == null)
            throw new InvalidOperationException("چک پیدا نشد.");

        return cheque;
    }

    private void AddHistory(Cheque cheque, ChequeStatus? from, ChequeStatus to,
        ChequeHistoryAction action, string? description)
    {
        var history = new ChequeHistory
        {
            TenantId = cheque.TenantId,
            ChequeId = cheque.Id,
            FromStatus = from,
            ToStatus = to,
            Action = action,
            Description = description,
            Date = DateTime.UtcNow
        };

        _context.ChequeHistories.Add(history);
    }

    /// <summary>
    /// ساخت سند حسابداری وقتی چک پاس می‌شه
    /// </summary>
    private async Task<Transaction> CreateClearedTransactionAsync(Cheque cheque, int tenantId)
    {
        var lines = new List<TransactionLine>();
        // 🆕 نام طرف حساب
        var contactName = "";
        if (cheque.ContactId.HasValue)
        {
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == cheque.ContactId.Value);
            if (contact != null) contactName = contact.Name;
        }

        var chequeTypeName = cheque.Direction == ChequeDirection.Received ? "دریافتنی" : "پرداختنی";

        // 🆕 شرح کلی
        var transactionDescription = $"سند خودکار پاس چک {chequeTypeName} شماره {cheque.ChequeNumber}";
        if (!string.IsNullOrEmpty(contactName))
        {
            transactionDescription += cheque.Direction == ChequeDirection.Received
                ? $" - از {contactName}"
                : $" - به {contactName}";
        }

        if (!string.IsNullOrEmpty(cheque.BankName))
        {
            transactionDescription += $" - بانک {cheque.BankName}";
        }

        // 🆕 شرح خطوط
        var lineDescription = $"پاس چک {chequeTypeName} شماره {cheque.ChequeNumber}";
        if (!string.IsNullOrEmpty(contactName))
        {
            lineDescription += cheque.Direction == ChequeDirection.Received
                ? $" - از {contactName}"
                : $" - به {contactName}";
        }

        if (cheque.Direction == ChequeDirection.Received)
        {
            // دریافتنی پاس شد: بانک (بدهکار) / چک‌های دریافتنی (بستانکار)
            var bankAccount = cheque.BankAccountId.HasValue
                ? await _context.Accounts.FindAsync(cheque.BankAccountId.Value)
                : await GetOrCreateAccountAsync(tenantId, "بانک", AccountType.Bank);

            var chequeAccount = await GetOrCreateAccountAsync(tenantId, "چک‌های دریافتنی", AccountType.Contra);

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = bankAccount!.Id,
                Debit = cheque.Amount,
                Credit = 0,
                Description = lineDescription
            });

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = chequeAccount.Id,
                ContactId = cheque.ContactId,
                Debit = 0,
                Credit = cheque.Amount,
                Description = lineDescription,
            });
        }
        else // Issued
        {
            // پرداختنی پاس شد: چک‌های پرداختنی (بدهکار) / بانک (بستانکار)
            var bankAccount = cheque.BankAccountId.HasValue
                ? await _context.Accounts.FindAsync(cheque.BankAccountId.Value)
                : await GetOrCreateAccountAsync(tenantId, "بانک", AccountType.Bank);

            var chequeAccount = await GetOrCreateAccountAsync(tenantId, "چک‌های پرداختنی", AccountType.Contra);

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = chequeAccount.Id,
                ContactId = cheque.ContactId,
                Debit = cheque.Amount,
                Credit = 0,
                Description = lineDescription,
            });

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = bankAccount!.Id,
                Debit = 0,
                Credit = cheque.Amount,
                Description = lineDescription
            });
        }
        // 🆕 شماره سند خودکار
        var nextNumber = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (int?)t.Number) ?? 0;
        nextNumber++;

        var transaction = new Transaction
        {
            TenantId = tenantId,
            Number = nextNumber,   
            Date = DateOnly.FromDateTime(DateTime.Now),
            Description = transactionDescription,
            Reference = cheque.ChequeNumber,
            IsPosted = true,
            Lines = lines,
            TotalAmount = cheque.Amount,
            ReferenceType = "Cheque",        
            ReferenceId = cheque.Id,          
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    private async Task<Account> GetOrCreateAccountAsync(int tenantId, string name, AccountType type)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Name == name && a.AccountType == type);

        if (account != null) return account;

        var category = await _context.AccountCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.DefaultAccountType == type);

        if (category == null)
        {
            category = new AccountCategory
            {
                TenantId = tenantId,
                Name = $"دسته‌بندی {name}",
                DefaultAccountType = type,
                IsActive = true
            };
            _context.AccountCategories.Add(category);
            await _context.SaveChangesAsync();
        }

        account = new Account
        {
            TenantId = tenantId,
            Name = name,
            AccountType = type,
            AccountCategoryId = category.Id,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return account;
    }

    public async Task UnclearAsync(int chequeId, int tenantId, string? reason)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status != ChequeStatus.Cleared)
            throw new InvalidOperationException("فقط چک پاس‌شده قابل برگشت است.");

        var oldStatus = cheque.Status;

        // 🆕 حذف سند حسابداری
        if (cheque.TransactionId.HasValue)
        {
            var transaction = await _context.Transactions
                .IgnoreQueryFilters()
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.Id == cheque.TransactionId.Value);

            if (transaction != null)
            {
                transaction.IsDeleted = true;
                transaction.DeletedAt = DateTime.UtcNow;

                foreach (var line in transaction.Lines)
                {
                    line.IsDeleted = true;
                    line.DeletedAt = DateTime.UtcNow;
                }
            }

            cheque.TransactionId = null;
        }

        // 🆕 برگشت وضعیت
        cheque.Status = ChequeStatus.InProgress;
        cheque.ClearedDate = null;
        cheque.BankTrackingNumber = null;
        cheque.UpdatedAt = DateTime.UtcNow;

        // 🆕 ثبت تاریخچه
        AddHistory(cheque, oldStatus, ChequeStatus.InProgress,
            ChequeHistoryAction.StatusChanged,
            reason ?? "برگشت از پاس (اشتباه پاس شده)");

        await _context.SaveChangesAsync();
    }

    public async Task UncashAsync(int chequeId, int tenantId, string? reason)
    {
        var cheque = await GetChequeAsync(chequeId, tenantId);

        if (cheque.Status != ChequeStatus.Cashed)
            throw new InvalidOperationException("فقط چک نقد‌شده قابل برگشت است.");

        var oldStatus = cheque.Status;

        // حذف سند حسابداری
        if (cheque.TransactionId.HasValue)
        {
            var transaction = await _context.Transactions
                .IgnoreQueryFilters()
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.Id == cheque.TransactionId.Value);

            if (transaction != null)
            {
                transaction.IsDeleted = true;
                transaction.DeletedAt = DateTime.UtcNow;

                foreach (var line in transaction.Lines)
                {
                    line.IsDeleted = true;
                    line.DeletedAt = DateTime.UtcNow;
                }
            }

            cheque.TransactionId = null;
        }

        cheque.Status = ChequeStatus.InProgress;
        cheque.ClearedDate = null;
        cheque.UpdatedAt = DateTime.UtcNow;

        AddHistory(cheque, oldStatus, ChequeStatus.InProgress,
            ChequeHistoryAction.StatusChanged,
            reason ?? "برگشت از نقد (اشتباه نقد شده)");

        await _context.SaveChangesAsync();
    }
}