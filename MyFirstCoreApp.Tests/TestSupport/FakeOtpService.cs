using MyFirstCoreApp.Application.Identity;
using MyFirstCoreApp.Application.Interfaces;

namespace MyFirstCoreApp.Tests.TestSupport;

/// <summary>
/// سرویس OTP جعلی: فقط ثبت می‌کند «به کدام موبایل‌ها کد خواسته شد». پیامک واقعی نمی‌رود
/// و رفتار پذیرش/خطا قابل‌تنظیم است.
/// </summary>
public sealed class FakeOtpService : IPasswordResetOtpService
{
    public List<string> RequestedMobiles { get; } = new();

    public bool Accept { get; set; } = true;
    public bool ThrowOnRequest { get; set; }

    public Task<PasswordResetOtpRequestResult> RequestCodeAsync(string mobile, CancellationToken cancellationToken = default)
    {
        RequestedMobiles.Add(mobile);

        if (ThrowOnRequest)
            throw new InvalidOperationException("پیامک در دسترس نیست.");

        return Task.FromResult(new PasswordResetOtpRequestResult(Accept, "پیام"));
    }

    public Task<PasswordResetOtpVerificationResult> VerifyCodeAsync(string mobile, string code, CancellationToken cancellationToken = default)
        => Task.FromResult(new PasswordResetOtpVerificationResult(false, null, "پشتیبانی نمی‌شود"));

    public Task<PasswordResetOtpCompletionResult> CompletePasswordResetAsync(int requestId, string newPassword, CancellationToken cancellationToken = default)
        => Task.FromResult(new PasswordResetOtpCompletionResult(false, ["پشتیبانی نمی‌شود"]));
}
