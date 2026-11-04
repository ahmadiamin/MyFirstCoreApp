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
    private static readonly SemaphoreSlim SmsRequestGate = new(1, 1);
    private static DateTimeOffset _nextSmsRequestUtc = DateTimeOffset.MinValue;
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly ITenantSmsSettingsService _settings;
    private readonly TimeProvider _timeProvider;
    private readonly IFarazSmsBalanceService _balanceService;

    public FarazSmsService(HttpClient httpClient, ApplicationDbContext context,
        ITenantSmsSettingsService settings, TimeProvider timeProvider, IFarazSmsBalanceService balanceService)
    {
        _httpClient = httpClient;
        _context = context;
        _settings = settings;
        _timeProvider = timeProvider;
        _balanceService = balanceService;
    }

    public async Task<FarazSmsSendResult> SendPortalLinkAsync(
        int tenantId, int contactId, int shareId, string mobile, string token, string? sentBy,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetForSendingAsync(tenantId, cancellationToken);
        if (settings is null)
            return new(false, null, "تنظیمات پیامک این شرکت ثبت نشده یا کلید آن قابل رمزگشایی نیست.");
        
        if (string.IsNullOrWhiteSpace(settings.PortalLinkPatternCode))
            return new(false, null, "کد الگوی پیامک لینک پرتال ثبت نشده است.");

        var configurationError = ValidateConfiguration(settings);
        if (configurationError is not null) return new(false, null, configurationError);

        mobile = NormalizeMobile(mobile);
        if (!System.Text.RegularExpressions.Regex.IsMatch(mobile, "^09[0-9]{9}$"))
            return new(false, null, "شماره موبایل مشتری باید ۱۱ رقم و با 09 شروع شود.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var share = await _context.CustomerPortalShares.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == shareId && x.TenantId == tenantId &&
                x.ContactId == contactId && !x.IsDeleted && !x.RevokedAtUtc.HasValue &&
                (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now), cancellationToken);
        if (share is null) return new(false, null, "لینک فعال پرتال مشتری پیدا نشد.");

        var recipient = new Dictionary<string, string>
        {
            ["mobile"] = mobile,
            ["code"] = token
        };

        var payload = new Dictionary<string, object>
        {
            ["code"] = settings.PortalLinkPatternCode.Trim(),
            ["attributes"] = new Dictionary<string, string>
            {
                ["var1"] = token
            },
            ["recipient"] = mobile,
            ["line_number"] = settings.LineNumber,
            ["number_format"] = "english"
        };

        try
        {
            await WaitForSmsRequestSlotAsync(cancellationToken);
            using var request = CreateRequest(HttpMethod.Post, "sms/pattern", settings.ApiKey);
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
            return new(false, null, "تنظیمات پیامک این شرکت در دسترس نیست.");

        if (string.IsNullOrWhiteSpace(settings.ApiKey) ||
            string.IsNullOrWhiteSpace(settings.LineNumber))
            return new(false, null, "کلید API پیامک یا خط فرستنده موجود نیست.");

        if (string.IsNullOrWhiteSpace(settings.ChequeReminderPatternCode))
            return new(false, null, "شناسه الگوی یادآوری چک ثبت نشده است.");

        mobile = NormalizeMobile(mobile);

        if (!System.Text.RegularExpressions.Regex.IsMatch(mobile, "^09[0-9]{9}$"))
            return new(false, null, "شماره موبایل تماس باید ۱۱ رقمی باشد و با ۰۹ شروع شود.");

        var attributes = new Dictionary<string, string>
        {
            ["var1"] = chequeNumber,
            ["var2"] = amount.ToString("N0", CultureInfo.GetCultureInfo("fa-IR")),
            ["var3"] = PersianDate.Format(dueDate),
            ["var4"] = $"{bankAccountName} - {reminderKind}"
        };

        var payload = new Dictionary<string, object>
        {
            ["code"] = settings.ChequeReminderPatternCode,
            ["attributes"] = attributes,
            ["recipient"] = mobile,
            ["line_number"] = settings.LineNumber,
            ["number_format"] = "persian"
        };

        if (scheduleIranLocal.HasValue)
        {
            payload["schedule"] = scheduleIranLocal.Value.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture);
        }

        try
        {
            await WaitForSmsRequestSlotAsync(cancellationToken);

            using var request = CreateRequest(
                HttpMethod.Post, "sms/pattern", settings.ApiKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var (accepted, requestId, providerMessage) = ParseSendResponse(responseBody);

            if (!response.IsSuccessStatusCode || !accepted)
            {
                return new(
                    false,
                    requestId,
                    providerMessage ?? $"درخواست پیامک رد شد (HTTP {(int)response.StatusCode}).");
            }

            return new(true, requestId, "یادآوری چک توسط FarazSMS پذیرفته شد.");
        }
        catch (HttpRequestException)
        {
            return new(false, null, "امکان اتصال به سرویس پیامک وجود نداشت.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "مهلت درخواست سرویس پیامک به پایان رسید.");
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
        return null;
    }

    private static (bool Accepted, string? RequestId, string? Message)
        ParseSendResponse(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var accepted = root.TryGetProperty("status", out var status) &&
                string.Equals(status.ToString(), "success", StringComparison.OrdinalIgnoreCase);

            var message =
                root.TryGetProperty("messages", out var messages)
                    ? messages.ToString()
                    : root.TryGetProperty("message", out var messageElement)
                        ? messageElement.ToString()
                        : null;

            string? requestId = null;

            if (root.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty("id", out var id))
                {
                    requestId = id.ToString();
                }
                else if (data.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    requestId = data.ToString();
                }
            }

            return (accepted, requestId, message);
        }
        catch (JsonException)
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

    public async Task<SmsBalanceResult> GetBalanceAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetForSendingAsync(tenantId, cancellationToken);

        if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new(false, null, null, "تنظیمات پیامک شرکت یا کلید API آن در دسترس نیست.");
        }

        return await _balanceService.GetBalanceAsync(settings.ApiKey, cancellationToken);
    }
}