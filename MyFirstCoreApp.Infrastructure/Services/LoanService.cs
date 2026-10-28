using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Loans;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class LoanService : ILoanService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITransactionService _transactionService;
    private readonly ISystemAccountService _systemAccounts;
    private readonly IAttachmentService _attachments;

    public LoanService(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        ITransactionService transactionService,
        ISystemAccountService systemAccounts,
        IAttachmentService attachments)
    {
        _context = context;
        _currentTenant = currentTenant;
        _transactionService = transactionService;
        _systemAccounts = systemAccounts;
        _attachments = attachments;
    }

    // ============================================
    // ثبت قرض
    // ============================================

    public async Task<int> CreateAsync(
        LoanRequest request, int tenantId, AttachmentUpload? receipt = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Direction))
            throw new InvalidOperationException("نوع قرض نامعتبر است.");

        ValidateAmountAndDate(request.Amount, request.Date);

        // رسید «قبل از ساخت هر چیزی» سنجیده می‌شود: رسید نامعتبر یعنی هیچ قرضی ثبت نمی‌شود
        var prepared = receipt == null
            ? null
            : await _attachments.PrepareAsync(receipt, cancellationToken);

        return await _context.ExecuteInTransactionAsync(async () =>
        {
            var contactName = await GetContactNameAsync(request.ContactId)
                ?? throw new InvalidOperationException("طرف حساب پیدا نشد.");

            await EnsureCashOrBankAsync(request.AccountId);

            var document = await BuildDocumentAsync(
                request.Direction, repayment: false, loanId: null, loanNumber: null,
                request.ContactId, contactName, request.AccountId,
                request.Amount, request.Date, request.Description, tenantId);

            await _transactionService.CreateAsync(document);

            if (prepared != null)
                await _attachments.AddAsync(AttachmentEntityType.Transaction, document.Id, prepared, cancellationToken);

            return document.Id;
        });
    }

    // ============================================
    // بازپرداخت
    // ============================================

    public async Task<int> RepayAsync(
        int loanId, LoanRepaymentRequest request, int tenantId,
        AttachmentUpload? receipt = null, CancellationToken cancellationToken = default)
    {
        ValidateAmountAndDate(request.Amount, request.Date);

        var prepared = receipt == null
            ? null
            : await _attachments.PrepareAsync(receipt, cancellationToken);

        return await _context.ExecuteInTransactionAsync(async () =>
        {
            // دو بازپرداخت هم‌زمان برای یک قرض پشت سر هم اجرا شوند؛ وگرنه هر دو «باقی‌مانده‌ی کافی» می‌بینند
            await LockLoanAsync(loanId);

            var loan = await LoadLoanDocumentAsync(loanId, tenantId);
            var direction = LoanDocumentTypes.DirectionOf(loan.ReferenceType!);

            var contactId = loan.Lines.Select(l => l.ContactId).FirstOrDefault(id => id.HasValue)
                ?? throw new InvalidOperationException("طرف حساب این قرض مشخص نیست.");

            var contactName = await GetContactNameAsync(contactId) ?? string.Empty;

            if (request.Date < loan.Date)
                throw new InvalidOperationException("تاریخ بازپرداخت نباید قبل از تاریخ قرض باشد.");

            await EnsureCashOrBankAsync(request.AccountId);

            var repaid = await SumRepaymentsAsync(loanId, tenantId);
            var remaining = loan.TotalAmount - repaid;

            if (request.Amount > remaining)
            {
                throw new InvalidOperationException(
                    $"مبلغ بازپرداخت ({request.Amount:N0}) بیشتر از باقی‌مانده‌ی قرض ({remaining:N0}) است.");
            }

            var document = await BuildDocumentAsync(
                direction, repayment: true, loanId: loan.Id, loanNumber: loan.Number,
                contactId, contactName, request.AccountId,
                request.Amount, request.Date, request.Description, tenantId);

            await _transactionService.CreateAsync(document);

            if (prepared != null)
                await _attachments.AddAsync(AttachmentEntityType.Transaction, document.Id, prepared, cancellationToken);

            return document.Id;
        });
    }

    // ============================================
    // حذف
    // ============================================

    public Task DeleteAsync(int loanId, int tenantId, CancellationToken cancellationToken = default)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            await LockLoanAsync(loanId);

            var loan = await LoadLoanDocumentAsync(loanId, tenantId);

            if (await HasRepaymentsAsync(loan.Id, tenantId))
                throw new InvalidOperationException("این قرض بازپرداخت دارد. ابتدا بازپرداخت‌ها را حذف کنید.");

            await _transactionService.DeleteAsync(loan.Id, tenantId);
        });
    }

    public Task DeleteRepaymentAsync(int repaymentId, int tenantId, CancellationToken cancellationToken = default)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var repayment = await _context.Transactions
                .Where(t => t.Id == repaymentId && t.TenantId == tenantId
                         && (t.ReferenceType == LoanDocumentTypes.GivenRepayment
                             || t.ReferenceType == LoanDocumentTypes.TakenRepayment))
                .Select(t => new { t.Id, t.ReferenceId })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("بازپرداخت پیدا نشد.");

            if (repayment.ReferenceId.HasValue)
                await LockLoanAsync(repayment.ReferenceId.Value);

            await _transactionService.DeleteAsync(repayment.Id, tenantId);
        });
    }

    // ============================================
    // پرس‌وجو
    // ============================================

    public async Task<IReadOnlyList<LoanListItem>> GetLoansAsync(
        LoanDirection? direction, bool onlyOpen, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();

        var types = direction switch
        {
            LoanDirection.Given => new[] { LoanDocumentTypes.Given },
            LoanDirection.Taken => new[] { LoanDocumentTypes.Taken },
            _ => new[] { LoanDocumentTypes.Given, LoanDocumentTypes.Taken }
        };

        var rows = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && types.Contains(t.ReferenceType!))
            .Select(t => new
            {
                t.Id,
                t.Number,
                t.Date,
                t.ReferenceType,
                t.TotalAmount,
                t.Description,
                ContactId = t.Lines.Where(l => l.ContactId != null).Select(l => l.ContactId).FirstOrDefault(),
                Repaid = _context.Transactions
                    .Where(r => r.TenantId == tenantId
                             && r.ReferenceId == t.Id
                             && (r.ReferenceType == LoanDocumentTypes.GivenRepayment
                                 || r.ReferenceType == LoanDocumentTypes.TakenRepayment))
                    .Sum(r => (decimal?)r.TotalAmount) ?? 0m
            })
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var contactIds = rows.Where(r => r.ContactId.HasValue).Select(r => r.ContactId!.Value).Distinct().ToList();
        var names = await _context.Contacts
            .AsNoTracking()
            .Where(c => contactIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return rows
            .Where(r => !onlyOpen || r.TotalAmount - r.Repaid > 0)
            .Select(r => new LoanListItem
            {
                Id = r.Id,
                Number = r.Number,
                Date = r.Date,
                Direction = LoanDocumentTypes.DirectionOf(r.ReferenceType!),
                ContactId = r.ContactId ?? 0,
                ContactName = r.ContactId.HasValue && names.TryGetValue(r.ContactId.Value, out var name) ? name : null,
                Amount = r.TotalAmount,
                Repaid = r.Repaid,
                Description = r.Description
            })
            .ToList();
    }

    public async Task<LoanDetails?> GetAsync(int loanId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();

        var loan = (await GetLoansAsync(null, onlyOpen: false, cancellationToken))
            .FirstOrDefault(l => l.Id == loanId);

        if (loan == null) return null;

        var repayments = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                     && t.ReferenceId == loanId
                     && (t.ReferenceType == LoanDocumentTypes.GivenRepayment
                         || t.ReferenceType == LoanDocumentTypes.TakenRepayment))
            .OrderBy(t => t.Date).ThenBy(t => t.Number)
            .Select(t => new LoanRepaymentItem
            {
                Id = t.Id,
                Number = t.Number,
                Date = t.Date,
                Amount = t.TotalAmount,
                Description = t.Description
            })
            .ToListAsync(cancellationToken);

        return new LoanDetails
        {
            Id = loan.Id,
            Number = loan.Number,
            Date = loan.Date,
            Direction = loan.Direction,
            ContactId = loan.ContactId,
            ContactName = loan.ContactName,
            Amount = loan.Amount,
            Repaid = loan.Repaid,
            Description = loan.Description,
            Repayments = repayments
        };
    }

    // ============================================
    // سند
    // ============================================

    private async Task<Transaction> BuildDocumentAsync(
        LoanDirection direction, bool repayment, int? loanId, int? loanNumber,
        int contactId, string contactName, int accountId,
        decimal amount, DateOnly date, string? userDescription, int tenantId)
    {
        // پول وارد حساب بانک/صندوق می‌شود: قرضِ گرفته‌شده و بازپرداختِ قرضِ داده‌شده
        var cashIn = (direction == LoanDirection.Taken && !repayment)
                     || (direction == LoanDirection.Given && repayment);

        var party = direction == LoanDirection.Given
            ? await _systemAccounts.GetOrCreateAsync(tenantId, "دریافتنی", AccountType.Receivable)
            : await _systemAccounts.GetOrCreateAsync(tenantId, "پرداختنی", AccountType.Payable);

        var title = (direction, repayment) switch
        {
            (LoanDirection.Given, false) => $"قرض داده‌شده به {contactName}",
            (LoanDirection.Taken, false) => $"قرض گرفته‌شده از {contactName}",
            (LoanDirection.Given, true) => $"بازپرداخت قرض (دریافت از {contactName})",
            _ => $"بازپرداخت قرض (پرداخت به {contactName})"
        };

        var description = "سند خودکار " + title;
        if (!string.IsNullOrWhiteSpace(userDescription))
            description += " - " + userDescription.Trim();

        var lines = new List<TransactionLine>
        {
            new()
            {
                AccountId = accountId,
                Debit = cashIn ? amount : 0,
                Credit = cashIn ? 0 : amount,
                Description = title
            },
            new()
            {
                AccountId = party.Id,
                ContactId = contactId,
                Debit = cashIn ? 0 : amount,
                Credit = cashIn ? amount : 0,
                Description = title
            }
        };

        return new Transaction
        {
            TenantId = tenantId,
            Number = 0, // شماره‌گذاری خودکار در TransactionService
            Date = date,
            Description = description,
            Reference = loanNumber?.ToString(),
            IsPosted = true,
            ReferenceType = repayment
                ? LoanDocumentTypes.RepaymentTypeFor(direction)
                : LoanDocumentTypes.LoanTypeFor(direction),
            ReferenceId = loanId,
            Lines = lines
        };
    }

    // ============================================
    // Helpers
    // ============================================

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("شرکت فعال مشخص نیست.");

    private static void ValidateAmountAndDate(decimal amount, DateOnly date)
    {
        if (amount <= 0)
            throw new InvalidOperationException("مبلغ باید بیشتر از صفر باشد.");

        if (decimal.Truncate(amount) != amount)
            throw new InvalidOperationException("مبلغ باید عدد صحیح (ریال) باشد.");

        if (date == default)
            throw new InvalidOperationException("تاریخ الزامی است.");
    }

    private async Task EnsureCashOrBankAsync(int accountId)
    {
        var valid = await _context.Accounts.AnyAsync(a =>
            a.Id == accountId && a.IsActive
            && (a.AccountType == AccountType.Bank || a.AccountType == AccountType.Cash));

        if (!valid)
            throw new InvalidOperationException("حساب انتخاب‌شده باید یک حساب بانکی یا صندوق (نقد) فعال باشد.");
    }

    private Task<string?> GetContactNameAsync(int contactId)
        => _context.Contacts
            .Where(c => c.Id == contactId)
            .Select(c => (string?)c.Name)
            .FirstOrDefaultAsync();

    private async Task<Transaction> LoadLoanDocumentAsync(int loanId, int tenantId)
    {
        var loan = await _context.Transactions
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == loanId && t.TenantId == tenantId
                                   && (t.ReferenceType == LoanDocumentTypes.Given
                                       || t.ReferenceType == LoanDocumentTypes.Taken));

        return loan ?? throw new InvalidOperationException("قرض پیدا نشد.");
    }

    private async Task<decimal> SumRepaymentsAsync(int loanId, int tenantId)
        => await _context.Transactions
            .Where(r => r.TenantId == tenantId
                     && r.ReferenceId == loanId
                     && (r.ReferenceType == LoanDocumentTypes.GivenRepayment
                         || r.ReferenceType == LoanDocumentTypes.TakenRepayment))
            .SumAsync(r => (decimal?)r.TotalAmount) ?? 0m;

    private Task<bool> HasRepaymentsAsync(int loanId, int tenantId)
        => _context.Transactions.AnyAsync(r => r.TenantId == tenantId
                                            && r.ReferenceId == loanId
                                            && (r.ReferenceType == LoanDocumentTypes.GivenRepayment
                                                || r.ReferenceType == LoanDocumentTypes.TakenRepayment));

    /// <summary>
    /// قفل «برنامه‌ایِ» هر قرض تا پایان تراکنش (فقط SQL Server؛ در سایر Providerها بی‌اثر است).
    /// بدون آن، دو بازپرداخت هم‌زمان هر دو باقی‌مانده‌ی کافی می‌دیدند و جمع‌شان از مبلغ قرض بیشتر می‌شد.
    /// </summary>
    private async Task LockLoanAsync(int loanId)
    {
        if (!_context.Database.IsSqlServer())
            return;

        await _context.Database.ExecuteSqlRawAsync(
            "DECLARE @result INT; " +
            "EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; " +
            "IF @result < 0 THROW 50001, N'قفل قرض در زمان مجاز گرفته نشد.', 1;",
            $"loan:{loanId}");
    }
}