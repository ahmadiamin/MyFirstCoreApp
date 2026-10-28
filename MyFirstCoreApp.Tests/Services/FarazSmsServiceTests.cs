using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class FarazSmsServiceTests : IDisposable
{
    private const int TenantId = 1;
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SendPortalLink_UsesKeywordsContractAndStoresRequestIdForDeliveryCheck()
    {
        _db.Tenant.TenantId = TenantId;
        int contactId;
        int shareId;
        await using (var setup = _db.NewContext())
        {
            setup.Tenants.Add(new Tenant { Id = TenantId, Name = "شرکت تست", IsActive = true });
            var contact = new Contact
            {
                TenantId = TenantId, Name = "مشتری تست", Mobile = "۰۹۱۲۳۴۵۶۷۸۹",
                ContactType = MyFirstCoreApp.Domain.Enums.ContactType.Customer, IsActive = true
            };
            setup.Contacts.Add(contact);
            await setup.SaveChangesAsync();
            contactId = contact.Id;
            var share = new CustomerPortalShare
            {
                TenantId = TenantId, ContactId = contactId, TokenHash = new string('A', 64),
                ProtectedToken = "protected", CanViewLedger = true, CanViewSalesInvoices = true
            };
            setup.CustomerPortalShares.Add(share);
            await setup.SaveChangesAsync();
            shareId = share.Id;
        }

        var handler = new RecordingHandler(
            "{\"status\":\"success\",\"message\":\"in-queue\",\"data\":{\"id\":12345}}",
            "{\"data\":[{\"mobile\":\"09123456789\",\"status\":\"delivered\"}]}");
        using var client = new HttpClient(handler);
        await using var context = _db.NewContext();
        var dataProtection = new EphemeralDataProtectionProvider();
        var smsSettings = new TenantSmsSettingsService(context, dataProtection);
        await smsSettings.SaveAsync(TenantId, "test-key", false, "90008361",
            "لینک پرتال مشتری: %var1%", "tester");
        var storedSettings = await context.TenantSmsSettings.AsNoTracking().SingleAsync(x => x.TenantId == TenantId);
        Assert.NotEqual("test-key", storedSettings.ProtectedApiKey);
        Assert.True((await smsSettings.GetSummaryAsync(TenantId)).ApiKeyConfigured);
        Assert.Equal("test-key", (await smsSettings.GetForSendingAsync(TenantId))?.ApiKey);
        var service = new FarazSmsService(client, context, smsSettings, TimeProvider.System);

        var sent = await service.SendPortalLinkAsync(TenantId, contactId, shareId,
            "۰۹۱۲۳۴۵۶۷۸۹", "https://example.test/customer-portal/abc123", "tester");

        Assert.True(sent.IsAccepted);
        Assert.Equal("12345", sent.RequestId);
        var post = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("https://api.iranpayamak.com/ws/v1/sms/keywords", post.Uri);
        Assert.Equal("test-key", post.ApiKey);
        using (var json = System.Text.Json.JsonDocument.Parse(post.Body))
        {
            var root = json.RootElement;
            Assert.Equal("لینک پرتال مشتری: %var1%", root.GetProperty("text").GetString());
            Assert.Equal("90008361", root.GetProperty("line_number").GetString());
            Assert.Equal("english", root.GetProperty("number_format").GetString());
            Assert.False(root.TryGetProperty("schedule", out _));
            var recipient = Assert.Single(root.GetProperty("recipients").EnumerateArray());
            Assert.Equal("09123456789", recipient.GetProperty("mobile").GetString());
            Assert.Equal("https://example.test/customer-portal/abc123", recipient.GetProperty("var1").GetString());
        }

        var stored = await context.CustomerPortalShares.SingleAsync(x => x.Id == shareId);
        Assert.Equal("12345", stored.LastSmsRequestId);
        Assert.NotNull(stored.LastSmsSentAtUtc);

        var report = await service.GetLatestDeliveryReportAsync(TenantId, contactId, shareId);
        Assert.NotNull(report);
        Assert.Equal("12345", report.RequestId);
        Assert.Contains("delivered", report.ResponseJson);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("https://api.iranpayamak.com/ws/v1/send_request/12345/items", handler.Requests[1].Uri);
        Assert.All(handler.Requests, request => Assert.Equal("test-key", request.ApiKey));
    }

    [Fact]
    public async Task SendChequeReminder_UsesConfiguredPatternAndIranLocalSchedule()
    {
        _db.Tenant.TenantId = TenantId;
        await using (var setup = _db.NewContext())
        {
            setup.Tenants.Add(new Tenant { Id = TenantId, Name = "test", IsActive = true });
            await setup.SaveChangesAsync();
        }

        var handler = new RecordingHandler("{\"status\":\"success\",\"message\":\"in-queue\",\"data\":{\"id\":987}} ");
        using var client = new HttpClient(handler);
        await using var context = _db.NewContext();
        var smsSettings = new TenantSmsSettingsService(context, new EphemeralDataProtectionProvider());
        await smsSettings.SaveAsync(TenantId, "test-key", false, "90008361", null, "tester",
            chequeReminderEnabled: true,
            chequeReminderDayBeforeEnabled: true,
            chequeReminderDueDayEnabled: true,
            chequeReminderTime: new TimeOnly(9, 0),
            chequeReminderMobile: "09123456789",
            chequeReminderText: "چک %var1% مبلغ %var2% سررسید %var3% %var4%");
        var service = new FarazSmsService(client, context, smsSettings, TimeProvider.System);
        var scheduledFor = new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Unspecified);

        var sent = await service.SendChequeReminderAsync(TenantId, "09123456789", "CHK-1", 125000,
            new DateOnly(2026, 10, 28), "Bank account", "Day before", scheduledFor);

        Assert.True(sent.IsAccepted);
        using var json = System.Text.Json.JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        var root = json.RootElement;
        Assert.Equal("2026-10-27 09:00:00", root.GetProperty("schedule").GetString());
        Assert.Equal("persian", root.GetProperty("number_format").GetString());
        var recipient = Assert.Single(root.GetProperty("recipients").EnumerateArray());
        Assert.Equal("CHK-1", recipient.GetProperty("var1").GetString());
        Assert.False(string.IsNullOrWhiteSpace(recipient.GetProperty("var2").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(recipient.GetProperty("var3").GetString()));
        Assert.Equal("Bank account - Day before", recipient.GetProperty("var4").GetString());
    }

    private sealed class RecordingHandler(params string[] responseBodies) : HttpMessageHandler
    {
        private int _responseIndex;
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty :
                await request.Content.ReadAsStringAsync(cancellationToken);
            var apiKey = request.Headers.TryGetValues("Api-Key", out var values) ? values.Single() : null;
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.ToString(), body, apiKey));
            var content = responseBodies[_responseIndex++];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string Body, string? ApiKey);
}
