using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class ChequeReminderHostedServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SendsIssuedChequeReminderToCompanyMobileWithoutContact()
    {
        const int tenantId = 1;
        _db.Tenant.TenantId = tenantId;
        var bankAccountId = await _db.AddAccountAsync(tenantId, "Bank account");

        await using (var setup = _db.NewContext())
        {
            setup.Tenants.Add(new Tenant { Id = tenantId, Name = "Test company", IsActive = true });
            setup.TenantSmsSettings.Add(new TenantSmsSetting
            {
                TenantId = tenantId,
                LineNumber = "90008361",
                ChequeReminderEnabled = true,
                ChequeReminderDayBeforeEnabled = true,
                ChequeReminderTime = new TimeOnly(9, 0),
                ChequeReminderMobile = "09123456789",
                ChequeReminderText = "Pattern %var1% %var2% %var3% %var4%"
            });
            setup.Cheques.Add(new Cheque
            {
                TenantId = tenantId,
                ChequeNumber = "CHK-1",
                Direction = ChequeDirection.Issued,
                Status = ChequeStatus.InProgress,
                Amount = 125000,
                IssueDate = new DateOnly(2026, 10, 20),
                DueDate = new DateOnly(2026, 10, 27),
                BankAccountId = bankAccountId,
                ContactId = null
            });
            await setup.SaveChangesAsync();
        }

        var companySms = new RecordingSmsService();
        var fixedTime = new FixedIranTimeProvider(
            new DateTimeOffset(2026, 10, 26, 4, 30, 0, TimeSpan.Zero));
        await using var workerContext = _db.NewContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ApplicationDbContext>(_ => workerContext);
        services.AddScoped<IFarazSmsService>(_ => companySms);
        using var provider = services.BuildServiceProvider();
        var worker = new ChequeReminderHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(), fixedTime,
            NullLogger<ChequeReminderHostedService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var sent = await companySms.Sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("09123456789", sent.Mobile);
            Assert.Equal("CHK-1", sent.ChequeNumber);
            Assert.Equal("Bank account", sent.BankAccountName);
            Assert.Contains("\u0686\u06A9 \u067E\u0631\u062F\u0627\u062E\u062A\u06CC", sent.ReminderKind);
            Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0), sent.ScheduleIranLocal);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var verification = _db.NewContext();
        var dispatch = Assert.Single(verification.ChequeSmsReminderDispatches);
        Assert.Equal("09123456789", dispatch.RecipientMobile);
        Assert.Equal(ChequeSmsReminderStatus.Accepted, dispatch.Status);
    }

    private sealed class FixedIranTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly TimeZoneInfo _iranZone = new IranTimeProvider().LocalTimeZone;
        public override TimeZoneInfo LocalTimeZone => _iranZone;
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingSmsService : IFarazSmsService
    {
        public TaskCompletionSource<SentReminder> Sent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<FarazSmsSendResult> SendChequeReminderAsync(int tenantId, string mobile,
            string chequeNumber, decimal amount, DateOnly dueDate, string bankAccountName,
            string reminderKind, DateTime? scheduleIranLocal, CancellationToken cancellationToken = default)
        {
            Sent.TrySetResult(new SentReminder(mobile, chequeNumber, bankAccountName,
                reminderKind, scheduleIranLocal));
            return Task.FromResult(new FarazSmsSendResult(true, "request-1", "accepted"));
        }

        public Task<FarazSmsSendResult> SendPortalLinkAsync(int tenantId, int contactId, int shareId,
            string mobile, string portalUrl, string? sentBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FarazSmsDeliveryReport?> GetLatestDeliveryReportAsync(int tenantId, int contactId,
            int shareId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed record SentReminder(string Mobile, string ChequeNumber, string BankAccountName,
        string ReminderKind, DateTime? ScheduleIranLocal);
}
