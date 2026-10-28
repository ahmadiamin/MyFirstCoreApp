using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Loans;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class LoanServiceTests : IDisposable
{
    private const int TenantA = 1;
    private const int TenantB = 2;
    private const decimal Amount = 1_000_000;

    private static readonly byte[] PngBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };

    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mfca-loan-" + Guid.NewGuid().ToString("N"));
    private readonly AttachmentOptions _options;

    public LoanServiceTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";
        _options = new AttachmentOptions { RootPath = _root, MaxFileSizeBytes = 1_000_000 };
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // ---------------------------------------------------------------
    // ثبت قرض
    // ---------------------------------------------------------------

    [Fact]
    public async Task Given_DebitsTheContactsReceivable_AndCreditsTheBank()
    {
        var f = await ArrangeAsync();

        var loanId = await CreateAsync(f, LoanDirection.Given);

        await using var verify = _db.NewContext();
        var document = await verify.Transactions.Include(t => t.Lines).SingleAsync(t => t.Id == loanId);

        Assert.Equal(LoanDocumentTypes.Given, document.ReferenceType);
        Assert.Null(document.ReferenceId);
        Assert.True(document.IsPosted);
        Assert.Equal(new DateOnly(2026, 4, 1), document.Date);
        Assert.Equal(Amount, document.TotalAmount);

        var party = document.Lines.Single(l => l.ContactId == f.Contact);
        Assert.Equal(Amount, party.Debit);                              // مشتری به ما بدهکار شد
        Assert.Equal(Amount, document.Lines.Single(l => l.AccountId == f.Bank).Credit);   // پول از بانک خارج شد

        Assert.Equal(Amount, await ContactBalanceAsync(f.Contact));
        Assert.Equal(-Amount, await AccountBalanceAsync(f.Bank));
    }

    [Fact]
    public async Task Taken_DebitsTheBank_AndCreditsThePayableOfTheContact()
    {
        var f = await ArrangeAsync();

        var loanId = await CreateAsync(f, LoanDirection.Taken);

        await using var verify = _db.NewContext();
        var document = await verify.Transactions.Include(t => t.Lines).SingleAsync(t => t.Id == loanId);

        Assert.Equal(LoanDocumentTypes.Taken, document.ReferenceType);
        Assert.Equal(Amount, document.Lines.Single(l => l.AccountId == f.Bank).Debit);     // پول وارد بانک شد
        Assert.Equal(Amount, document.Lines.Single(l => l.ContactId == f.Contact).Credit);  // ما بدهکار شدیم

        Assert.Equal(-Amount, await ContactBalanceAsync(f.Contact));
        Assert.Equal(Amount, await AccountBalanceAsync(f.Bank));
    }

    [Fact]
    public async Task Create_WorksWithACashAccountToo()
    {
        var f = await ArrangeAsync();

        var loanId = await NewLoanAsync(f.Contact, f.Cash, LoanDirection.Given);

        await using var verify = _db.NewContext();
        var document = await verify.Transactions.Include(t => t.Lines).SingleAsync(t => t.Id == loanId);
        Assert.Equal(Amount, document.Lines.Single(l => l.AccountId == f.Cash).Credit);
    }

    [Fact]
    public async Task Create_RejectsInvalidInput()
    {
        var f = await ArrangeAsync();

        var cases = new (Func<LoanRequest, LoanRequest> Mutate, string Expected)[]
        {
            (r => { r.Amount = 0; return r; }, "بیشتر از صفر"),
            (r => { r.Amount = 1_000.5m; return r; }, "عدد صحیح"),
            (r => { r.Date = default; return r; }, "تاریخ"),
            (r => { r.ContactId = 999_999; return r; }, "طرف حساب"),
            (r => { r.AccountId = f.Expense; return r; }, "بانکی یا صندوق"),   // حساب هزینه
            (r => { r.AccountId = 999_999; return r; }, "بانکی یا صندوق"),
            (r => { r.Direction = (LoanDirection)99; return r; }, "نوع قرض"),
        };

        foreach (var (mutate, expected) in cases)
        {
            var request = mutate(NewRequest(f, LoanDirection.Given));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(request));
            Assert.Contains(expected, ex.Message);
        }

        await using var verify = _db.NewContext();
        Assert.Empty(await verify.Transactions.ToListAsync());
    }

    // ---------------------------------------------------------------
    // رسید
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_WithAValidReceipt_AttachesItToTheLoanDocument()
    {
        var f = await ArrangeAsync();

        var loanId = await CreateAsync(NewRequest(f, LoanDirection.Given), Receipt("رسید.png", PngBytes));

        await using var context = _db.NewContext();
        var attachments = await NewAttachmentService(context).ListAsync(AttachmentEntityType.Transaction, loanId);

        var attachment = Assert.Single(attachments);
        Assert.Equal("رسید.png", attachment.FileName);
    }

    [Fact]
    public async Task Create_WithAnInvalidReceipt_CreatesNoLoanAtAll()
    {
        var f = await ArrangeAsync();

        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAsync(NewRequest(f, LoanDirection.Given), Receipt("fake.png", exe)));

        Assert.Contains("نمی‌خواند", ex.Message);

        await using var verify = _db.NewContext();
        Assert.Empty(await verify.Transactions.ToListAsync());
        Assert.Empty(await verify.TransactionLines.ToListAsync());
        Assert.Equal(0, await ContactBalanceAsync(f.Contact));
    }

    // ---------------------------------------------------------------
    // بازپرداخت
    // ---------------------------------------------------------------

    [Fact]
    public async Task Repay_OfAGivenLoan_ReceivesMoney_AndReducesTheRemaining()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Given);

        var repaymentId = await RepayAsync(loanId, f.Bank, 400_000, new DateOnly(2026, 5, 1));

        await using var verify = _db.NewContext();
        var repayment = await verify.Transactions.Include(t => t.Lines).SingleAsync(t => t.Id == repaymentId);

        Assert.Equal(LoanDocumentTypes.GivenRepayment, repayment.ReferenceType);
        Assert.Equal(loanId, repayment.ReferenceId);
        Assert.Equal(400_000, repayment.Lines.Single(l => l.AccountId == f.Bank).Debit);     // پول به بانک برگشت
        Assert.Equal(400_000, repayment.Lines.Single(l => l.ContactId == f.Contact).Credit);

        Assert.Equal(600_000, await ContactBalanceAsync(f.Contact));
        Assert.Equal(-600_000, await AccountBalanceAsync(f.Bank));

        var loan = await NewService(verify).GetAsync(loanId);
        Assert.Equal(600_000, loan!.Remaining);
        Assert.Equal(400_000, loan.Repaid);
        Assert.Single(loan.Repayments);
    }

    [Fact]
    public async Task Repay_OfATakenLoan_PaysMoney()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Taken);

        var repaymentId = await RepayAsync(loanId, f.Bank, 250_000, new DateOnly(2026, 5, 1));

        await using var verify = _db.NewContext();
        var repayment = await verify.Transactions.Include(t => t.Lines).SingleAsync(t => t.Id == repaymentId);

        Assert.Equal(LoanDocumentTypes.TakenRepayment, repayment.ReferenceType);
        Assert.Equal(250_000, repayment.Lines.Single(l => l.ContactId == f.Contact).Debit);   // بدهی ما کم شد
        Assert.Equal(250_000, repayment.Lines.Single(l => l.AccountId == f.Bank).Credit);     // پول از بانک خارج شد

        Assert.Equal(-750_000, await ContactBalanceAsync(f.Contact));
    }

    [Fact]
    public async Task Repay_CannotExceedTheRemaining_OrPrecedeTheLoanDate_OrUseABadAccount()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Given);
        await RepayAsync(loanId, f.Bank, 700_000, new DateOnly(2026, 5, 1));

        // 400 هزار > باقی‌مانده‌ی 300 هزار
        var tooMuch = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RepayAsync(loanId, f.Bank, 400_000, new DateOnly(2026, 5, 2)));
        Assert.Contains("باقی‌مانده", tooMuch.Message);

        // قبل از تاریخ قرض (2026-04-01)
        var tooEarly = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RepayAsync(loanId, f.Bank, 100_000, new DateOnly(2026, 3, 30)));
        Assert.Contains("قبل از تاریخ قرض", tooEarly.Message);

        // حساب هزینه
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RepayAsync(loanId, f.Expense, 100_000, new DateOnly(2026, 5, 2)));

        // مبلغ غیرصحیح
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RepayAsync(loanId, f.Bank, 1.5m, new DateOnly(2026, 5, 2)));

        await using var verify = _db.NewContext();
        Assert.Equal(300_000, (await NewService(verify).GetAsync(loanId))!.Remaining);
    }

    [Fact]
    public async Task Repay_UntilZero_ClosesTheLoan_AndOnlyOpenFilterHidesIt()
    {
        var f = await ArrangeAsync();
        var closed = await CreateAsync(f, LoanDirection.Given);
        var open = await CreateAsync(f, LoanDirection.Taken);

        await RepayAsync(closed, f.Bank, Amount, new DateOnly(2026, 5, 1));

        await using var verify = _db.NewContext();
        var service = NewService(verify);

        var openOnly = await service.GetLoansAsync(null, onlyOpen: true);
        Assert.Equal(new[] { open }, openOnly.Select(l => l.Id).ToArray());

        var all = await service.GetLoansAsync(null, onlyOpen: false);
        Assert.Equal(2, all.Count);

        var givenOnly = await service.GetLoansAsync(LoanDirection.Given, onlyOpen: false);
        Assert.Equal(new[] { closed }, givenOnly.Select(l => l.Id).ToArray());
        Assert.Equal("مشتری", givenOnly[0].ContactName);
        Assert.Equal(0, givenOnly[0].Remaining);
    }

    [Fact]
    public async Task Repay_WithAReceipt_AttachesItToTheRepaymentDocument()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Given);

        var repaymentId = await RepayAsync(loanId, f.Bank, 100_000, new DateOnly(2026, 5, 1), Receipt("pay.png", PngBytes));

        await using var context = _db.NewContext();
        var attachments = await NewAttachmentService(context).ListAsync(AttachmentEntityType.Transaction, repaymentId);

        Assert.Single(attachments);
    }

    // ---------------------------------------------------------------
    // حذف
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_IsBlockedWhileRepaymentsExist_ThenAllowed()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Given);
        var repaymentId = await RepayAsync(loanId, f.Bank, 100_000, new DateOnly(2026, 5, 1));

        await using (var context = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).DeleteAsync(loanId, TenantA));
            Assert.Contains("بازپرداخت", ex.Message);
        }

        await using (var context = _db.NewContext())
            await NewService(context).DeleteRepaymentAsync(repaymentId, TenantA);

        await using (var context = _db.NewContext())
            await NewService(context).DeleteAsync(loanId, TenantA);

        await using var verify = _db.NewContext();
        Assert.Null(await NewService(verify).GetAsync(loanId));
        Assert.Empty(await verify.Transactions.ToListAsync());
        Assert.Equal(0, await ContactBalanceAsync(f.Contact));
    }

    [Fact]
    public async Task DeleteRepayment_OnlyAcceptsRepaymentDocuments()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Given);

        // شناسه‌ی خودِ قرض، بازپرداخت نیست
        await using var context = _db.NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(context).DeleteRepaymentAsync(loanId, TenantA));
    }

    // ---------------------------------------------------------------
    // سال مالی و جداسازی شرکت‌ها
    // ---------------------------------------------------------------

    [Fact]
    public async Task ClosedFiscalYear_BlocksCreatingAndRepayingLoans()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Given);

        // سال مالی تست (2025-03-21 تا 2027-03-20) را می‌بندیم؛ سند پیش‌نویسی در آن نیست
        await using (var context = _db.NewContext())
        {
            var year = await context.FiscalYears.SingleAsync();
            await new FiscalYearService(context, _db.User, _time).CloseAsync(year.Id, TenantA);
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(f, LoanDirection.Given));
        Assert.Contains("بسته", ex.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RepayAsync(loanId, f.Bank, 100_000, new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public async Task AnotherTenantsLoans_AreInvisible_AndCannotBeRepaidOrDeleted()
    {
        var f = await ArrangeAsync();
        var loanId = await CreateAsync(f, LoanDirection.Given);

        _db.Tenant.TenantId = TenantB;

        await using var context = _db.NewContext();
        var service = NewService(context);

        Assert.Empty(await service.GetLoansAsync(null, onlyOpen: false));
        Assert.Null(await service.GetAsync(loanId));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RepayAsync(loanId, new LoanRepaymentRequest { AccountId = f.Bank, Amount = 1, Date = new DateOnly(2026, 5, 1) }, TenantB));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(loanId, TenantB));
    }

    // ---------------------------------------------------------------
    // Arrange / Helpers
    // ---------------------------------------------------------------

    private sealed record Fixture(int Contact, int Bank, int Cash, int Expense);

    private async Task<Fixture> ArrangeAsync()
    {
        var contact = await AddContactAsync();
        var bank = await AddAccountAsync("بانک ملت", AccountType.Bank);
        var cash = await AddAccountAsync("صندوق", AccountType.Cash);
        var expense = await AddAccountAsync("هزینه", AccountType.Expense);

        return new Fixture(contact, bank, cash, expense);
    }

    private LoanRequest NewRequest(Fixture f, LoanDirection direction)
        => new()
        {
            Direction = direction,
            ContactId = f.Contact,
            AccountId = f.Bank,
            Amount = Amount,
            Date = new DateOnly(2026, 4, 1),
            Description = "تست"
        };

    private Task<int> CreateAsync(Fixture f, LoanDirection direction) => CreateAsync(NewRequest(f, direction));

    private async Task<int> CreateAsync(LoanRequest request, AttachmentUpload? receipt = null)
    {
        await using var context = _db.NewContext();
        return await NewService(context).CreateAsync(request, TenantA, receipt);
    }

    private async Task<int> NewLoanAsync(int contactId, int accountId, LoanDirection direction)
    {
        return await CreateAsync(new LoanRequest
        {
            Direction = direction,
            ContactId = contactId,
            AccountId = accountId,
            Amount = Amount,
            Date = new DateOnly(2026, 4, 1)
        });
    }

    private async Task<int> RepayAsync(int loanId, int accountId, decimal amount, DateOnly date, AttachmentUpload? receipt = null)
    {
        await using var context = _db.NewContext();
        return await NewService(context).RepayAsync(
            loanId,
            new LoanRepaymentRequest { AccountId = accountId, Amount = amount, Date = date },
            TenantA,
            receipt);
    }

    private static AttachmentUpload Receipt(string fileName, byte[] content)
        => new() { FileName = fileName, Length = content.Length, Content = new MemoryStream(content) };

    private AttachmentService NewAttachmentService(ApplicationDbContext context)
        => new(context, _db.Tenant, new LocalAttachmentStore(Options.Create(_options)), Options.Create(_options));

    private LoanService NewService(ApplicationDbContext context)
        => new(context, _db.Tenant, new TransactionService(context), new SystemAccountService(context),
            NewAttachmentService(context));

    private async Task<int> AddContactAsync()
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = "مشتری", ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private async Task<int> AddAccountAsync(string name, AccountType type)
    {
        await using var context = _db.NewContext();

        var category = new AccountCategory { Name = "دسته " + name, DefaultAccountType = type, IsActive = true };
        context.AccountCategories.Add(category);
        await context.SaveChangesAsync();

        var account = new Account { Name = name, AccountType = type, AccountCategoryId = category.Id, IsActive = true };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return account.Id;
    }

    private async Task<decimal> ContactBalanceAsync(int contactId)
    {
        await using var context = _db.NewContext();
        var lines = await context.TransactionLines.Where(l => l.ContactId == contactId).ToListAsync();
        return lines.Sum(l => l.Debit - l.Credit);
    }

    private async Task<decimal> AccountBalanceAsync(int accountId)
    {
        await using var context = _db.NewContext();
        var lines = await context.TransactionLines.Where(l => l.AccountId == accountId).ToListAsync();
        return lines.Sum(l => l.Debit - l.Credit);
    }
}
