namespace MyFirstCoreApp.Application.CustomerPortal;

public sealed record TenantSmsSettingsSummary(
    bool ApiKeyConfigured,
    string LineNumber,
    string? PortalLinkText,
    bool ChequeReminderEnabled,
    bool ChequeReminderDayBeforeEnabled,
    bool ChequeReminderDueDayEnabled,
    TimeOnly ChequeReminderTime,
    string? ChequeReminderMobile,
    string? ChequeReminderPatternCode);

public sealed record TenantSmsConfiguration(
    string ApiKey,
    string LineNumber,
    string? PortalLinkPatternCode,
    string? ChequeReminderMobile,
    string? ChequeReminderPatternCode);
