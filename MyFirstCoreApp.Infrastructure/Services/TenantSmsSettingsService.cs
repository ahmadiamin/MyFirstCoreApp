using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class TenantSmsSettingsService : ITenantSmsSettingsService
{
    private readonly ApplicationDbContext _context;

    public TenantSmsSettingsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TenantSmsSettingsSummary> GetSummaryAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var setting = await _context.TenantSmsSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new
            {
                x.ProtectedApiKey,
                x.LineNumber,
                x.PortalLinkPatternCode,
                x.ChequeReminderEnabled,
                x.ChequeReminderDayBeforeEnabled,
                x.ChequeReminderDueDayEnabled,
                x.ChequeReminderTime,
                x.ChequeReminderMobile,
                x.ChequeReminderPatternCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return setting is null
            ? new TenantSmsSettingsSummary(false, "90008361", null, false, false, false,
                new TimeOnly(9, 0), null, null)
            : new TenantSmsSettingsSummary(!string.IsNullOrWhiteSpace(setting.ProtectedApiKey),
                setting.LineNumber, setting.PortalLinkPatternCode, setting.ChequeReminderEnabled,
                setting.ChequeReminderDayBeforeEnabled, setting.ChequeReminderDueDayEnabled,
                setting.ChequeReminderTime, setting.ChequeReminderMobile, setting.ChequeReminderPatternCode);
    }

    public async Task<TenantSmsConfiguration?> GetForSendingAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var setting = await _context.TenantSmsSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ProtectedApiKey != null)
            .Select(x => new
            {
                x.ProtectedApiKey,
                x.LineNumber,
                x.PortalLinkPatternCode,
                x.ChequeReminderMobile,
                x.ChequeReminderPatternCode
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (setting?.ProtectedApiKey is null) return null;

        try
        {
            var apiKey = setting.ProtectedApiKey;
            return new TenantSmsConfiguration(apiKey, setting.LineNumber, setting.PortalLinkPatternCode,
                setting.ChequeReminderMobile, setting.ChequeReminderPatternCode);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task SaveAsync(int tenantId, string? apiKey, bool clearApiKey, string lineNumber,
        string? PortalLinkPatternCode, string? updatedBy, bool chequeReminderEnabled = false,
        bool chequeReminderDayBeforeEnabled = false, bool chequeReminderDueDayEnabled = false,
        TimeOnly? chequeReminderTime = null, string? chequeReminderMobile = null,
        string? chequeReminderPatternCode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lineNumber) || lineNumber.Length > 50)
            throw new InvalidOperationException("شماره خط ارسال پیامک را وارد کنید.");

        if (PortalLinkPatternCode?.Length > 1000)
            throw new InvalidOperationException("طول الگوی پیامک بیش از حد مجاز است.");

        if (chequeReminderEnabled && !chequeReminderDayBeforeEnabled && !chequeReminderDueDayEnabled)
            throw new InvalidOperationException("حداقل یک زمان یادآوری چک باید فعال باشد.");

        if (chequeReminderEnabled && string.IsNullOrWhiteSpace(chequeReminderMobile))
            throw new InvalidOperationException("ارائه یک شماره موبایل برای یادآوری‌های شرکت الزامی است..");

        var normalizedReminderMobile = string.IsNullOrWhiteSpace(chequeReminderMobile)
            ? null
            : NormalizeMobile(chequeReminderMobile);

        if (chequeReminderEnabled && !System.Text.RegularExpressions.Regex.IsMatch(
                normalizedReminderMobile!, "^09[0-9]{9}$"))
            throw new InvalidOperationException("شماره موبایل یادآوری شرکت باید ۱۱ رقم باشد و با ۰۹ شروع شود.");

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
            setting.ProtectedApiKey = apiKey.Trim();

        setting.LineNumber = lineNumber.Trim();
        setting.PortalLinkPatternCode = string.IsNullOrWhiteSpace(PortalLinkPatternCode) ? null : PortalLinkPatternCode.Trim();
        setting.ChequeReminderEnabled = chequeReminderEnabled;
        setting.ChequeReminderDayBeforeEnabled = chequeReminderDayBeforeEnabled;
        setting.ChequeReminderDueDayEnabled = chequeReminderDueDayEnabled;
        setting.ChequeReminderTime = chequeReminderTime ?? new TimeOnly(9, 0);
        setting.ChequeReminderMobile = normalizedReminderMobile;
        setting.ChequeReminderPatternCode = string.IsNullOrWhiteSpace(chequeReminderPatternCode) ? null : chequeReminderPatternCode.Trim();
        setting.UpdatedBy = updatedBy;
        await _context.SaveChangesAsync(cancellationToken);
    }

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