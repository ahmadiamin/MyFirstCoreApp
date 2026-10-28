namespace MyFirstCoreApp.Application.CustomerPortal;

public sealed record SystemSmsSettingsSummary(
    bool ApiKeyConfigured,
    string LineNumber,
    bool RegistrationSmsEnabled,
    string? RegistrationSmsPatternCode);

public sealed record SystemSmsSendResult(bool IsAccepted, string? RequestId, string Message);
