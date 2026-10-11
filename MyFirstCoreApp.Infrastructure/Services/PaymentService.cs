using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;

    public PaymentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetNextNumberAsync(int tenantId)
    {
        var maxNumber = await _context.Payments
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .MaxAsync(p => (int?)p.Number) ?? 0;

        return maxNumber + 1;
    }

    public async Task<Payment> CreateAsync(Payment payment)
    {
        if (payment.Amount <= 0)
            throw new InvalidOperationException("مبلغ باید بیشتر از صفر باشد.");

        if (payment.Allocations.Count == 0)
            throw new InvalidOperationException("حداقل یک تخصیص به فاکتور لازم است.");

        // چک مجموع تخصیص‌ها
        var totalAllocated = payment.Allocations.Sum(a => a.Amount);
        if (totalAllocated > payment.Amount)
            throw new InvalidOperationException($"مجموع تخصیص ({totalAllocated:N0}) بیشتر از مبلغ پرداخت ({payment.Amount:N0}) است.");

        // چک مبلغ تخصیص به هر فاکتور
        foreach (var alloc in payment.Allocations)
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == alloc.InvoiceId);

            if (invoice == null)
                throw new InvalidOperationException("فاکتور پیدا نشد.");

            var remaining = invoice.TotalAmount - invoice.PaidAmount;
            if (alloc.Amount > remaining)
                throw new InvalidOperationException($"مبلغ تخصیص ({alloc.Amount:N0}) بیشتر از باقی‌مانده فاکتور {invoice.Number} ({remaining:N0}) است.");
        }

        // شماره خودکار
        if (payment.Number == 0)
            payment.Number = await GetNextNumberAsync(payment.TenantId);

        payment.AllocatedAmount = totalAllocated;

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        await AddHistoryAsync(payment, null, PaymentStatus.Draft, "ایجاد پرداخت");
        await _context.SaveChangesAsync();

        return payment;
    }

    public async Task UpdateAsync(Payment payment)
    {
        var existing = await _context.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == payment.Id);

        if (existing == null)
            throw new InvalidOperationException("پرداخت پیدا نشد.");

        if (existing.Status != PaymentStatus.Draft)
            throw new InvalidOperationException("فقط پرداخت پیش‌نویس قابل ویرایش است.");

        // چک‌ها
        var totalAllocated = payment.Allocations.Sum(a => a.Amount);
        if (totalAllocated > payment.Amount)
            throw new InvalidOperationException($"مجموع تخصیص ({totalAllocated:N0}) بیشتر از مبلغ پرداخت ({payment.Amount:N0}) است.");

        // حذف تخصیص‌های قدیمی
        _context.PaymentAllocations.RemoveRange(existing.Allocations);

        // آپدیت
        existing.Date = payment.Date;
        existing.PaymentType = payment.PaymentType;
        existing.PaymentMethod = payment.PaymentMethod;
        existing.ContactId = payment.ContactId;
        existing.BankAccountId = payment.BankAccountId;
        existing.Amount = payment.Amount;
        existing.AllocatedAmount = totalAllocated;
        existing.Reference = payment.Reference;
        existing.Description = payment.Description;
        existing.ImagePath = payment.ImagePath;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.Allocations = payment.Allocations;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, int tenantId)
    {
        var entity = await _context.Payments
            .IgnoreQueryFilters()   // 🆕
            .Include(p => p.Allocations)
            .Include(p => p.Histories)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (entity == null)
            throw new InvalidOperationException("پرداخت پیدا نشد.");   // 🆕

        if (entity.Status != PaymentStatus.Draft)
            throw new InvalidOperationException("فقط پرداخت پیش‌نویس قابل حذف است.");

        // Soft Delete
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        // Soft Delete وابسته‌ها
        foreach (var a in entity.Allocations)
        {
            a.IsDeleted = true;
            a.DeletedAt = DateTime.UtcNow;
        }

        foreach (var h in entity.Histories)
        {
            h.IsDeleted = true;
            h.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task PostAsync(int paymentId, int tenantId)
    {
        var payment = await _context.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.TenantId == tenantId);

        if (payment == null)
            throw new InvalidOperationException("پرداخت پیدا نشد.");

        if (payment.Status == PaymentStatus.Posted)
            throw new InvalidOperationException("پرداخت قبلاً قطعی شده است.");

        // ۱. تخصیص به فاکتورها
        foreach (var alloc in payment.Allocations)
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == alloc.InvoiceId);

            if (invoice == null) continue;

            var remaining = invoice.TotalAmount - invoice.PaidAmount;
            if (alloc.Amount > remaining)
                throw new InvalidOperationException($"مبلغ تخصیص به فاکتور {invoice.Number} بیشتر از باقی‌مانده است.");

            invoice.PaidAmount += alloc.Amount;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        // ۲. سند خودکار
        var transaction = await CreateTransactionFromPaymentAsync(payment, tenantId);
        payment.TransactionId = transaction.Id;
        payment.Status = PaymentStatus.Posted;
        payment.UpdatedAt = DateTime.UtcNow;

        await AddHistoryAsync(payment, PaymentStatus.Draft, PaymentStatus.Posted, "قطعی شد");

        await _context.SaveChangesAsync();
    }

    public async Task UnpostAsync(int paymentId, int tenantId)
    {
        var payment = await _context.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.TenantId == tenantId);

        if (payment == null) throw new InvalidOperationException("پرداخت پیدا نشد.");

        if (payment.Status != PaymentStatus.Posted)
            throw new InvalidOperationException("پرداخت قطعی نیست.");

        // ۱. برگشت تخصیص‌ها
        foreach (var alloc in payment.Allocations)
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == alloc.InvoiceId);

            if (invoice != null)
            {
                invoice.PaidAmount -= alloc.Amount;
                invoice.UpdatedAt = DateTime.UtcNow;
            }
        }

        // ۲. حذف سند
        if (payment.TransactionId.HasValue)
        {
            var transaction = await _context.Transactions
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.Id == payment.TransactionId.Value);

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
            payment.TransactionId = null;
        }

        payment.Status = PaymentStatus.Draft;
        payment.UpdatedAt = DateTime.UtcNow;

        await AddHistoryAsync(payment, PaymentStatus.Posted, PaymentStatus.Draft, "برگشت به پیش‌نویس");

        await _context.SaveChangesAsync();
    }

    // ============================================
    // Helpers
    // ============================================

    private async Task AddHistoryAsync(Payment payment, PaymentStatus? from, PaymentStatus to, string? description)
    {
        _context.PaymentHistories.Add(new PaymentHistory
        {
            TenantId = payment.TenantId,
            PaymentId = payment.Id,
            FromStatus = from,
            ToStatus = to,
            Description = description,
            Date = DateTime.UtcNow
        });
    }

    private async Task<Transaction> CreateTransactionFromPaymentAsync(Payment payment, int tenantId)
    {
        var lines = new List<TransactionLine>();
        var paymentTypeName = payment.PaymentType == PaymentType.Received ? "دریافت" : "پرداخت";

        // 🆕 نام طرف حساب
        var contactName = "";
        if (payment.ContactId.HasValue)
        {
            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == payment.ContactId.Value);
            if (contact != null) contactName = contact.Name;
        }

        // 🆕 شرح کلی سند
        var transactionDescription = payment.PaymentType == PaymentType.Received
            ? $"سند خودکار دریافت شماره {payment.Number}"
            : $"سند خودکار پرداخت شماره {payment.Number}";

        if (!string.IsNullOrEmpty(contactName))
        {
            transactionDescription += payment.PaymentType == PaymentType.Received
                ? $" از {contactName}"
                : $" به {contactName}";
        }

        if (!string.IsNullOrEmpty(payment.Reference))
        {
            transactionDescription += $" - مرجع: {payment.Reference}";
        }

        // 🆕 شرح خطوط
        var lineDescription = $"{(payment.PaymentType == PaymentType.Received ? "دریافت" : "پرداخت")} شماره {payment.Number}";
        if (!string.IsNullOrEmpty(contactName))
        {
            lineDescription += payment.PaymentType == PaymentType.Received
                ? $" از {contactName}"
                : $" به {contactName}";
        }

        if (payment.PaymentType == PaymentType.Received)
        {
            var bankAccount = payment.BankAccountId.HasValue
                ? await _context.Accounts.FindAsync(payment.BankAccountId.Value)
                : await GetOrCreateAccountAsync(tenantId, "بانک", AccountType.Bank);

            var receivableAccount = await GetOrCreateAccountAsync(tenantId, "دریافتنی", AccountType.Receivable);

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = bankAccount!.Id,
                Debit = payment.Amount,
                Credit = 0,
                Description = lineDescription
            });

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = receivableAccount.Id,
                ContactId = payment.ContactId,
                Debit = 0,
                Credit = payment.Amount,
                Description = lineDescription
            });
        }
        else // Paid
        {
            var bankAccount = payment.BankAccountId.HasValue
                ? await _context.Accounts.FindAsync(payment.BankAccountId.Value)
                : await GetOrCreateAccountAsync(tenantId, "بانک", AccountType.Bank);

            var payableAccount = await GetOrCreateAccountAsync(tenantId, "پرداختنی", AccountType.Payable);

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = payableAccount.Id,
                ContactId = payment.ContactId,
                Debit = payment.Amount,
                Credit = 0,
                Description = lineDescription
            });

            lines.Add(new TransactionLine
            {
                TenantId = tenantId,
                AccountId = bankAccount!.Id,
                Debit = 0,
                Credit = payment.Amount,
                Description = lineDescription
            });
        }

        // 🆕 شماره سند
        var nextNumber = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (int?)t.Number) ?? 0;
        nextNumber++;

        var transaction = new Transaction
        {
            TenantId = tenantId,
            Number = nextNumber,
            Date = payment.Date,
            Description = transactionDescription,   
            Reference = payment.Number.ToString(),
            IsPosted = true,
            Lines = lines,
            TotalAmount = payment.Amount,
            ReferenceType = "Payment",       
            ReferenceId = payment.Id,         
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
}