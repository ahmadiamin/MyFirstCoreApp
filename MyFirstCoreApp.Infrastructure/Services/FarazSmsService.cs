using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class FarazSmsService : IFarazSmsService
{
    private const string BaseAddress = "https://api.iranpayamak.com/ws/v1/";
    private const string LinkVariable = "%var1%";
    private static readonly SemaphoreSlim SmsRequestGate = new(1, 1);
    private static DateTimeOffset _nextSmsRequestUtc = DateTimeOffset.MinValue;
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly ITenantSmsSettingsService _settings;
    private readonly TimeProvider _timeProvider;

    public FarazSmsService(HttpClient httpClient, ApplicationDbContext context,
        ITenantSmsSettingsService settings, TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _context = context;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public async Task<FarazSmsSendResult> SendPortalLinkAsync(
        int tenantId, int contactId, int shareId, string mobile, string portalUrl, string? sentBy,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetForSendingAsync(tenantId, cancellationToken);
        if (settings is null) return new(false, null, "تنظیمات پیامک این شرکت ثبت نشده یا کلید آن قابل رمزگشایی نیست.");
        var configurationError = ValidateConfiguration(settings);
        if (configurationError is not null) return new(false, null, configurationError);

        mobile = NormalizeMobile(mobile);
        if (!System.Text.RegularExpressions.Regex.IsMatch(mobile, "^09[0-9]{9}$"))
            return new(false, null, "شماره موبایل مشتری باید ۱۱ رقم و با 09 شروع شود.");
        if (!Uri.TryCreate(portalUrl, UriKind.Absolute, out var linkUri) ||
            (linkUri.Scheme != Uri.UriSchemeHttps && linkUri.Scheme != Uri.UriSchemeHttp))
            return new(false, null, "لینک پرتال معتبر نیست.");
        if (linkUri.AbsoluteUri.Length > 500)
            return new(false, null, "طول لینک از حد مجاز الگوی پیامک بیشتر است.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var share = await _context.CustomerPortalShares.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == shareId && x.TenantId == tenantId &&
                x.ContactId == contactId && !x.IsDeleted && !x.RevokedAtUtc.HasValue &&
                (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now), cancellationToken);
        if (share is null) return new(false, null, "لینک فعال پرتال مشتری پیدا نشد.");

        var recipient = new Dictionary<string, string>
        {
            ["mobile"] = mobile,
            ["var1"] = linkUri.AbsoluteUri
        };
        var payload = new
        {
            text = settings.PortalLinkText,
            line_number = settings.LineNumber,
            recipients = new[] { recipient },
            number_format = "english"
        };

        try
        {
            await WaitForSmsRequestSlotAsync(cancellationToken);
            using var request = CreateRequest(HttpMethod.Post, "sms/keywords", settings.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var (accepted, requestId, providerMessage) = ParseSendResponse(responseBody);
            if (!response.IsSuccessStatusCode || !accepted)
            {
                var status = (int)response.StatusCode;
                return new(false, requestId, providerMessage ?? $"ارسال پیامک پذیرفته نشد (HTTP {status}).");
            }

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                if (requestId.Length > 100)
                    return new(true, null, "درخواست پذیرفته شد؛ شناسه پاسخ برای پیگیری قابل ذخیره نیست.");
                share.LastSmsRequestId = requestId;
                share.LastSmsSentAtUtc = now;
                share.UpdatedBy = sentBy;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return new(true, requestId,
                requestId is null
                    ? "درخواست پذیرفته شد، اما شناسه پیگیری در پاسخ سرویس وجود نداشت."
                    : "درخواست ارسال پذیرفته شد و در صف پیامک قرار گرفت.");
        }
        catch (HttpRequestException)
        {
            return new(false, null, "ارتباط با سرویس پیامک برقرار نشد.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "مهلت ارتباط با سرویس پیامک به پایان رسید.");
        }
    }

    public async Task<FarazSmsSendResult> SendChequeReminderAsync(
        int tenantId, string mobile, string chequeNumber, decimal amount, DateOnly dueDate,
        string bankAccountName, string reminderKind, DateTime? scheduleIranLocal,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetForSendingAsync(tenantId, cancellationToken);
        if (settings is null)
            return new(false, null, "SMS settings or API key are unavailable.");
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.LineNumber))
            return new(false, null, "SMS API key or sender line is missing.");
        var template = settings.ChequeReminderText;
        if (string.IsNullOrWhiteSpace(template) ||
            new[] { "%var1%", "%var2%", "%var3%", "%var4%" }
                .Any(token => !template.Contains(token, StringComparison.Ordinal)))
            return new(false, null, "Cheque reminder SMS template is missing required variables.");

        mobile = NormalizeMobile(mobile);
        if (!System.Text.RegularExpressions.Regex.IsMatch(mobile, "^09[0-9]{9}$"))
            return new(false, null, "Contact mobile must be 11 digits beginning with 09.");

        var recipient = new Dictionary<string, string>
        {
            ["mobile"] = mobile,
            ["var1"] = chequeNumber,
            ["var2"] = amount.ToString("N0", CultureInfo.GetCultureInfo("fa-IR")),
            ["var3"] = PersianDate.Format(dueDate),
            ["var4"] = $"{bankAccountName} - {reminderKind}"
        };
        var payload = new Dictionary<string, object>
        {
            ["text"] = template,
            ["line_number"] = settings.LineNumber,
            ["recipients"] = new[] { recipient },
            ["number_format"] = "persian"
        };
        if (scheduleIranLocal.HasValue)
            payload["schedule"] = scheduleIranLocal.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        try
        {
            await WaitForSmsRequestSlotAsync(cancellationToken);
            using var request = CreateRequest(HttpMethod.Post, "sms/keywords", settings.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var (accepted, requestId, providerMessage) = ParseSendResponse(responseBody);
            if (!response.IsSuccessStatusCode || !accepted)
                return new(false, requestId, providerMessage ?? $"SMS request was rejected (HTTP {(int)response.StatusCode}).");
            return new(true, requestId, "Cheque reminder accepted by FarazSMS.");
        }
        catch (HttpRequestException)
        {
            return new(false, null, "Could not connect to the SMS service.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "SMS service request timed out.");
        }
    }

    public async Task<FarazSmsDeliveryReport?> GetLatestDeliveryReportAsync(
        int tenantId, int contactId, int shareId, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetForSendingAsync(tenantId, cancellationToken);
        if (settings is null)
            return new(string.Empty, false, "{}", "تنظیمات پیامک این شرکت ثبت نشده یا کلید آن قابل رمزگشایی نیست.");
        var configurationError = ValidateConfiguration(settings);
        if (configurationError is not null)
            return new(string.Empty, false, "{}", configurationError);

        var requestId = await _context.CustomerPortalShares.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == shareId && x.TenantId == tenantId && x.ContactId == contactId && !x.IsDeleted)
            .Select(x => x.LastSmsRequestId)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestId)) return null;

        try
        {
            await WaitForSmsRequestSlotAsync(cancellationToken);
            using var request = CreateRequest(HttpMethod.Get,
                $"send_request/{Uri.EscapeDataString(requestId)}/items", settings.ApiKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            if (responseJson.Length > 20_000) responseJson = responseJson[..20_000];
            return new(requestId, response.IsSuccessStatusCode, responseJson,
                response.IsSuccessStatusCode
                    ? "گزارش وضعیت تحویل از سرویس دریافت شد."
                    : $"دریافت گزارش وضعیت ناموفق بود (HTTP {(int)response.StatusCode}).");
        }
        catch (HttpRequestException)
        {
            return new(requestId, false, "{}", "ارتباط با سرویس پیامک برقرار نشد.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(requestId, false, "{}", "مهلت ارتباط با سرویس پیامک به پایان رسید.");
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string apiKey)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(BaseAddress), relativePath));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Api-Key", apiKey);
        return request;
    }

    private async Task WaitForSmsRequestSlotAsync(CancellationToken cancellationToken)
    {
        await SmsRequestGate.WaitAsync(cancellationToken);
        try
        {
            var wait = _nextSmsRequestUtc - _timeProvider.GetUtcNow();
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, cancellationToken);
            _nextSmsRequestUtc = _timeProvider.GetUtcNow().AddMilliseconds(1050);
        }
        finally
        {
            SmsRequestGate.Release();
        }
    }

    private static string? ValidateConfiguration(TenantSmsConfiguration settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return "کلید API پیامک در تنظیمات شرکت ثبت نشده است.";
        if (string.IsNullOrWhiteSpace(settings.LineNumber))
            return "شماره خط ارسال پیامک تنظیم نشده است.";
        if (string.IsNullOrWhiteSpace(settings.PortalLinkText) ||
            !settings.PortalLinkText.Contains(LinkVariable, StringComparison.Ordinal))
            return "الگوی پیامک باید در تنظیمات شرکت و پنل FarazSMS یکسان بوده و شامل %var1% باشد.";
        return null;
    }

    private static (bool Accepted, string? RequestId, string? Message) ParseSendResponse(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var accepted = root.TryGetProperty("status", out var status) &&
                           string.Equals(status.ToString(), "success", StringComparison.OrdinalIgnoreCase);
            var message = root.TryGetProperty("message", out var messageElement) ? messageElement.ToString() : null;
            string? requestId = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("id", out var id) && id.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                requestId = id.ToString();
            return (accepted, requestId, message);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return (false, null, null);
        }
    }

    private static string NormalizeMobile(string mobile)
    {
        var result = new StringBuilder(mobile.Length);
        foreach (var character in mobile.Trim())
        {
            if (character is >= '۰' and <= '۹') result.Append((char)('0' + character - '۰'));
            else if (character is >= '٠' and <= '٩') result.Append((char)('0' + character - '٠'));
            else result.Append(character);
        }
        return result.ToString();
    }
}
