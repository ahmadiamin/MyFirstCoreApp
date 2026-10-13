using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// قواعد این سرویس:
///  - هر عملیات چندمرحله‌ای (ثبت، قطعی، برگشت) داخل «یک تراکنش دیتابیس» است؛ یا همه انجام می‌شود یا هیچ.
///  - سند حسابداری فقط از مسیر ITransactionService ساخته/حذف می‌شود (اعتبارسنجی و شماره‌گذاری مرکزی).
///  - اعتبارسنجی هنگام ثبت، ویرایش و «دوباره هنگام قطعی شدن» اجرا می‌شود
///    (چون وضعیت فاکتورها بین ثبت پیش‌نویس و قطعی شدن ممکن است عوض شده باشد).
///  - باقی‌مانده‌ی فاکتور فقط از InvoiceBalance محاسبه می‌شود.
/// </summary>
public class PaymentService : IPaymentService
{
    private const int MaxNumberingAttempts = 3;

    private readonly ApplicationDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly ISystemAccountService _systemAccounts;
    private readonly ICurrentUser _currentUser;

    public PaymentService(
        ApplicationDbContext context,
        ITransactionService transactionService,
        ISystemAccountService systemAccounts,
        ICurrentUser currentUser)
    {
        _context = context;
        _transactionService = transactionService;
        _systemAccounts = systemAccounts;
        _currentUser = currentUser;
    }

    // ============================================
    // شماره‌گذاری
    // ============================================

    public async Task<int> GetNextNumberAsync(int tenantId)
    {
        var maxNumber = await _context.Payments
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .MaxAsync(p => (int?)p.Number) ?? 0;

        return maxNumber + 1;
    }

    // ============================================
    // ثبت
    // ============================================

