namespace MyFirstCoreApp.Application.Identity;

public sealed record PasswordResetOtpRequestResult(
    bool IsAccepted,
    string Message);

public sealed record PasswordResetOtpVerificationResult(
    bool IsVerified,
    int? RequestId,
    string Message);

public sealed record PasswordResetOtpCompletionResult(
    bool IsSucceeded,
    IReadOnlyList<string> Errors);