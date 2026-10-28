using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class SystemSmsServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RegistrationCode_UsesProtectedSystemKeyAndVar1()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        await using var context = _db.NewContext();
        var service = new SystemSmsService(context, new EphemeralDataProtectionProvider(), client);

        await service.SaveAsync("system-test-key", false, "90008361", true,
            "pattern-otp-123", "test-user");

        var stored = await context.SystemSmsSettings.AsNoTracking().SingleAsync();
        Assert.NotEqual("system-test-key", stored.ProtectedApiKey);
        Assert.True((await service.GetSummaryAsync()).ApiKeyConfigured);

        var result = await service.SendRegistrationCodeAsync("۰۹۱۲۳۴۵۶۷۸۹", "654321");

        Assert.True(result.IsAccepted);
        Assert.Equal("req-1", result.RequestId);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.iranpayamak.com/ws/v1/sms/pattern", request.Uri);
        Assert.Equal("system-test-key", request.ApiKey);
        using var document = JsonDocument.Parse(request.Body);
        var root = document.RootElement;
        Assert.Equal("pattern-otp-123", root.GetProperty("code").GetString());
        Assert.Equal("english", root.GetProperty("number_format").GetString());
        Assert.Equal("09123456789", root.GetProperty("recipient").GetString());
        Assert.Equal("654321", root.GetProperty("attributes").GetProperty("var1").GetString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Uri, string Body, string? ApiKey)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var apiKey = request.Headers.TryGetValues("Api-Key", out var values) ? values.Single() : null;
            Requests.Add((request.RequestUri!.ToString(), body, apiKey));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"success\",\"message\":\"in-queue\",\"data\":{\"id\":\"req-1\"}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
