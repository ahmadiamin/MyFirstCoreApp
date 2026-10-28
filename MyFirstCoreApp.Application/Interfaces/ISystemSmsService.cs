using MyFirstCoreApp.Application.CustomerPortal;

namespace MyFirstCoreApp.Application.Interfaces;

public interface ISystemSmsService
{
    Task<SystemSmsSettingsSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string? apiKey, bool clearApiKey, string lineNumber,
        bool registrationSmsEnabled, string? registrationSmsText, string? updatedBy,
        CancellationToken cancellationToken = default);
    Task<SystemSmsSendResult> SendRegistrationCodeAsync(string mobile, string code,
        CancellationToken cancellationToken = default);
}
