using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class SystemSmsService : ISystemSmsService
{
    private const string ProtectorPurpose = "MyFirstCoreApp.SystemSmsSettings.ApiKey.v1";
    private const string Endpoint = "https://api.iranpayamak.com/ws/v1/sms/pattern";
    private readonly ApplicationDbContext _context;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly HttpClient _httpClient;

    public SystemSmsService(ApplicationDbContext context, IDataProtectionProvider dataProtection,
        HttpClient httpClient)
    {
        _context = context;
        _dataProtection = dataProtection;
        _httpClient = httpClient;
    }

    public async Task<SystemSmsSettingsSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.SystemSmsSettings.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Id)
            .Select(x => new { x.ProtectedApiKey, x.LineNumber, x.RegistrationSmsEnabled, x.RegistrationSmsPatternCode })
            .FirstOrDefaultAsync(cancellationToken);

        return setting is null
            ? new SystemSmsSettingsSummary(false, "90008361", false, null)
            : new SystemSmsSettingsSummary(!string.IsNullOrWhiteSpace(setting.ProtectedApiKey),
                setting.LineNumber, setting.RegistrationSmsEnabled, setting.RegistrationSmsPatternCode);
    }

    public async Task SaveAsync(string? apiKey, bool clearApiKey, string lineNumber,
        bool registrationSmsEnabled, string? registrationSmsPatternCode, string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        lineNumber = lineNumber.Trim();
        registrationSmsPatternCode = registrationSmsPatternCode?.Trim();
        if (string.IsNullOrWhiteSpace(lineNumber) || lineNumber.Length > 50)
            throw new InvalidOperationException("شماره خط پیامک را وارد کنید.");
        if (registrationSmsPatternCode?.Length > 100)
            throw new InvalidOperationException("کد الگوی پیامک بیش از حد مجاز است.");
        if (registrationSmsEnabled && string.IsNullOrWhiteSpace(registrationSmsPatternCode))
            throw new InvalidOperationException("برای فعال‌سازی تأیید موبایل، کد الگوی پیامک لازم است.");
        if (clearApiKey && !string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("برای جایگزینی کلید، گزینه پاک‌کردن را خالی بگذارید.");

        var setting = await _context.SystemSmsSettings.IgnoreQueryFilters()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (setting is null)
        {
            setting = new SystemSmsSetting { CreatedBy = updatedBy };
            _context.SystemSmsSettings.Add(setting);
        }

        if (clearApiKey) setting.ProtectedApiKey = null;
        else if (!string.IsNullOrWhiteSpace(apiKey))
            setting.ProtectedApiKey = _dataProtection.CreateProtector(ProtectorPurpose).Protect(apiKey.Trim());

        setting.LineNumber = lineNumber;
        setting.RegistrationSmsEnabled = registrationSmsEnabled;
        setting.RegistrationSmsPatternCode = string.IsNullOrWhiteSpace(registrationSmsPatternCode) ? null : registrationSmsPatternCode;
        setting.UpdatedBy = updatedBy;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SystemSmsSendResult> SendRegistrationCodeAsync(string mobile, string code,
        CancellationToken cancellationToken = default)
    {
        var setting = await _context.SystemSmsSettings.AsNoTracking()
            .Where(x => !x.IsDeleted && x.RegistrationSmsEnabled && x.ProtectedApiKey != null)
            .OrderBy(x => x.Id)
            .Select(x => new { x.ProtectedApiKey, x.LineNumber, x.RegistrationSmsPatternCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (setting?.ProtectedApiKey is null || string.IsNullOrWhiteSpace(setting.RegistrationSmsPatternCode))
            return new(false, null, "ارسال پیامک تأیید ثبت‌نام در تنظیمات سامانه فعال نشده است.");

        mobile = NormalizeDigits(mobile.Trim());
        if (!Regex.IsMatch(mobile, "^09[0-9]{9}$"))
            return new(false, null, "شماره موبایل باید ۱۱ رقم و با 09 شروع شود.");

        string apiKey;
        try { apiKey = _dataProtection.CreateProtector(ProtectorPurpose).Unprotect(setting.ProtectedApiKey); }
        catch (CryptographicException) { return new(false, null, "کلید پیامک سامانه قابل خواندن نیست؛ آن را دوباره ثبت کنید."); }

        var payload = new
        {
            code = setting.RegistrationSmsPatternCode,
            attributes = new Dictionary<string, string> { ["var1"] = code },
            recipient = mobile,
            line_number = setting.LineNumber,
            number_format = "english"
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.TryAddWithoutValidation("Api-Key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var accepted = response.IsSuccessStatusCode && root.TryGetProperty("status", out var status) &&
                string.Equals(status.ToString(), "success", StringComparison.OrdinalIgnoreCase);
            var message = root.TryGetProperty("message", out var messageElement) ? messageElement.ToString() : null;
            string? requestId = null;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("id", out var id)) requestId = id.ToString();
            return new(accepted, requestId, accepted ? "کد تأیید به سامانه پیامک تحویل داده شد."
                : message ?? $"ارسال پیامک تأیید ناموفق بود (HTTP {(int)response.StatusCode}).");
        }
        catch (HttpRequestException) { return new(false, null, "ارتباط با سرویس پیامک برقرار نشد."); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, null, "مهلت ارتباط با سرویس پیامک به پایان رسید."); }
        catch (JsonException) { return new(false, null, "پاسخ سرویس پیامک معتبر نیست."); }
    }

    private static string NormalizeDigits(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= '\u06F0' and <= '\u06F9') result.Append((char)('0' + character - '\u06F0'));
            else if (character is >= '\u0660' and <= '\u0669') result.Append((char)('0' + character - '\u0660'));
            else result.Append(character);
        }
        return result.ToString();
    }
}
