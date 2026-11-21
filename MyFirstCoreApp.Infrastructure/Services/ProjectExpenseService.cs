using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Projects;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Attachments;
using MyFirstCoreApp.Domain.Entities.Projects;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class ProjectExpenseService : IProjectExpenseService
{
    private readonly ApplicationDbContext _db;
    private readonly ITransactionService _transactions;
    private readonly ISystemAccountService _systemAccounts;

    public ProjectExpenseService(ApplicationDbContext db, ITransactionService transactions, ISystemAccountService systemAccounts)
    { _db = db; _transactions = transactions; _systemAccounts = systemAccounts; }

    public async Task<List<ProjectExpenseDto>> GetListAsync(int tenantId, int? projectId = null)
    {
        var q = _db.ProjectExpenses.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (projectId.HasValue) q = q.Where(x => x.ProjectId == projectId.Value);
        return await q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Number).Select(x => new ProjectExpenseDto
        {
            Id = x.Id, Number = x.Number, Date = x.Date, ProjectId = x.ProjectId,
            ProjectTitle = x.Project == null ? null : x.Project.Title,
            ContactId = x.ContactId, ContactName = x.Contact == null ? null : x.Contact.Name,
            ExpenseAccountId = x.ExpenseAccountId, ExpenseAccountName = x.ExpenseAccount.Name,
            Amount = x.Amount, PaidAmount = x.Payments.Sum(p => (decimal?)p.Amount) ?? 0,
            Description = x.Description, Reference = x.Reference
        }).ToListAsync();
    }

    public async Task<ProjectExpenseDto?> GetByIdAsync(int id, int tenantId)
    {
        var e = await _db.ProjectExpenses.AsNoTracking().Include(x => x.Project).Include(x => x.Contact)
            .Include(x => x.ExpenseAccount).Include(x => x.Payments).ThenInclude(x => x.Account)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
        if (e == null) return null;
        return new ProjectExpenseDto
        {
            Id = e.Id, Number = e.Number, Date = e.Date, ProjectId = e.ProjectId, ProjectTitle = e.Project?.Title,
            ContactId = e.ContactId, ContactName = e.Contact?.Name, ExpenseAccountId = e.ExpenseAccountId,
            ExpenseAccountName = e.ExpenseAccount.Name, Amount = e.Amount, PaidAmount = e.Payments.Sum(p => p.Amount),
            Description = e.Description, Reference = e.Reference,
            Payments = e.Payments.OrderByDescending(p => p.Date).Select(p => new ProjectExpensePaymentDto
            { Id = p.Id, Date = p.Date, Amount = p.Amount, AccountName = p.Account.Name, PaymentMethod = p.PaymentMethod, Reference = p.Reference, Description = p.Description }).ToList()
        };
    }

    public async Task<int> CreateAsync(ProjectExpenseRequest r, int tenantId)
    {
        ValidateMoney(r.Amount, r.Date, r.Description);
        Account? selectedExpenseAccount = null;
        if (r.ExpenseAccountId > 0)
        {
            selectedExpenseAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == r.ExpenseAccountId && a.TenantId == tenantId && a.IsActive && a.AccountType == AccountType.Expense);
            if (selectedExpenseAccount == null || IsInvoiceSystemExpense(selectedExpenseAccount.Name))
                throw new InvalidOperationException("حساب انتخاب‌شده برای ثبت هزینه عمومی معتبر نیست.");
        }
        else if (r.ExpenseAccountId != 0)
            throw new InvalidOperationException("حساب هزینه انتخاب‌شده معتبر نیست.");
        if (r.ProjectId.HasValue && !await _db.Projects.AnyAsync(p => p.Id == r.ProjectId && p.TenantId == tenantId))
            throw new InvalidOperationException("پروژه انتخاب‌شده معتبر نیست.");
        if (r.ContactId.HasValue && !await _db.Contacts.AnyAsync(c => c.Id == r.ContactId && c.TenantId == tenantId && c.IsActive))
            throw new InvalidOperationException("طرف حساب انتخاب‌شده معتبر نیست.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        selectedExpenseAccount ??= await _systemAccounts.GetOrCreateAsync(tenantId, "هزینه‌های عمومی", AccountType.Expense);
        var number = (await _db.ProjectExpenses.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).MaxAsync(x => (int?)x.Number) ?? 0) + 1;
        var expense = new ProjectExpense { TenantId = tenantId, Number = number, Date = r.Date, ProjectId = r.ProjectId,
            ContactId = r.ContactId, ExpenseAccountId = selectedExpenseAccount.Id, Amount = r.Amount,
            Description = r.Description.Trim(), Reference = Clean(r.Reference) };
        _db.ProjectExpenses.Add(expense);
        await _db.SaveChangesAsync();

        var payable = await _systemAccounts.GetOrCreateAsync(tenantId, "پرداختنی", AccountType.Payable);
        var journal = await _transactions.CreateAsync(new Transaction
        {
            TenantId = tenantId, Date = r.Date, Description = $"هزینه شماره {number}: {r.Description.Trim()}",
            Reference = Clean(r.Reference) ?? number.ToString(), IsPosted = true,
            ReferenceType = "ProjectExpense", ReferenceId = expense.Id,
            Lines = new List<TransactionLine>
            {
                new() { AccountId = selectedExpenseAccount.Id, Debit = r.Amount, Description = r.Description.Trim() },
                new() { AccountId = payable.Id, ContactId = r.ContactId, Credit = r.Amount, Description = r.Description.Trim() }
            }
        });
        expense.TransactionId = journal.Id; expense.IsPosted = true;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return expense.Id;
    }

    public async Task<int> AddPaymentAsync(ProjectExpensePaymentRequest r, int tenantId)
    {
        ValidateMoney(r.Amount, r.Date, "پرداخت");
        if (r.PaymentMethod == PaymentMethod.Cheque)
            throw new InvalidOperationException("برای پرداخت چکی، ابتدا گردش چک‌های پرداختنی باید به هزینه متصل شود؛ این روش در این مرحله در دسترس نیست.");
        var expense = await _db.ProjectExpenses.Include(x => x.Payments).Include(x => x.Contact)
            .FirstOrDefaultAsync(x => x.Id == r.ProjectExpenseId && x.TenantId == tenantId && x.IsPosted);
        if (expense == null) throw new InvalidOperationException("هزینه قطعی‌شده پیدا نشد.");
        var remaining = expense.Amount - expense.Payments.Sum(p => p.Amount);
        if (r.Amount > remaining) throw new InvalidOperationException("مبلغ پرداخت از مانده هزینه بیشتر است.");
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == r.AccountId && a.TenantId == tenantId && a.IsActive && (a.AccountType == AccountType.Bank || a.AccountType == AccountType.Cash));
        if (account == null) throw new InvalidOperationException("حساب پرداخت باید حساب بانکی یا صندوق فعال در شرکت جاری باشد.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var payable = await _systemAccounts.GetOrCreateAsync(tenantId, "پرداختنی", AccountType.Payable);
        var journal = await _transactions.CreateAsync(new Transaction
        {
            TenantId = tenantId, Date = r.Date, Description = $"پرداخت هزینه شماره {expense.Number}",
            Reference = Clean(r.Reference) ?? expense.Number.ToString(), IsPosted = true,
            ReferenceType = "ProjectExpensePayment",
            Lines = new List<TransactionLine>
            {
                new() { AccountId = payable.Id, ContactId = expense.ContactId, Debit = r.Amount, Description = $"تسویه هزینه {expense.Number}" },
                new() { AccountId = account.Id, Credit = r.Amount, Description = Clean(r.Description) ?? $"پرداخت هزینه {expense.Number}" }
            }
        });
        var payment = new ProjectExpensePayment { TenantId = tenantId, ProjectExpenseId = expense.Id, Date = r.Date, Amount = r.Amount,
            AccountId = account.Id, PaymentMethod = r.PaymentMethod, Reference = Clean(r.Reference), Description = Clean(r.Description), TransactionId = journal.Id };
        _db.ProjectExpensePayments.Add(payment);
        await _db.SaveChangesAsync();
        journal.ReferenceId = payment.Id;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return payment.Id;
    }

    public async Task DeletePaymentAsync(int expenseId, int paymentId, int tenantId)
    {
        var payment = await _db.ProjectExpensePayments
            .FirstOrDefaultAsync(x => x.Id == paymentId && x.ProjectExpenseId == expenseId && x.TenantId == tenantId)
            ?? throw new InvalidOperationException("پرداخت موردنظر پیدا نشد.");
        await using var tx = await _db.Database.BeginTransactionAsync();
        await DeletePaymentRecordAsync(payment, tenantId);
        await tx.CommitAsync();
    }

    public async Task DeleteAsync(int expenseId, int tenantId)
    {
        var expense = await _db.ProjectExpenses.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == expenseId && x.TenantId == tenantId)
            ?? throw new InvalidOperationException("هزینه موردنظر پیدا نشد.");
        var paymentIds = await _db.ProjectExpensePayments.AsNoTracking()
            .Where(x => x.ProjectExpenseId == expenseId && x.TenantId == tenantId)
            .Select(x => x.Id).ToListAsync();
        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var paymentId in paymentIds)
        {
            var payment = await _db.ProjectExpensePayments.FirstAsync(x => x.Id == paymentId && x.TenantId == tenantId);
            await DeletePaymentRecordAsync(payment, tenantId);
            _db.Entry(payment).State = EntityState.Detached;
        }
        if (expense.TransactionId.HasValue)
            await _transactions.DeleteAsync(expense.TransactionId.Value, tenantId);
        _db.Attachments.RemoveRange(_db.Attachments.Where(x => x.EntityType == AttachmentEntityType.ProjectExpense && x.EntityId == expense.Id));
        _db.ProjectExpenses.Attach(expense);
        _db.ProjectExpenses.Remove(expense);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task DeletePaymentRecordAsync(ProjectExpensePayment payment, int tenantId)
    {
        // Detach the required payment-to-transaction relationship before soft-deleting the journal.
        // Otherwise EF's client cascade treats the dependent as physically deleted before the
        // soft-delete interceptor can convert both records to updates.
        _db.Entry(payment).State = EntityState.Detached;
        await _transactions.DeleteAsync(payment.TransactionId, tenantId);
        _db.ProjectExpensePayments.Attach(payment);
        _db.ProjectExpensePayments.Remove(payment);
        var attachments = await _db.Attachments.Where(x =>
            x.EntityType == AttachmentEntityType.ProjectExpensePayment && x.EntityId == payment.Id).ToListAsync();
        _db.Attachments.RemoveRange(attachments);
        await _db.SaveChangesAsync();
    }

    private static void ValidateMoney(decimal amount, DateOnly date, string description)
    {
        if (amount <= 0 || decimal.Truncate(amount) != amount) throw new InvalidOperationException("مبلغ باید عدد صحیح و بیشتر از صفر باشد.");
        if (date == default) throw new InvalidOperationException("تاریخ الزامی است.");
        if (string.IsNullOrWhiteSpace(description)) throw new InvalidOperationException("شرح الزامی است.");
    }
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool IsInvoiceSystemExpense(string name)
        => name is "برگشت از فروش" or "تخفیف فروش" or "خرید کالا";
}
