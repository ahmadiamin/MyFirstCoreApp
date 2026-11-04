using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class SystemSmsService : ISystemSmsService
{
    private const string Endpoint = "https://api.iranpayamak.com/ws/v1/sms/pattern";

    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IFarazSmsBalanceService _balanceService;
    public SystemSmsService(
        ApplicationDbContext context,
        HttpClient httpClient,
        IFarazSmsBalanceService balanceService)
    {
        _context = context;
        _httpClient = httpClient;
        _balanceService = balanceService;
    }

    public async Task<SystemSmsSettingsSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var setting = await _context.SystemSmsSettings
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.ProtectedApiKey,
                x.LineNumber,
                x.RegistrationSmsEnabled,
                x.RegistrationSmsPatternCode,
                x.PasswordRecoverySmsEnabled,
                x.PasswordRecoverySmsPatternCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return setting is null
            ? new SystemSmsSettingsSummary(
                false, "90008361", false, null, false, null)
            : new SystemSmsSettingsSummary(
                !string.IsNullOrWhiteSpace(setting.ProtectedApiKey),
                setting.LineNumber,
                setting.RegistrationSmsEnabled,
                setting.RegistrationSmsPatternCode,
                setting.PasswordRecoverySmsEnabled,
                setting.PasswordRecoverySmsPatternCode);
    }

    public async Task SaveAsync(
        string? apiKey,
        bool clearApiKey,
        string lineNumber,
        bool registrationSmsEnabled,
        string? registrationSmsPatternCode,
        bool passwordRecoverySmsEnabled,
        string? passwordRecoverySmsPatternCode,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        lineNumber = lineNumber.Trim();
        registrationSmsPatternCode = registrationSmsPatternCode?.Trim();
        passwordRecoverySmsPatternCode = passwordRecoverySmsPatternCode?.Trim();

        if (string.IsNullOrWhiteSpace(lineNumber) || lineNumber.Length > 50)
            throw new InvalidOperationException("شماره خط پیامک را وارد کنید.");

        if (registrationSmsPatternCode?.Length > 100)
            throw new InvalidOperationException("کد الگوی پیامک ثبت‌نام بیش از حد مجاز است.");

        if (passwordRecoverySmsPatternCode?.Length > 100)
            throw new InvalidOperationException("کد الگوی پیامک بازیابی رمز عبور بیش از حد مجاز است.");

        if (registrationSmsEnabled &&
            string.IsNullOrWhiteSpace(registrationSmsPatternCode))
            throw new InvalidOperationException("برای فعال‌سازی تأیید موبایل، کد الگوی پیامک لازم است.");

        if (passwordRecoverySmsEnabled &&
            string.IsNullOrWhiteSpace(passwordRecoverySmsPatternCode))
            throw new InvalidOperationException("برای فعال‌سازی بازیابی رمز عبور، کد الگوی پیامک لازم است.");

        if (clearApiKey && !string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("برای پاک‌کردن کلید، فیلد کلید API باید خالی باشد.");

        if ((registrationSmsEnabled || passwordRecoverySmsEnabled) &&
            clearApiKey && string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("برای فعال‌بودن ارسال پیامک، کلید API را پاک نکنید.");

        var setting = await _context.SystemSmsSettings
            .IgnoreQueryFilters()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting is null)
        {
            setting = new SystemSmsSetting { CreatedBy = updatedBy };
            _context.SystemSmsSettings.Add(setting);
        }

        if (clearApiKey)
            setting.ProtectedApiKey = null;
        else if (!string.IsNullOrWhiteSpace(apiKey))
        {
            setting.ProtectedApiKey = apiKey.Trim();
        }

        setting.LineNumber = lineNumber;
        setting.RegistrationSmsEnabled = registrationSmsEnabled;
        setting.RegistrationSmsPatternCode =
            string.IsNullOrWhiteSpace(registrationSmsPatternCode)
                ? null
                : registrationSmsPatternCode;

        setting.PasswordRecoverySmsEnabled = passwordRecoverySmsEnabled;
        setting.PasswordRecoverySmsPatternCode =
            string.IsNullOrWhiteSpace(passwordRecoverySmsPatternCode)
                ? null
                : passwordRecoverySmsPatternCode;

        setting.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<SystemSmsSendResult> SendRegistrationCodeAsync(
        string mobile,
        string code,
        CancellationToken cancellationToken = default)
    {
        return SendPatternSmsAsync(
            mobile,
            code,
            isPasswordRecovery: false,
            cancellationToken);
    }

    public Task<SystemSmsSendResult> SendPasswordRecoveryCodeAsync(
        string mobile,
        string code,
        CancellationToken cancellationToken = default)
    {
        return SendPatternSmsAsync(
            mobile,
            code,
            isPasswordRecovery: true,
            cancellationToken);
    }

    private async Task<SystemSmsSendResult> SendPatternSmsAsync(
        string mobile,
        string value,
        bool isPasswordRecovery,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new(false, null, "محتوای پیامک معتبر نیست.");

        mobile = NormalizeDigits(mobile.Trim());

        if (!Regex.IsMatch(mobile, "^09[0-9]{9}$"))
            return new(false, null, "شماره موبایل باید ۱۱ رقم و با 09 شروع شود.");

        var setting = await _context.SystemSmsSettings
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.ProtectedApiKey != null &&
                        (isPasswordRecovery
                            ? x.PasswordRecoverySmsEnabled
                            : x.RegistrationSmsEnabled))
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.ProtectedApiKey,
                x.LineNumber,
                PatternCode = isPasswordRecovery
                    ? x.PasswordRecoverySmsPatternCode
                    : x.RegistrationSmsPatternCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (setting?.ProtectedApiKey is null ||
            string.IsNullOrWhiteSpace(setting.PatternCode))
        {
            return new(
                false,
                null,
                isPasswordRecovery
                    ? "ارسال پیامک بازیابی رمز عبور در تنظیمات سامانه فعال نشده است."
                    : "ارسال پیامک تأیید ثبت‌نام در تنظیمات سامانه فعال نشده است.");
        }

        string apiKey;

        try
        {
            apiKey = setting.ProtectedApiKey;
        }
        catch (CryptographicException)
        {
            return new(
                false,
                null,
                "کلید پیامک سامانه قابل خواندن نیست؛ آن را دوباره ثبت کنید.");
        }

        var payload = new
        {
            code = setting.PatternCode,
            attributes = new Dictionary<string, string>
            {
                ["var1"] = value
            },
            recipient = mobile,
            line_number = setting.LineNumber,
            number_format = "english"
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);

            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.TryAddWithoutValidation("Api-Key", apiKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var accepted =
                response.IsSuccessStatusCode &&
                root.TryGetProperty("status", out var status) &&
                string.Equals(
                    status.ToString(),
                    "success",
                    StringComparison.OrdinalIgnoreCase);

            var message = root.TryGetProperty("message", out var messageElement)
                ? messageElement.ToString()
                : null;

            string? requestId = null;

            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("id", out var id))
            {
                requestId = id.ToString();
            }

            var successMessage = isPasswordRecovery
                ? "لینک بازیابی رمز عبور به سامانه پیامک تحویل داده شد."
                : "کد تأیید به سامانه پیامک تحویل داده شد.";

            var failureMessage = isPasswordRecovery
                ? "ارسال پیامک بازیابی رمز عبور ناموفق بود."
                : "ارسال پیامک تأیید ناموفق بود.";

            return new(
                accepted,
                requestId,
                accepted
                    ? successMessage
                    : message ?? $"{failureMessage} (HTTP {(int)response.StatusCode}).");
        }
        catch (HttpRequestException)
        {
            return new(false, null, "ارتباط با سرویس پیامک برقرار نشد.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "مهلت ارتباط با سرویس پیامک به پایان رسید.");
        }
        catch (JsonException)
        {
            return new(false, null, "پاسخ سرویس پیامک معتبر نیست.");
        }
    }

    private static string NormalizeDigits(string value)
    {
        var result = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is >= '\u06F0' and <= '\u06F9')
                result.Append((char)('0' + character - '\u06F0'));
            else if (character is >= '\u0660' and <= '\u0669')
                result.Append((char)('0' + character - '\u0660'));
            else
                result.Append(character);
        }

        return result.ToString();
    }

    public async Task<SmsBalanceResult> GetBalanceAsync(
    CancellationToken cancellationToken = default)
    {
        var apiKey = await _context.SystemSmsSettings
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.ProtectedApiKey != null &&
                        x.ProtectedApiKey != "")
            .OrderBy(x => x.Id)
            .Select(x => x.ProtectedApiKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new(false, null, null, "کلید API پیامک سامانه ثبت نشده است.");
        }

        return await _balanceService.GetBalanceAsync(
            apiKey.Trim(), cancellationToken);
    }
}