    public async Task<Payment> CreateAsync(Payment payment)
    {
        await ValidateAsync(payment);

        payment.Status = PaymentStatus.Draft;
        payment.AllocatedAmount = payment.Allocations.Sum(a => a.Amount);
        foreach (var allocation in payment.Allocations)
            allocation.TenantId = payment.TenantId;

        var autoNumber = payment.Number == 0;

        await using var dbTransaction = await _context.Database.BeginTransactionAsync();

        // Number = 0 یعنی خودکار؛ در تداخل هم‌زمان با شماره‌ی بعدی دوباره تلاش می‌شود
        for (var attempt = 1; ; attempt++)
        {
            if (autoNumber)
                payment.Number = await GetNextNumberAsync(payment.TenantId);

            _context.Payments.Add(payment);

            try
            {
                await _context.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (autoNumber && attempt < MaxNumberingAttempts && ex.IsDuplicateKey())
            {
                DetachNew(payment);
            }
        }

        AddHistory(payment, null, PaymentStatus.Draft, "ایجاد پرداخت");
        await _context.SaveChangesAsync();

        await dbTransaction.CommitAsync();
        return payment;
    }

    // ============================================
    // ویرایش
    // ============================================

    public async Task UpdateAsync(Payment payment)
    {
        var existing = await LoadAsync(payment.Id, payment.TenantId);

        if (existing.Status != PaymentStatus.Draft)
            throw new InvalidOperationException("فقط پرداخت پیش‌نویس قابل ویرایش است.");

        await ValidateAsync(payment);

        // فقط فیلدهای مجاز (نه Number، Status، TransactionId و ...)
        existing.Date = payment.Date;
        existing.PaymentType = payment.PaymentType;
        existing.PaymentMethod = payment.PaymentMethod;
        existing.ContactId = payment.ContactId;
        existing.BankAccountId = payment.BankAccountId;
        existing.Amount = payment.Amount;
        existing.Reference = payment.Reference;
        existing.Description = payment.Description;
        existing.ImagePath = payment.ImagePath ?? existing.ImagePath;
        existing.AllocatedAmount = payment.Allocations.Sum(a => a.Amount);

        // جایگزینی تخصیص‌ها (شناسه‌های ورودی نادیده گرفته می‌شود)
        _context.PaymentAllocations.RemoveRange(existing.Allocations.ToList());

        foreach (var allocation in payment.Allocations)
        {
            existing.Allocations.Add(new PaymentAllocation
            {
                TenantId = existing.TenantId,
                InvoiceId = allocation.InvoiceId,
                Amount = allocation.Amount,
                Description = allocation.Description
            });
        }

        AddHistory(existing, PaymentStatus.Draft, PaymentStatus.Draft, "ویرایش پرداخت");
        await _context.SaveChangesAsync();
    }

    // ============================================
    // حذف (فقط پیش‌نویس)
    // ============================================

    public async Task DeleteAsync(int id, int tenantId)
    {
        var payment = await LoadAsync(id, tenantId, includeHistories: true);

        if (payment.Status != PaymentStatus.Draft)
            throw new InvalidOperationException("فقط پرداخت پیش‌نویس قابل حذف است.");

        // حذف واقعی را Interceptor به حذف نرم تبدیل می‌کند (DeletedAt/DeletedBy پر می‌شود)
        _context.PaymentAllocations.RemoveRange(payment.Allocations.ToList());
        _context.PaymentHistories.RemoveRange(payment.Histories.ToList());
        _context.Payments.Remove(payment);

        await _context.SaveChangesAsync();
    }

    // ============================================
    // قطعی
    // ============================================

    public async Task PostAsync(int paymentId, int tenantId)
    {
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();

        var payment = await LoadAsync(paymentId, tenantId);

        if (payment.Status == PaymentStatus.Posted)
            throw new InvalidOperationException("پرداخت قبلاً قطعی شده است.");

        if (payment.Status == PaymentStatus.Cancelled)
            throw new InvalidOperationException("پرداخت لغوشده قابل قطعی شدن نیست.");

        // وضعیت فاکتورها ممکن است بعد از ثبت پیش‌نویس عوض شده باشد
        await ValidateAsync(payment);

        // ۱. اعمال تخصیص‌ها روی فاکتورها
        var invoiceIds = payment.Allocations.Select(a => a.InvoiceId).ToList();
        var invoices = await _context.Invoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        foreach (var allocation in payment.Allocations)
            invoices[allocation.InvoiceId].PaidAmount += allocation.Amount;

        // ۲. سند خودکار (از مسیر مرکزی، با اعتبارسنجی و شماره‌گذاری)
        var transaction = await BuildTransactionAsync(payment, tenantId);
        await _transactionService.CreateAsync(transaction);

        payment.TransactionId = transaction.Id;
        payment.Status = PaymentStatus.Posted;

        AddHistory(payment, PaymentStatus.Draft, PaymentStatus.Posted, "قطعی شد");
        await _context.SaveChangesAsync();

        await dbTransaction.CommitAsync();
    }

    // ============================================
    // برگشت به پیش‌نویس
    // ============================================

    public async Task UnpostAsync(int paymentId, int tenantId)
    {
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();

        var payment = await LoadAsync(paymentId, tenantId);

        if (payment.Status != PaymentStatus.Posted)
            throw new InvalidOperationException("پرداخت قطعی نیست.");

        // ۱. برگشت تخصیص‌ها
        var invoiceIds = payment.Allocations.Select(a => a.InvoiceId).ToList();
        var invoices = await _context.Invoices
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        foreach (var allocation in payment.Allocations)
        {
            if (!invoices.TryGetValue(allocation.InvoiceId, out var invoice))
                continue;

            if (invoice.PaidAmount < allocation.Amount)
            {
                throw new InvalidOperationException(
                    $"مبلغ پرداخت‌شده‌ی فاکتور {invoice.Number} با این تخصیص همخوانی ندارد؛ برگشت ممکن نیست.");
            }

            invoice.PaidAmount -= allocation.Amount;
        }

        // ۲. حذف سند خودکار
        if (payment.TransactionId.HasValue)
        {
            var transactionId = payment.TransactionId.Value;

            var exists = await _context.Transactions.AnyAsync(t => t.Id == transactionId);
            if (exists)
                await _transactionService.DeleteAsync(transactionId, tenantId);

            payment.TransactionId = null;
        }

        payment.Status = PaymentStatus.Draft;

        AddHistory(payment, PaymentStatus.Posted, PaymentStatus.Draft, "برگشت به پیش‌نویس");
        await _context.SaveChangesAsync();

        await dbTransaction.CommitAsync();
    }

    // ============================================
    // اعتبارسنجی
    // ============================================

    private async Task ValidateAsync(Payment payment)
    {
        if (payment.Date == default)
            throw new InvalidOperationException("تاریخ پرداخت الزامی است.");

        if (payment.Amount <= 0)
            throw new InvalidOperationException("مبلغ باید بیشتر از صفر باشد.");

        if (!IsWhole(payment.Amount))
            throw new InvalidOperationException("مبلغ باید عدد صحیح (ریال) باشد.");

        if (!payment.ContactId.HasValue)
            throw new InvalidOperationException("طرف حساب الزامی است.");

        if (!await _context.Contacts.AnyAsync(c => c.Id == payment.ContactId.Value))
            throw new InvalidOperationException("طرف حساب معتبر نیست.");

        if (payment.BankAccountId.HasValue)
        {
            var isBankAccount = await _context.Accounts
                .AnyAsync(a => a.Id == payment.BankAccountId.Value && a.AccountType == AccountType.Bank);

            if (!isBankAccount)
                throw new InvalidOperationException("حساب بانکی انتخاب‌شده معتبر نیست.");
        }

        var allocations = payment.Allocations.ToList();

        if (allocations.Count == 0)
            throw new InvalidOperationException("حداقل یک تخصیص به فاکتور لازم است.");

        if (allocations.Any(a => a.Amount <= 0 || !IsWhole(a.Amount)))
            throw new InvalidOperationException("مبلغ هر تخصیص باید عدد صحیح و بیشتر از صفر باشد.");

        if (allocations.GroupBy(a => a.InvoiceId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("هر فاکتور فقط یک‌بار می‌تواند در یک پرداخت تخصیص داده شود.");

        var totalAllocated = allocations.Sum(a => a.Amount);
        if (totalAllocated > payment.Amount)
        {
            throw new InvalidOperationException(
                $"مجموع تخصیص ({totalAllocated:N0}) بیشتر از مبلغ پرداخت ({payment.Amount:N0}) است.");
        }

        var expectedType = payment.PaymentType == PaymentType.Received
            ? InvoiceType.Sale
            : InvoiceType.Purchase;

        var invoiceIds = allocations.Select(a => a.InvoiceId).ToList();
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync();

        foreach (var allocation in allocations)
        {
            var invoice = invoices.FirstOrDefault(i => i.Id == allocation.InvoiceId)
                ?? throw new InvalidOperationException("فاکتور پیدا نشد.");

            if (invoice.InvoiceType != expectedType)
                throw new InvalidOperationException($"فاکتور {invoice.Number} با نوع این پرداخت سازگار نیست.");

            if (invoice.ContactId != payment.ContactId)
                throw new InvalidOperationException($"فاکتور {invoice.Number} متعلق به این طرف حساب نیست.");

            if (invoice.Status != InvoiceStatus.Posted)
                throw new InvalidOperationException($"فاکتور {invoice.Number} قطعی نشده است.");

            var remaining = InvoiceBalance.Remaining(invoice);
            if (allocation.Amount > remaining)
            {
                throw new InvalidOperationException(
                    $"مبلغ تخصیص ({allocation.Amount:N0}) بیشتر از باقی‌مانده‌ی فاکتور {invoice.Number} ({remaining:N0}) است.");
            }
        }
    }

    private static bool IsWhole(decimal value) => decimal.Truncate(value) == value;

    // ============================================
    // سند حسابداری
    // ============================================

    private async Task<Transaction> BuildTransactionAsync(Payment payment, int tenantId)
    {
        var isReceived = payment.PaymentType == PaymentType.Received;

        var contactName = payment.ContactId.HasValue
            ? await _context.Contacts
                .Where(c => c.Id == payment.ContactId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync()
            : null;

        var title = isReceived ? "دریافت" : "پرداخت";
        var preposition = isReceived ? "از" : "به";

        var description = $"سند خودکار {title} شماره {payment.Number}";
        var lineDescription = $"{title} شماره {payment.Number}";

        if (!string.IsNullOrEmpty(contactName))
        {
            description += $" {preposition} {contactName}";
            lineDescription += $" {preposition} {contactName}";
        }

        if (!string.IsNullOrEmpty(payment.Reference))
            description += $" - مرجع: {payment.Reference}";

        var bankAccountId = payment.BankAccountId
            ?? (await _systemAccounts.GetOrCreateAsync(tenantId, "بانک", AccountType.Bank)).Id;

        var lines = new List<TransactionLine>();

        if (isReceived)
        {
            var receivable = await _systemAccounts.GetOrCreateAsync(tenantId, "دریافتنی", AccountType.Receivable);

            lines.Add(new TransactionLine
            {
                AccountId = bankAccountId,
                Debit = payment.Amount,
                Description = lineDescription
            });
            lines.Add(new TransactionLine
            {
                AccountId = receivable.Id,
                ContactId = payment.ContactId,
                Credit = payment.Amount,
                Description = lineDescription
            });
        }
        else
        {
            var payable = await _systemAccounts.GetOrCreateAsync(tenantId, "پرداختنی", AccountType.Payable);

            lines.Add(new TransactionLine
            {
                AccountId = payable.Id,
                ContactId = payment.ContactId,
                Debit = payment.Amount,
                Description = lineDescription
            });
            lines.Add(new TransactionLine
            {
                AccountId = bankAccountId,
                Credit = payment.Amount,
                Description = lineDescription
            });
        }

        return new Transaction
        {
            TenantId = tenantId,
            Number = 0, // شماره‌گذاری خودکار در TransactionService
            Date = payment.Date,
            Description = description,
            Reference = payment.Number.ToString(),
            IsPosted = true,
            ReferenceType = "Payment",
            ReferenceId = payment.Id,
            Lines = lines
        };
    }

    // ============================================
    // Helpers
    // ============================================

    private async Task<Payment> LoadAsync(int id, int tenantId, bool includeHistories = false)
    {
        var query = _context.Payments.Include(p => p.Allocations).AsQueryable();

        if (includeHistories)
            query = query.Include(p => p.Histories);

        var payment = await query.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        return payment ?? throw new InvalidOperationException("پرداخت پیدا نشد.");
    }

    private void AddHistory(Payment payment, PaymentStatus? from, PaymentStatus to, string description)
    {
        _context.PaymentHistories.Add(new PaymentHistory
        {
            TenantId = payment.TenantId,
            PaymentId = payment.Id,
            FromStatus = from,
            ToStatus = to,
            Description = description,
            Date = DateTime.UtcNow,
            UserId = _currentUser.UserId
        });
    }

    private void DetachNew(Payment payment)
    {
        foreach (var allocation in payment.Allocations)
            _context.Entry(allocation).State = EntityState.Detached;

        _context.Entry(payment).State = EntityState.Detached;
    }
}
