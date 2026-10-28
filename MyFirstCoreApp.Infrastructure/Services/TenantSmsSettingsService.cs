using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class TenantSmsSettingsService : ITenantSmsSettingsService
{
    private const string ProtectorPurpose = "MyFirstCoreApp.TenantSmsSettings.ApiKey.v1";
    private readonly ApplicationDbContext _context;
    private readonly IDataProtectionProvider _dataProtection;

    public TenantSmsSettingsService(ApplicationDbContext context, IDataProtectionProvider dataProtection)
    {
        _context = context;
        _dataProtection = dataProtection;
    }

    public async Task<TenantSmsSettingsSummary> GetSummaryAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var setting = await _context.TenantSmsSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new { x.ProtectedApiKey, x.LineNumber, x.PortalLinkText,
                x.ChequeReminderEnabled, x.ChequeReminderDayBeforeEnabled, x.ChequeReminderDueDayEnabled,
                x.ChequeReminderTime, x.ChequeReminderMobile, x.ChequeReminderText })
            .FirstOrDefaultAsync(cancellationToken);

        return setting is null
            ? new TenantSmsSettingsSummary(false, "90008361", null, false, false, false,
                new TimeOnly(9, 0), null, null)
            : new TenantSmsSettingsSummary(!string.IsNullOrWhiteSpace(setting.ProtectedApiKey),
                setting.LineNumber, setting.PortalLinkText, setting.ChequeReminderEnabled,
                setting.ChequeReminderDayBeforeEnabled, setting.ChequeReminderDueDayEnabled,
                setting.ChequeReminderTime, setting.ChequeReminderMobile, setting.ChequeReminderText);
    }

    public async Task<TenantSmsConfiguration?> GetForSendingAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var setting = await _context.TenantSmsSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ProtectedApiKey != null)
            .Select(x => new { x.ProtectedApiKey, x.LineNumber, x.PortalLinkText,
                x.ChequeReminderMobile, x.ChequeReminderText })
            .FirstOrDefaultAsync(cancellationToken);
        if (setting?.ProtectedApiKey is null) return null;

        try
        {
            var apiKey = CreateProtector(tenantId).Unprotect(setting.ProtectedApiKey);
            return new TenantSmsConfiguration(apiKey, setting.LineNumber, setting.PortalLinkText,
                setting.ChequeReminderMobile, setting.ChequeReminderText);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task SaveAsync(int tenantId, string? apiKey, bool clearApiKey, string lineNumber,
        string? portalLinkText, string? updatedBy, bool chequeReminderEnabled = false,
        bool chequeReminderDayBeforeEnabled = false, bool chequeReminderDueDayEnabled = false,
        TimeOnly? chequeReminderTime = null, string? chequeReminderMobile = null,
        string? chequeReminderText = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lineNumber) || lineNumber.Length > 50)
            throw new InvalidOperationException("شماره خط ارسال پیامک را وارد کنید.");
        if (portalLinkText?.Length > 1000)
            throw new InvalidOperationException("طول الگوی پیامک بیش از حد مجاز است.");
        if (chequeReminderText?.Length > 1000)
            throw new InvalidOperationException("Cheque reminder template exceeds the maximum length.");
        if (chequeReminderEnabled && string.IsNullOrWhiteSpace(chequeReminderText))
            throw new InvalidOperationException("A cheque reminder template is required when reminders are enabled.");
        if (chequeReminderEnabled && !chequeReminderDayBeforeEnabled && !chequeReminderDueDayEnabled)
            throw new InvalidOperationException("At least one cheque reminder time must be enabled.");
        if (chequeReminderEnabled && string.IsNullOrWhiteSpace(chequeReminderMobile))
            throw new InvalidOperationException("A mobile number for company reminders is required.");
        var normalizedReminderMobile = string.IsNullOrWhiteSpace(chequeReminderMobile)
            ? null
            : NormalizeMobile(chequeReminderMobile);
        if (chequeReminderEnabled && !System.Text.RegularExpressions.Regex.IsMatch(
                normalizedReminderMobile!, "^09[0-9]{9}$"))
            throw new InvalidOperationException("The company reminder mobile must be 11 digits beginning with 09.");
        if (chequeReminderEnabled && new[] { "%var1%", "%var2%", "%var3%", "%var4%" }
                .Any(variable => !chequeReminderText!.Contains(variable, StringComparison.Ordinal)))
            throw new InvalidOperationException("The cheque reminder template must contain %var1% through %var4%.");

        if (!string.IsNullOrWhiteSpace(portalLinkText) && !portalLinkText.Contains("%var1%", StringComparison.Ordinal))
            throw new InvalidOperationException("الگوی پیامک باید شامل متغیر %var1% باشد.");

        var tenantExists = await _context.Tenants.IgnoreQueryFilters().AnyAsync(
            x => x.Id == tenantId && !x.IsDeleted, cancellationToken);
        if (!tenantExists) throw new InvalidOperationException("شرکت پیدا نشد.");

        var setting = await _context.TenantSmsSettings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (setting is null)
        {
            setting = new TenantSmsSetting
            {
                TenantId = tenantId,
                CreatedBy = updatedBy
            };
            _context.TenantSmsSettings.Add(setting);
        }

        if (clearApiKey)
            setting.ProtectedApiKey = null;
        else if (!string.IsNullOrWhiteSpace(apiKey))
            setting.ProtectedApiKey = CreateProtector(tenantId).Protect(apiKey.Trim());

        setting.LineNumber = lineNumber.Trim();
        setting.PortalLinkText = string.IsNullOrWhiteSpace(portalLinkText) ? null : portalLinkText.Trim();
        setting.ChequeReminderEnabled = chequeReminderEnabled;
        setting.ChequeReminderDayBeforeEnabled = chequeReminderDayBeforeEnabled;
        setting.ChequeReminderDueDayEnabled = chequeReminderDueDayEnabled;
        setting.ChequeReminderTime = chequeReminderTime ?? new TimeOnly(9, 0);
        setting.ChequeReminderMobile = normalizedReminderMobile;
        setting.ChequeReminderText = string.IsNullOrWhiteSpace(chequeReminderText) ? null : chequeReminderText.Trim();
        setting.UpdatedBy = updatedBy;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private IDataProtector CreateProtector(int tenantId) =>
        _dataProtection.CreateProtector(ProtectorPurpose)
            .CreateProtector(tenantId.ToString(CultureInfo.InvariantCulture));

    private static string NormalizeMobile(string mobile)
    {
        var result = new System.Text.StringBuilder(mobile.Length);
        foreach (var character in mobile.Trim())
        {
            if (character is >= '\u06F0' and <= '\u06F9') result.Append((char)('0' + character - '\u06F0'));
            else if (character is >= '\u0660' and <= '\u0669') result.Append((char)('0' + character - '\u0660'));
            else result.Append(character);
        }
        return result.ToString();
    }
}
