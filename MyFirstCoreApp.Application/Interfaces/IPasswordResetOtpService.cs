using MyFirstCoreApp.Application.Identity;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IPasswordResetOtpService
{
    Task<PasswordResetOtpRequestResult> RequestCodeAsync(
        string mobile,
        CancellationToken cancellationToken = default);

    Task<PasswordResetOtpVerificationResult> VerifyCodeAsync(
        string mobile,
        string code,
        CancellationToken cancellationToken = default);

    Task<PasswordResetOtpCompletionResult> CompletePasswordResetAsync(
    int requestId,
    string newPassword,
    CancellationToken cancellationToken = default);
}