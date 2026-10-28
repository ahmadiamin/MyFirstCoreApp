using MyFirstCoreApp.Application.CustomerPortal;

namespace MyFirstCoreApp.Application.Interfaces;

public interface ITenantSmsSettingsService
{
    Task<TenantSmsSettingsSummary> GetSummaryAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<TenantSmsConfiguration?> GetForSendingAsync(int tenantId, CancellationToken cancellationToken = default);
    Task SaveAsync(int tenantId, string? apiKey, bool clearApiKey, string lineNumber,
        string? portalLinkText, string? updatedBy, bool chequeReminderEnabled = false,
        bool chequeReminderDayBeforeEnabled = false, bool chequeReminderDueDayEnabled = false,
        TimeOnly? chequeReminderTime = null, string? chequeReminderMobile = null,
        string? chequeReminderText = null,
        CancellationToken cancellationToken = default);
}
