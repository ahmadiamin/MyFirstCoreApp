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
    string? ChequeReminderText);

public sealed record TenantSmsConfiguration(
    string ApiKey,
    string LineNumber,
    string? PortalLinkText,
    string? ChequeReminderMobile,
    string? ChequeReminderText);
