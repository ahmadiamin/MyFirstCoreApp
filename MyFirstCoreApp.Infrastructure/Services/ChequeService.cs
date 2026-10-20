using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Banking;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// مدل حسابداری چک: «ثبت هنگام دریافت/صدور».
///
/// برای هر چک سه نوع سند خودکار (ReferenceType = "Cheque") وجود دارد:
///   ثبت  (Register): هنگام ثبت چک و هنگام ارائه‌ی مجدد؛ مانده‌ی طرف حساب همان لحظه اصلاح می‌شود.
///   معکوس (Reverse): هنگام برگشت یا ابطال چکِ در انتظار؛ سند ثبت را خنثی می‌کند.
///   پاس   (Clear)  : هنگام پاس/نقد شدن؛ پول بین بانک و حساب چک جابه‌جا می‌شود.
///
/// Cheque.RegistrationTransactionId = سند ثبتِ فعال (null یعنی چک در حال حاضر ثبت‌شده نیست).
/// Cheque.TransactionId             = سند پاس/نقد.
/// همه‌ی اسناد فقط از مسیر ITransactionService ساخته/حذف می‌شوند.
/// </summary>
public class ChequeService : IChequeService
{
    private const string DocumentReferenceType = "Cheque";

    private readonly ApplicationDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly ISystemAccountService _systemAccounts;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ChequeService(
        ApplicationDbContext context,
        ITransactionService transactionService,
        ISystemAccountService systemAccounts,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _context = context;
        _transactionService = transactionService;
        _systemAccounts = systemAccounts;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    // ============================================
    // CRUD
    // ============================================

    public Task<Cheque> CreateAsync(ChequeRequest request, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            await ValidateRequestAsync(request);

            var cheque = new Cheque
            {
                TenantId = tenantId,
                Status = ChequeStatus.InProgress
            };
            ApplyRequest(cheque, request);

            _context.Cheques.Add(cheque);
            await _context.SaveChangesAsync();

            // مانده‌ی طرف حساب بلافاصله اصلاح می‌شود (سند ثبت به تاریخ صدور)
            await RegisterAsync(cheque, cheque.IssueDate);

            AddHistory(cheque, null, ChequeStatus.InProgress, ChequeHistoryAction.Created, "ایجاد چک");
            await _context.SaveChangesAsync();

            return cheque;
        });
    }

    public Task UpdateAsync(int chequeId, ChequeRequest request, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (!ChequeRules.IsEditable(cheque.Status))
                throw new InvalidOperationException("چک در وضعیت فعلی قابل ویرایش نیست.");

            await ValidateRequestAsync(request);

            // سندهای چک به جهت وابسته‌اند؛ تغییر جهت، دفتر را خراب می‌کرد
            if (request.Direction != cheque.Direction)
                throw new InvalidOperationException("جهت چک بعد از ثبت قابل تغییر نیست.");

            var pending = ChequeRules.IsPending(cheque.Status);

            if (!pending &&
                (request.Amount != cheque.Amount || (request.ContactId ?? 0) != (cheque.ContactId ?? 0)))
            {
                throw new InvalidOperationException(
                    "در این وضعیت فقط اطلاعات توصیفی چک قابل ویرایش است (مبلغ و طرف حساب تغییر نمی‌کند).");
            }

            // پرداختِ قطعی روی همین مبلغ و طرف حساب بنا شده است
            if (request.Amount != cheque.Amount || (request.ContactId ?? 0) != (cheque.ContactId ?? 0))
                await EnsureNoPostedPaymentAsync(cheque.Id, "تغییر مبلغ یا طرف حساب چک ممکن نیست.");

            ApplyRequest(cheque, request);

            if (pending)
                await ReplaceRegistrationAsync(cheque);

            AddHistory(cheque, cheque.Status, cheque.Status, ChequeHistoryAction.Note, "ویرایش چک");
            await _context.SaveChangesAsync();
        });
    }

    public Task DeleteAsync(int chequeId, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await _context.Cheques
                .Include(c => c.Histories)
                .FirstOrDefaultAsync(c => c.Id == chequeId && c.TenantId == tenantId)
                ?? throw new InvalidOperationException("چک پیدا نشد.");

            if (cheque.Status != ChequeStatus.InProgress)
                throw new InvalidOperationException("فقط چک در جریان قابل حذف است.");

            var usedByPayment = await _context.Payments
                .Where(p => p.ChequeId == cheque.Id && p.Status != PaymentStatus.Cancelled)
                .Select(p => (int?)p.Number)
                .FirstOrDefaultAsync();

            if (usedByPayment.HasValue)
                throw new InvalidOperationException(
                    $"این چک در پرداخت شماره {usedByPayment} استفاده شده و قابل حذف نیست.");

            // همه‌ی سندهای این چک (ثبت، معکوس‌های قدیمی، ...)
            var documentIds = await _context.Transactions
                .Where(t => t.ReferenceType == DocumentReferenceType && t.ReferenceId == cheque.Id)
                .Select(t => t.Id)
                .ToListAsync();

            foreach (var documentId in documentIds)
                await _transactionService.DeleteAsync(documentId, tenantId);

            // حذف واقعی را Interceptor به حذف نرم تبدیل می‌کند
            _context.ChequeHistories.RemoveRange(cheque.Histories.ToList());
            _context.Cheques.Remove(cheque);

            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // گردش کار
    // ============================================

    public Task MarkAsDepositedAsync(int chequeId, int tenantId, string? description)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (!ChequeRules.IsPending(cheque.Status))
                throw new InvalidOperationException("فقط چک در جریان یا ارائه‌مجدد قابل ارائه به بانک است.");

            AddHistory(cheque, cheque.Status, cheque.Status,
                ChequeHistoryAction.Deposited, description ?? "به بانک داده شد");

            await _context.SaveChangesAsync();
        });
    }

    public Task MarkAsClearedAsync(int chequeId, int tenantId, DateOnly? clearedDate, string? bankTrackingNumber)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (cheque.Status == ChequeStatus.Cleared)
                throw new InvalidOperationException("چک قبلاً پاس شده است.");

            if (!ChequeRules.IsPending(cheque.Status))
                throw new InvalidOperationException(
                    "فقط چک در جریان یا ارائه‌مجدد قابل پاس شدن است. اگر چک برگشتی دوباره وصول می‌شود، ابتدا «ارائه مجدد» را ثبت کنید.");

            await SettleAsync(cheque, ChequeStatus.Cleared, clearedDate,
                ChequeHistoryAction.Cleared, "چک پاس شد", bankTrackingNumber);
        });
    }

    public Task MarkAsCashedAsync(int chequeId, int tenantId, DateOnly? cashedDate, string? description)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (!ChequeRules.IsPending(cheque.Status))
                throw new InvalidOperationException("فقط چک در جریان یا ارائه‌مجدد قابل نقد شدن است.");

            await SettleAsync(cheque, ChequeStatus.Cashed, cashedDate,
                ChequeHistoryAction.Cashed, description ?? "چک نقد شد", null);
        });
    }

    public Task MarkAsBouncedAsync(int chequeId, int tenantId, string? bounceReason, DateOnly? bouncedDate)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (!ChequeRules.IsPending(cheque.Status))
                throw new InvalidOperationException("فقط چک در جریان یا ارائه‌مجدد قابل برگشت خوردن است.");

            // اگر پرداخت قطعی به این چک تکیه دارد، فاکتورها «پرداخت‌شده» می‌مانند ولی دفتر برمی‌گردد
            await EnsureNoPostedPaymentAsync(cheque.Id, "ثبت برگشت چک ممکن نیست.");

            var date = bouncedDate ?? _timeProvider.Today();

            // مانده‌ی طرف حساب برمی‌گردد (چک دیگر وصول‌شدنی نیست)
            if (cheque.RegistrationTransactionId.HasValue)
                await ReverseRegistrationAsync(cheque, date, "برگشت چک");

            var oldStatus = cheque.Status;
            cheque.Status = ChequeStatus.Bounced;
            cheque.BouncedDate = date;
            cheque.BounceReason = bounceReason;

            AddHistory(cheque, oldStatus, ChequeStatus.Bounced,
                ChequeHistoryAction.Bounced, bounceReason ?? "چک برگشت خورد");

            await _context.SaveChangesAsync();
        });
    }

    public Task MarkAsReturnedAsync(int chequeId, int tenantId, string? description)
    {
        return ChangeStatusWithoutLedgerAsync(chequeId, tenantId,
            allowed: s => s == ChequeStatus.Bounced,
            notAllowedMessage: "فقط چک برگشتی قابل عودت است.",
            newStatus: ChequeStatus.Returned,
            action: ChequeHistoryAction.Returned,
            description: description ?? "به مشتری عودت داده شد");
    }

    public Task MarkAsReferredAsync(int chequeId, int tenantId, string? description)
    {
        return ChangeStatusWithoutLedgerAsync(chequeId, tenantId,
            allowed: s => s == ChequeStatus.Bounced,
            notAllowedMessage: "فقط چک برگشتی قابل ارجاع است.",
            newStatus: ChequeStatus.Referred,
            action: ChequeHistoryAction.Referred,
            description: description ?? "ارجاع به صاحب چک");
    }

    public Task MarkAsRePresentedAsync(int chequeId, int tenantId, DateOnly newDueDate, string? description)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (cheque.Status != ChequeStatus.Bounced && cheque.Status != ChequeStatus.Returned)
                throw new InvalidOperationException("فقط چک برگشتی/عودت‌شده قابل ارائه مجدد است.");

            if (newDueDate == default)
                throw new InvalidOperationException("تاریخ سررسید جدید الزامی است.");

            if (newDueDate < cheque.IssueDate)
                throw new InvalidOperationException("سررسید جدید نباید قبل از تاریخ صدور چک باشد.");

            var oldStatus = cheque.Status;
            cheque.Status = ChequeStatus.RePresented;
            cheque.DueDate = newDueDate;
            cheque.BouncedDate = null;

            // چک دوباره در انتظار وصول است؛ مانده‌ی طرف حساب دوباره اصلاح می‌شود
            await EnsureRegisteredAsync(cheque, _timeProvider.Today());

            AddHistory(cheque, oldStatus, ChequeStatus.RePresented,
                ChequeHistoryAction.RePresented,
                $"ارائه مجدد با سررسید {PersianDate.Format(newDueDate)} - {description}");

            await _context.SaveChangesAsync();
        });
    }

    public Task MarkAsCancelledAsync(int chequeId, int tenantId, string? reason)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            // قبلاً چک نقد‌شده هم قابل ابطال بود و سند پاس‌اش در دفتر می‌ماند
            if (cheque.Status is ChequeStatus.Cleared or ChequeStatus.Cashed)
                throw new InvalidOperationException(
                    "چک پاس یا نقد شده قابل ابطال نیست. ابتدا آن را از حالت پاس/نقد برگردانید.");

            if (cheque.Status == ChequeStatus.Cancelled)
                throw new InvalidOperationException("چک قبلاً ابطال شده است.");

            await EnsureNoPostedPaymentAsync(cheque.Id, "ابطال چک ممکن نیست.");

            // چک در انتظار هنوز در دفتر ثبت است؛ برگشتی/عودتی/ارجاعی قبلاً معکوس شده‌اند
            if (ChequeRules.IsPending(cheque.Status) && cheque.RegistrationTransactionId.HasValue)
                await ReverseRegistrationAsync(cheque, _timeProvider.Today(), "ابطال چک");

            var oldStatus = cheque.Status;
            cheque.Status = ChequeStatus.Cancelled;

            AddHistory(cheque, oldStatus, ChequeStatus.Cancelled,
                ChequeHistoryAction.Cancelled, reason ?? "ابطال شد");

            await _context.SaveChangesAsync();
        });
    }

    public Task AddNoteAsync(int chequeId, int tenantId, string note)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(note))
                throw new InvalidOperationException("متن یادداشت الزامی است.");

            var cheque = await GetChequeAsync(chequeId, tenantId);

            AddHistory(cheque, cheque.Status, cheque.Status, ChequeHistoryAction.Note, note.Trim());
            await _context.SaveChangesAsync();
        });
    }

    public Task UnclearAsync(int chequeId, int tenantId, string? reason)
        => RevertSettlementAsync(chequeId, tenantId, ChequeStatus.Cleared,
            "فقط چک پاس‌شده قابل برگشت است.", reason ?? "برگشت از پاس (اشتباه پاس شده)");

    public Task UncashAsync(int chequeId, int tenantId, string? reason)
        => RevertSettlementAsync(chequeId, tenantId, ChequeStatus.Cashed,
            "فقط چک نقد‌شده قابل برگشت است.", reason ?? "برگشت از نقد (اشتباه نقد شده)");

    // ============================================
    // عملیات مشترک گردش کار
    // ============================================

    /// <summary>پاس/نقد: سند بانک ↔ حساب چک (و در صورت نبودنِ سند ثبت، ابتدا ثبت).</summary>
    private async Task SettleAsync(
        Cheque cheque, ChequeStatus newStatus, DateOnly? date,
        ChequeHistoryAction action, string description, string? bankTrackingNumber)
    {
        var settleDate = date ?? _timeProvider.Today();

        // چک‌های قدیمی (قبل از این مدل) سند ثبت ندارند؛ برای یکدست شدن دفتر همین‌جا ثبت می‌شوند
        await EnsureRegisteredAsync(cheque, cheque.IssueDate);

        var document = await BuildClearingDocumentAsync(cheque, settleDate);
        await _transactionService.CreateAsync(document);

        var oldStatus = cheque.Status;
        cheque.Status = newStatus;
        cheque.ClearedDate = settleDate;
        cheque.TransactionId = document.Id;

        if (newStatus == ChequeStatus.Cleared)
            cheque.BankTrackingNumber = bankTrackingNumber;

        AddHistory(cheque, oldStatus, newStatus, action, description);
        await _context.SaveChangesAsync();
    }

    /// <summary>برگشت از پاس/نقد: سند پاس حذف می‌شود؛ سند ثبت سر جایش است و چک دوباره در انتظار است.</summary>
    private Task RevertSettlementAsync(int chequeId, int tenantId, ChequeStatus expected,
        string notAllowedMessage, string description)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (cheque.Status != expected)
                throw new InvalidOperationException(notAllowedMessage);

            if (cheque.TransactionId.HasValue)
            {
                var documentId = cheque.TransactionId.Value;

                var exists = await _context.Transactions.AnyAsync(t => t.Id == documentId);
                if (exists)
                    await _transactionService.DeleteAsync(documentId, tenantId);

                cheque.TransactionId = null;
            }

            var oldStatus = cheque.Status;
            cheque.Status = ChequeStatus.InProgress;
            cheque.ClearedDate = null;
            cheque.BankTrackingNumber = null;

            // چک قدیمی بدون سند ثبت: الان که دوباره در انتظار است باید ثبت شود
            await EnsureRegisteredAsync(cheque, cheque.IssueDate);

            AddHistory(cheque, oldStatus, ChequeStatus.InProgress, ChequeHistoryAction.StatusChanged, description);
            await _context.SaveChangesAsync();
        });
    }

    private Task ChangeStatusWithoutLedgerAsync(
        int chequeId, int tenantId, Func<ChequeStatus, bool> allowed, string notAllowedMessage,
        ChequeStatus newStatus, ChequeHistoryAction action, string description)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var cheque = await GetChequeAsync(chequeId, tenantId);

            if (!allowed(cheque.Status))
                throw new InvalidOperationException(notAllowedMessage);

            var oldStatus = cheque.Status;
            cheque.Status = newStatus;

            AddHistory(cheque, oldStatus, newStatus, action, description);
            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // اسناد حسابداری
    // ============================================

    private async Task RegisterAsync(Cheque cheque, DateOnly date)
    {
        var document = await BuildRegistrationDocumentAsync(cheque, date, reverse: false);
        await _transactionService.CreateAsync(document);

        cheque.RegistrationTransactionId = document.Id;
    }

    private async Task EnsureRegisteredAsync(Cheque cheque, DateOnly date)
    {
        if (cheque.RegistrationTransactionId.HasValue)
        {
            var id = cheque.RegistrationTransactionId.Value;
            if (await _context.Transactions.AnyAsync(t => t.Id == id))
                return;
        }

        await RegisterAsync(cheque, date);
    }

    private async Task ReverseRegistrationAsync(Cheque cheque, DateOnly date, string reason)
    {
        var reversal = await BuildRegistrationDocumentAsync(cheque, date, reverse: true, reason);
        await _transactionService.CreateAsync(reversal);

        cheque.RegistrationTransactionId = null;
    }

    /// <summary>ویرایش چک در انتظار: سند ثبت قدیمی حذف و با مبلغ/طرف حساب جدید دوباره ساخته می‌شود.</summary>
    private async Task ReplaceRegistrationAsync(Cheque cheque)
    {
        var date = cheque.IssueDate;

        if (cheque.RegistrationTransactionId.HasValue)
        {
            var oldId = cheque.RegistrationTransactionId.Value;

            var old = await _context.Transactions
                .Where(t => t.Id == oldId)
                .Select(t => new { t.Date })
                .FirstOrDefaultAsync();

            if (old != null)
            {
                // تاریخ ثبت قبلی حفظ می‌شود (مثلاً تاریخ ارائه‌ی مجدد)
                date = old.Date;
                await _transactionService.DeleteAsync(oldId, cheque.TenantId);
            }

            cheque.RegistrationTransactionId = null;
        }

        await RegisterAsync(cheque, date);
    }

    private async Task<Transaction> BuildRegistrationDocumentAsync(
        Cheque cheque, DateOnly date, bool reverse, string? reason = null)
    {
        var received = cheque.Direction == ChequeDirection.Received;
        var accounts = await GetAccountsAsync(cheque);

        var kind = received ? "دریافتنی" : "پرداختنی";
        var contactName = await GetContactNameAsync(cheque.ContactId);

        var title = reverse
            ? $"{reason ?? "معکوس"} {kind} شماره {cheque.ChequeNumber}"
            : $"ثبت چک {kind} شماره {cheque.ChequeNumber}";

        var description = $"سند خودکار {title}" + PartySuffix(cheque, contactName);

        // ثبت — دریافتنی: چک (بدهکار) / طرف حساب (بستانکار)
        //       پرداختنی: طرف حساب (بدهکار) / چک (بستانکار)
        // معکوس = جابه‌جایی همین دو سمت
        var instrumentIsDebit = received != reverse;

        var instrumentLine = new TransactionLine
        {
            AccountId = accounts.Instrument.Id,
            Debit = instrumentIsDebit ? cheque.Amount : 0,
            Credit = instrumentIsDebit ? 0 : cheque.Amount,
            Description = title
        };

        var partyLine = new TransactionLine
        {
            AccountId = accounts.Party.Id,
            ContactId = cheque.ContactId,
            Debit = instrumentIsDebit ? 0 : cheque.Amount,
            Credit = instrumentIsDebit ? cheque.Amount : 0,
            Description = title
        };

        return NewDocument(cheque, date, description, new List<TransactionLine> { instrumentLine, partyLine });
    }

    private async Task<Transaction> BuildClearingDocumentAsync(Cheque cheque, DateOnly date)
    {
        var received = cheque.Direction == ChequeDirection.Received;
        var accounts = await GetAccountsAsync(cheque);

        var kind = received ? "دریافتنی" : "پرداختنی";
        var contactName = await GetContactNameAsync(cheque.ContactId);

        var title = $"پاس چک {kind} شماره {cheque.ChequeNumber}";
        var description = $"سند خودکار {title}" + PartySuffix(cheque, contactName);

        if (!string.IsNullOrEmpty(cheque.BankName))
            description += $" - بانک {cheque.BankName}";

        // دریافتنی: بانک (بدهکار) / چک (بستانکار) — پرداختنی: چک (بدهکار) / بانک (بستانکار)
        var bankLine = new TransactionLine
        {
            AccountId = accounts.Bank.Id,
            Debit = received ? cheque.Amount : 0,
            Credit = received ? 0 : cheque.Amount,
            Description = title
        };

        var instrumentLine = new TransactionLine
        {
            AccountId = accounts.Instrument.Id,
            Debit = received ? 0 : cheque.Amount,
            Credit = received ? cheque.Amount : 0,
            Description = title
        };

        return NewDocument(cheque, date, description, new List<TransactionLine> { bankLine, instrumentLine });
    }

    private static Transaction NewDocument(Cheque cheque, DateOnly date, string description, List<TransactionLine> lines)
    {
        return new Transaction
        {
            TenantId = cheque.TenantId,
            Number = 0, // شماره‌گذاری خودکار در TransactionService
            Date = date,
            Description = description,
            Reference = cheque.ChequeNumber,
            IsPosted = true,
            ReferenceType = DocumentReferenceType,
            ReferenceId = cheque.Id,
            Lines = lines
        };
    }

    private async Task<ChequeAccounts> GetAccountsAsync(Cheque cheque)
    {
        var received = cheque.Direction == ChequeDirection.Received;
        var tenantId = cheque.TenantId;

        var instrument = await _systemAccounts.GetOrCreateAsync(
            tenantId, received ? "چک‌های دریافتنی" : "چک‌های پرداختنی", AccountType.Contra);

        var party = await _systemAccounts.GetOrCreateAsync(
            tenantId, received ? "دریافتنی" : "پرداختنی",
            received ? AccountType.Receivable : AccountType.Payable);

        Account bank;
        if (cheque.BankAccountId.HasValue)
        {
            var bankId = cheque.BankAccountId.Value;
            bank = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == bankId)
                   ?? throw new InvalidOperationException("حساب بانکی چک پیدا نشد.");
        }
        else
        {
            bank = await _systemAccounts.GetOrCreateAsync(tenantId, "بانک", AccountType.Bank);
        }

        return new ChequeAccounts(instrument, party, bank);
    }

    private sealed record ChequeAccounts(Account Instrument, Account Party, Account Bank);

    private static string PartySuffix(Cheque cheque, string? contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return string.Empty;

        return cheque.Direction == ChequeDirection.Received
            ? $" - از {contactName}"
            : $" - به {contactName}";
    }

    private Task<string?> GetContactNameAsync(int? contactId)
    {
        if (!contactId.HasValue) return Task.FromResult<string?>(null);

        return _context.Contacts
            .Where(c => c.Id == contactId.Value)
            .Select(c => (string?)c.Name)
            .FirstOrDefaultAsync();
    }

    // ============================================
    // اعتبارسنجی و نگاشت درخواست
    // ============================================

    private async Task ValidateRequestAsync(ChequeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ChequeNumber))
            throw new InvalidOperationException("شماره چک الزامی است.");

        if (request.ChequeNumber.Trim().Length > 50)
            throw new InvalidOperationException("شماره چک حداکثر ۵۰ کاراکتر است.");

        if (!Enum.IsDefined(request.Direction))
            throw new InvalidOperationException("جهت چک نامعتبر است.");

        if (request.Amount <= 0)
            throw new InvalidOperationException("مبلغ چک باید بیشتر از صفر باشد.");

        if (decimal.Truncate(request.Amount) != request.Amount)
            throw new InvalidOperationException("مبلغ چک باید عدد صحیح (ریال) باشد.");

        if (request.IssueDate == default)
            throw new InvalidOperationException("تاریخ صدور الزامی است.");

        if (request.DueDate == default)
            throw new InvalidOperationException("تاریخ سررسید الزامی است.");

        if (request.DueDate < request.IssueDate)
            throw new InvalidOperationException("تاریخ سررسید نباید قبل از تاریخ صدور باشد.");

        if (request.ContactId is > 0)
        {
            var contactId = request.ContactId.Value;
            if (!await _context.Contacts.AnyAsync(c => c.Id == contactId))
                throw new InvalidOperationException("طرف حساب معتبر نیست.");
        }

        if (request.BankAccountId is > 0)
        {
            var bankId = request.BankAccountId.Value;
            if (!await _context.Accounts.AnyAsync(a => a.Id == bankId && a.AccountType == AccountType.Bank))
                throw new InvalidOperationException("حساب بانکی انتخاب‌شده معتبر نیست.");
        }

        if (request.InvoiceId is > 0)
        {
            var invoiceId = request.InvoiceId.Value;
            var invoice = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => new { i.InvoiceType, i.ContactId })
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("فاکتور انتخاب‌شده معتبر نیست.");

            var expectedType = request.Direction == ChequeDirection.Received
                ? InvoiceType.Sale
                : InvoiceType.Purchase;

            if (invoice.InvoiceType != expectedType)
                throw new InvalidOperationException("فاکتور انتخاب‌شده با جهت چک سازگار نیست.");

            if (request.ContactId is > 0 && invoice.ContactId != request.ContactId)
                throw new InvalidOperationException("فاکتور انتخاب‌شده متعلق به این طرف حساب نیست.");
        }
    }

    private static void ApplyRequest(Cheque cheque, ChequeRequest request)
    {
        cheque.ChequeNumber = request.ChequeNumber.Trim();
        cheque.Direction = request.Direction;
        cheque.Amount = request.Amount;
        cheque.IssueDate = request.IssueDate;
        cheque.DueDate = request.DueDate;
        cheque.ContactId = request.ContactId is > 0 ? request.ContactId : null;
        cheque.BankAccountId = request.BankAccountId is > 0 ? request.BankAccountId : null;
        cheque.BankName = Clean(request.BankName);
        cheque.AccountNumber = Clean(request.AccountNumber);
        cheque.BranchCode = Clean(request.BranchCode);
        cheque.BranchName = Clean(request.BranchName);
        cheque.DrawerName = Clean(request.DrawerName);
        cheque.DrawerNationalId = Clean(request.DrawerNationalId);
        cheque.Description = Clean(request.Description);
        cheque.InvoiceId = request.InvoiceId is > 0 ? request.InvoiceId : null;
        cheque.ImagePath = request.ImagePath ?? cheque.ImagePath;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ============================================
    // Helpers
    // ============================================

    /// <summary>
    /// تا وقتی یک پرداخت «قطعی» به این چک وصل است، چک نباید برگردد/ابطال شود/مبلغش عوض شود؛
    /// وگرنه فاکتورهای آن پرداخت پرداخت‌شده می‌مانند ولی دفتر چیز دیگری می‌گوید.
    /// </summary>
    private async Task EnsureNoPostedPaymentAsync(int chequeId, string actionMessage)
    {
        var paymentNumber = await _context.Payments
            .Where(p => p.ChequeId == chequeId && p.Status == PaymentStatus.Posted)
            .Select(p => (int?)p.Number)
            .FirstOrDefaultAsync();

        if (paymentNumber.HasValue)
            throw new InvalidOperationException(
                $"{actionMessage} این چک در پرداخت شماره {paymentNumber} استفاده شده و آن پرداخت قطعی است؛ " +
                "ابتدا پرداخت را به پیش‌نویس برگردانید.");
    }

    private async Task<Cheque> GetChequeAsync(int chequeId, int tenantId)
    {
        var cheque = await _context.Cheques
            .FirstOrDefaultAsync(c => c.Id == chequeId && c.TenantId == tenantId);

        return cheque ?? throw new InvalidOperationException("چک پیدا نشد.");
    }

    private void AddHistory(Cheque cheque, ChequeStatus? from, ChequeStatus to,
        ChequeHistoryAction action, string? description)
    {
        _context.ChequeHistories.Add(new ChequeHistory
        {
            TenantId = cheque.TenantId,
            ChequeId = cheque.Id,
            FromStatus = from,
            ToStatus = to,
            Action = action,
            Description = description,
            Date = _timeProvider.GetUtcNow().UtcDateTime,
            UserId = _currentUser.UserId
        });
    }
}
