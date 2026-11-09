using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Identity;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class PasswordResetOtpServiceTests
{
    [Fact]
    public async Task VerifyCode_WithInvalidMobile_ShouldFail()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var userManager = CreateUserManager(context);
        var service = new PasswordResetOtpService(
            context,
            userManager,
            new PasswordHasher<ApplicationUser>(),
            null!);

        var result = await service.VerifyCodeAsync(
            "123456789",
            "123456");

        Assert.False(result.IsVerified);
        Assert.Null(result.RequestId);
    }

    [Fact]
    public async Task VerifyCode_WithInvalidCodeLength_ShouldFail()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var userManager = CreateUserManager(context);
        var service = new PasswordResetOtpService(
            context,
            userManager,
            new PasswordHasher<ApplicationUser>(),
            null!);

        var result = await service.VerifyCodeAsync(
            "09123456789",
            "12345");

        Assert.False(result.IsVerified);
        Assert.Null(result.RequestId);
    }

    [Fact]
    public async Task VerifyCode_WithUnknownMobile_ShouldFail()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var userManager = CreateUserManager(context);
        var service = new PasswordResetOtpService(
            context,
            userManager,
            new PasswordHasher<ApplicationUser>(),
            null!);

        var result = await service.VerifyCodeAsync(
            "09123456789",
            "123456");

        Assert.False(result.IsVerified);
        Assert.Null(result.RequestId);
    }

    [Fact]
    public async Task VerifyCode_WithCorrectCode_ShouldVerify()
    {
        using var database = new TestDatabase();

        var user = new ApplicationUser
        {
            Id = "test-user-1",
            UserName = "test-user",
            FullName = "کاربر تست",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        var hasher = new PasswordHasher<ApplicationUser>();
        var code = "123456";

        using (var seedContext = database.NewContext())
        {
            seedContext.Users.Add(user);

            seedContext.PasswordResetOtps.Add(
                new PasswordResetOtp
                {
                    UserId = user.Id,
                    CodeHash = hasher.HashPassword(user, code),
                    SentAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    FailedAttempts = 0,
                    CreatedAt = DateTime.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        using var context = database.NewContext();

        var userManager = CreateUserManager(context);

        var service = new PasswordResetOtpService(
            context,
            userManager,
            hasher,
            null!);

        var result = await service.VerifyCodeAsync(
            "۰۹۱۲۳۴۵۶۷۸۹",
            "۱۲۳۴۵۶");

        Assert.True(result.IsVerified);
        Assert.NotNull(result.RequestId);

        var savedOtp = context.PasswordResetOtps
            .Single(x => x.UserId == user.Id);

        Assert.NotNull(savedOtp.VerifiedAt);
        Assert.Equal(0, savedOtp.FailedAttempts);
    }

    [Fact]
    public async Task VerifyCode_WithWrongCode_ShouldIncreaseFailedAttempts()
    {
        using var database = new TestDatabase();

        var user = new ApplicationUser
        {
            Id = "test-user-2",
            UserName = "test-user-2",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        var hasher = new PasswordHasher<ApplicationUser>();

        using (var seedContext = database.NewContext())
        {
            seedContext.Users.Add(user);

            seedContext.PasswordResetOtps.Add(
                new PasswordResetOtp
                {
                    UserId = user.Id,
                    CodeHash = hasher.HashPassword(user, "123456"),
                    SentAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    FailedAttempts = 0,
                    CreatedAt = DateTime.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        using var context = database.NewContext();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            hasher,
            null!);

        var result = await service.VerifyCodeAsync(
            "09123456789",
            "654321");

        Assert.False(result.IsVerified);

        var savedOtp = context.PasswordResetOtps
            .Single(x => x.UserId == user.Id);

        Assert.Equal(1, savedOtp.FailedAttempts);
        Assert.Null(savedOtp.VerifiedAt);
        Assert.Null(savedOtp.ConsumedAt);
    }

    [Fact]
    public async Task VerifyCode_AfterFiveWrongAttempts_ShouldConsumeOtp()
    {
        using var database = new TestDatabase();

        var user = new ApplicationUser
        {
            Id = "test-user-3",
            UserName = "test-user-3",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        var hasher = new PasswordHasher<ApplicationUser>();

        using (var seedContext = database.NewContext())
        {
            seedContext.Users.Add(user);

            seedContext.PasswordResetOtps.Add(
                new PasswordResetOtp
                {
                    UserId = user.Id,
                    CodeHash = hasher.HashPassword(user, "123456"),
                    SentAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    FailedAttempts = 4,
                    CreatedAt = DateTime.UtcNow
                });

            await seedContext.SaveChangesAsync();
        }

        using var context = database.NewContext();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            hasher,
            null!);

        var result = await service.VerifyCodeAsync(
            "09123456789",
            "654321");

        Assert.False(result.IsVerified);

        var savedOtp = context.PasswordResetOtps
            .Single(x => x.UserId == user.Id);

        Assert.Equal(5, savedOtp.FailedAttempts);
        Assert.NotNull(savedOtp.ConsumedAt);
    }
    [Fact]
    public async Task RequestCode_BeforeResendCooldown_ShouldRejectSecondRequest()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "otp-resend-user",
            UserName = "otp-resend-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var smsService = new FakeSystemSmsService();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            smsService);

        var firstResult = await service.RequestCodeAsync(user.PhoneNumber);
        Assert.Matches(@"^[0-9]{6}$", smsService.LastCode!);
        Assert.Equal(user.PhoneNumber, smsService.LastMobile);
        Assert.True(firstResult.IsAccepted);
        Assert.Equal(1, smsService.SendCount);

        var secondResult = await service.RequestCodeAsync(user.PhoneNumber);

        Assert.False(secondResult.IsAccepted);
        Assert.Equal(1, smsService.SendCount);
    }

    [Fact]
    public async Task RequestCode_AfterThreeRequests_ShouldRejectFourthRequest()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "otp-limit-user",
            UserName = "otp-limit-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var smsService = new FakeSystemSmsService();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            smsService);

        for (var i = 1; i <= 3; i++)
        {
            var result = await service.RequestCodeAsync(user.PhoneNumber);

            Assert.True(result.IsAccepted);
            Assert.Equal(i, smsService.SendCount);

            var latestOtp = context.PasswordResetOtps
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.SentAt)
                .First();

            latestOtp.SentAt = DateTime.UtcNow.AddSeconds(-61);

            await context.SaveChangesAsync();
        }

        var fourthResult = await service.RequestCodeAsync(user.PhoneNumber);

        Assert.False(fourthResult.IsAccepted);
        Assert.Equal(3, smsService.SendCount);

        Assert.Equal(
            3,
            context.PasswordResetOtps.Count(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task RequestCode_WhenSmsSendingFails_ShouldConsumeOtp()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "otp-sms-failed-user",
            UserName = "otp-sms-failed-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var smsService = new FakeSystemSmsService
        {
            SendAccepted = false
        };

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            smsService);

        var requestResult = await service.RequestCodeAsync(user.PhoneNumber);

        Assert.False(requestResult.IsAccepted);
        Assert.Equal(1, smsService.SendCount);
        Assert.NotNull(smsService.LastCode);

        var savedOtp = context.PasswordResetOtps
            .Single(x => x.UserId == user.Id);

        Assert.NotNull(savedOtp.ConsumedAt);

        var verifyResult = await service.VerifyCodeAsync(
            user.PhoneNumber,
            smsService.LastCode!);

        Assert.False(verifyResult.IsVerified);
        Assert.Null(verifyResult.RequestId);
    }

    private static FakeUserManager CreateUserManager(
        ApplicationDbContext context)
    {
        return new FakeUserManager(
            new UserStore<ApplicationUser>(context));
    }

    private sealed class FakeUserManager : UserManager<ApplicationUser>
    {
        private readonly IUserStore<ApplicationUser> _store;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

        public FakeUserManager(IUserStore<ApplicationUser> store)
            : base(
                store,
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                new Logger<FakeUserManager>(
                    LoggerFactory.Create(builder => { })))
        {
            _store = store;
            _passwordHasher = new PasswordHasher<ApplicationUser>();
        }

        public override Task<string> GeneratePasswordResetTokenAsync(
            ApplicationUser user)
        {
            return Task.FromResult("test-reset-token");
        }

        public override async Task<IdentityResult> ResetPasswordAsync(
            ApplicationUser user,
            string token,
            string newPassword)
        {
            if (token != "test-reset-token")
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Description = "توکن نامعتبر است."
                    });
            }

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                newPassword);

            return await _store.UpdateAsync(
                user,
                CancellationToken.None);
        }
    }

    private sealed class UserStoreStub : IUserStore<ApplicationUser>
    {
        public void Dispose() { }

        public Task<string> GetUserIdAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
            => Task.FromResult(user.Id);

        public Task<string?> GetUserNameAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(
            ApplicationUser user,
            string? userName,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string?> GetNormalizedUserNameAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(
            ApplicationUser user,
            string? normalizedName,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IdentityResult> CreateAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByNameAsync(
    string normalizedUserName,
    CancellationToken cancellationToken)
        {
            return Task.FromResult<ApplicationUser?>(null);
        }

        public Task<ApplicationUser?> FindByIdAsync(
    string userId,
    CancellationToken cancellationToken)
        {
            return Task.FromResult<ApplicationUser?>(null);
        }
    }
    [Fact]
    public async Task VerifyCode_WithExpiredOtp_ShouldFail()
    {
        using var database = new TestDatabase();

        var user = new ApplicationUser
        {
            Id = "expired-user",
            UserName = "expired-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        var hasher = new PasswordHasher<ApplicationUser>();

        using (var seedContext = database.NewContext())
        {
            seedContext.Users.Add(user);

            seedContext.PasswordResetOtps.Add(new PasswordResetOtp
            {
                UserId = user.Id,
                CodeHash = hasher.HashPassword(user, "123456"),
                SentAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            });

            await seedContext.SaveChangesAsync();

        }

        using var context = database.NewContext();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            hasher,
            null!);

        var result = await service.VerifyCodeAsync(
            "09123456789",
            "123456");

        Assert.False(result.IsVerified);
        Assert.Null(result.RequestId);
    }

    [Fact]
    public async Task VerifyCode_ReusingVerifiedOtp_ShouldFail()
    {
        using var database = new TestDatabase();

        var user = new ApplicationUser
        {
            Id = "reuse-user",
            UserName = "reuse-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        var hasher = new PasswordHasher<ApplicationUser>();

        using (var seedContext = database.NewContext())
        {
            seedContext.Users.Add(user);

            seedContext.PasswordResetOtps.Add(new PasswordResetOtp
            {
                UserId = user.Id,
                CodeHash = hasher.HashPassword(user, "123456"),
                SentAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            });

            await seedContext.SaveChangesAsync();
        }

        using var context = database.NewContext();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            hasher,
            null!);

        // بار اول باید موفق باشد.
        var firstResult = await service.VerifyCodeAsync(
            "09123456789",
            "123456");

        Assert.True(firstResult.IsVerified);
        Assert.NotNull(firstResult.RequestId);

        // بار دوم همان کد نباید پذیرفته شود.
        var secondResult = await service.VerifyCodeAsync(
            "09123456789",
            "123456");

        Assert.False(secondResult.IsVerified);
        Assert.Null(secondResult.RequestId);
    }

    private sealed class FakeSystemSmsService : ISystemSmsService
    {
        public bool SendAccepted { get; set; } = true;

        public int SendCount { get; private set; }

        public string? LastMobile { get; private set; }

        public string? LastCode { get; private set; }
        public bool ThrowOnSend { get; set; }
        public SystemSmsSettingsSummary Summary { get; set; } = new(
            ApiKeyConfigured: true,
            LineNumber: "300000000",
            RegistrationSmsEnabled: false,
            RegistrationSmsPatternCode: null,
            PasswordRecoverySmsEnabled: true,
            PasswordRecoverySmsPatternCode: "password-recovery-test"
        );
        
        public Task<SmsBalanceResult> GetBalanceAsync(    CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new SmsBalanceResult(false, null, null, "در تست استفاده نمی‌شود."));
        }
        
        public Task<SystemSmsSettingsSummary> GetSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Summary);
        }

        public Task SaveAsync(
            string? apiKey,
            bool clearApiKey,
            string lineNumber,
            bool registrationSmsEnabled,
            string? registrationSmsPatternCode,
            bool passwordRecoverySmsEnabled,
            string? passwordRecoverySmsPatternCode,
            string? updatedBy,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SystemSmsSendResult> SendRegistrationCodeAsync(
            string mobile,
            string code,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SystemSmsSendResult> SendPasswordRecoveryCodeAsync(
            string mobile,
            string code,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("SMS provider error.");

            SendCount++;
            LastMobile = mobile;
            LastCode = code;

            return Task.FromResult(new SystemSmsSendResult(
                IsAccepted: SendAccepted,
                RequestId: SendAccepted ? $"test-{SendCount}" : null,
                Message: SendAccepted ? "ارسال موفق" : "ارسال ناموفق"
            ));
        }
    }
    [Fact]
    public async Task RequestCode_WhenSmsThrowsException_ShouldConsumeOtp()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "otp-sms-exception-user",
            UserName = "otp-sms-exception-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var smsService = new FakeSystemSmsService
        {
            ThrowOnSend = true
        };

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            smsService);

        var result = await service.RequestCodeAsync(user.PhoneNumber);

        Assert.False(result.IsAccepted);

        var savedOtp = context.PasswordResetOtps
            .Single(x => x.UserId == user.Id);

        Assert.NotNull(savedOtp.ConsumedAt);
    }

    [Fact]
    public async Task RequestCode_AfterNewRequest_ShouldInvalidatePreviousOtp()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();
            
        var user = new ApplicationUser
        {
            Id = "otp-replacement-user",
            UserName = "otp-replacement-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var smsService = new FakeSystemSmsService();

        var hasher = new PasswordHasher<ApplicationUser>();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            hasher,
            smsService);

        // درخواست کد اول
        var firstRequest = await service.RequestCodeAsync(user.PhoneNumber);

        Assert.True(firstRequest.IsAccepted);
        Assert.NotNull(smsService.LastCode);

        var firstCode = smsService.LastCode;

        // برای عبور از محدودیت ۶۰ ثانیه، زمان ارسال کد اول را تغییر می‌دهیم.
        var firstOtp = context.PasswordResetOtps
            .Single(x => x.UserId == user.Id);

        firstOtp.SentAt = DateTime.UtcNow.AddSeconds(-61);

        await context.SaveChangesAsync();

        // درخواست کد دوم
        var secondRequest = await service.RequestCodeAsync(user.PhoneNumber);

        Assert.True(secondRequest.IsAccepted);
        Assert.Equal(2, smsService.SendCount);

        var secondCode = smsService.LastCode;

        Assert.NotNull(secondCode);
        Assert.NotEqual(firstCode, secondCode);

        // کد اول باید باطل شده باشد.
        var oldCodeResult = await service.VerifyCodeAsync(
            user.PhoneNumber,
            firstCode!);

        Assert.False(oldCodeResult.IsVerified);
        Assert.Null(oldCodeResult.RequestId);

        // کد دوم باید معتبر باشد.
        var newCodeResult = await service.VerifyCodeAsync(
            user.PhoneNumber,
            secondCode!);

        Assert.True(newCodeResult.IsVerified);
        Assert.NotNull(newCodeResult.RequestId);

        // بررسی وضعیت ذخیره‌شده در دیتابیس
        var savedOldOtp = await context.PasswordResetOtps
            .FindAsync(firstOtp.Id);

        Assert.NotNull(savedOldOtp);
        Assert.NotNull(savedOldOtp.ConsumedAt);
    }
    [Fact]
    public async Task CompletePasswordReset_ShouldConsumeAllPendingOtps()
    {
        using var database = new TestDatabase();
        await using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "user-otp-test",
            UserName = "09123456789",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true
        };

        context.Users.Add(user);

        var now = DateTime.UtcNow;

        var verifiedOtp = new PasswordResetOtp
        {
            UserId = user.Id,
            CodeHash = "test-hash",
            SentAt = now.AddMinutes(-2),
            ExpiresAt = now.AddMinutes(3),
            VerifiedAt = now.AddMinutes(-1)
        };

        var previousOtp = new PasswordResetOtp
        {
            UserId = user.Id,
            CodeHash = "previous-hash",
            SentAt = now.AddMinutes(-4),
            ExpiresAt = now.AddMinutes(1),
            VerifiedAt = now.AddMinutes(-3)
        };

        context.PasswordResetOtps.AddRange(verifiedOtp, previousOtp);
        await context.SaveChangesAsync();

        var userManager = new FakeUserManager(
            new UserStore<ApplicationUser>(context));

        var smsService = new FakeSystemSmsService();

        var service = new PasswordResetOtpService(
            context,
            userManager,
            new PasswordHasher<ApplicationUser>(),
            smsService);

        var result = await service.CompletePasswordResetAsync(
            verifiedOtp.Id,
            "NewPassword123!");

        Assert.True(result.IsSucceeded);
        Assert.Empty(result.Errors);

        Assert.NotNull(user.PasswordChangedAt);

        var otps = await context.PasswordResetOtps
            .Where(x => x.UserId == user.Id)
            .ToListAsync();

        Assert.Equal(2, otps.Count);
        Assert.All(otps, otp => Assert.NotNull(otp.ConsumedAt));

        Assert.NotEqual("test-hash", user.PasswordHash);
    }

    [PasswordResetSqlServerFact]
    public async Task RequestCode_ConcurrentRequests_ShouldAcceptOnlyOne()
    {
        var database = new PasswordResetSqlServerTestDatabase();

        // آماده‌سازی کاربر
        const string testUserId = "otp-concurrency-user";
        const string testMobile = "09123456789";

        await using (var context = database.NewContext())
        {
            // پاک‌سازی اطلاعات اجرای قبلی تست
            await context.PasswordResetOtps
                .IgnoreQueryFilters()
                .Where(x => x.UserId == testUserId)
                .ExecuteDeleteAsync();

            var oldUser = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == testUserId);

            if (oldUser is not null)
                context.Users.Remove(oldUser);

            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = testUserId,
                UserName = testMobile,
                PhoneNumber = testMobile,
                PhoneNumberConfirmed = true,
                IsActive = true
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var smsService = new FakeSystemSmsService();

        async Task<PasswordResetOtpRequestResult> SendRequestAsync()
        {
            await using var context = database.NewContext();

            var userManager = new FakeUserManager(
                new UserStore<ApplicationUser>(context));

            var service = new PasswordResetOtpService(
                context,
                userManager,
                new PasswordHasher<ApplicationUser>(),
                smsService);

            return await service.RequestCodeAsync(testMobile);
        }

        var results = await Task.WhenAll(
            SendRequestAsync(),
            SendRequestAsync());

        Assert.Single(results.Where(x => x.IsAccepted));
        Assert.Single(results.Where(x => !x.IsAccepted));

        Assert.Equal(1, smsService.SendCount);

        await using var verificationContext = database.NewContext();

        var activeOtps = await verificationContext.PasswordResetOtps
            .Where(x =>
                x.UserId == testUserId &&
                x.ConsumedAt == null)
            .ToListAsync();

        Assert.Single(activeOtps);
    }

    [PasswordResetSqlServerFact]
    public async Task VerifyCode_ConcurrentRequests_ShouldVerifyOnlyOnce()
    {
        var database = new PasswordResetSqlServerTestDatabase();

        const string testUserId = "otp-verify-concurrency-user";
        const string testMobile = "09123456788";
        const string testCode = "123456";

        // پاک‌سازی اطلاعات اجرای قبلی
        await using (var context = database.NewContext())
        {
            var oldOtps = await context.PasswordResetOtps
                .IgnoreQueryFilters()
                .Where(x => x.UserId == testUserId)
                .ToListAsync();

            context.PasswordResetOtps.RemoveRange(oldOtps);

            var oldUser = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == testUserId);

            if (oldUser is not null)
                context.Users.Remove(oldUser);

            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = testUserId,
                UserName = testMobile,
                PhoneNumber = testMobile,
                PhoneNumberConfirmed = true,
                IsActive = true
            };

            context.Users.Add(user);

            var hasher = new PasswordHasher<ApplicationUser>();

            context.PasswordResetOtps.Add(new PasswordResetOtp
            {
                UserId = testUserId,
                CodeHash = hasher.HashPassword(user, testCode),
                SentAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                FailedAttempts = 0
            });

            await context.SaveChangesAsync();
            var savedOtp = await context.PasswordResetOtps
                .IgnoreQueryFilters()
                .SingleAsync(x => x.UserId == testUserId);

            Assert.Null(savedOtp.ConsumedAt);
            Assert.Null(savedOtp.ConsumedAt);

            // بررسی مستقیم دیتابیس، بدون فیلترهای EF
            var consumedAtFromDb = await context.Database
                .SqlQuery<DateTime?>($"""
        SELECT ConsumedAt AS Value
        FROM PasswordResetOtps
        WHERE UserId = {testUserId}
        """)
                .SingleAsync();

            Assert.Null(consumedAtFromDb);
            Assert.Null(savedOtp.VerifiedAt);
            Assert.False(savedOtp.IsDeleted);
        }

        async Task<PasswordResetOtpVerificationResult> VerifyAsync()
        {
            await using var context = database.NewContext();

            var userManager = new FakeUserManager(
                new UserStore<ApplicationUser>(context));

            var service = new PasswordResetOtpService(
                context,
                userManager,
                new PasswordHasher<ApplicationUser>(),
                new FakeSystemSmsService());

            return await service.VerifyCodeAsync(testMobile, testCode);
        }
        await using (var context = database.NewContext())
        {
            var user = await context.Users
                .FirstOrDefaultAsync(x => x.Id == testUserId);

            Assert.NotNull(user);
            Assert.Equal(testMobile, user.PhoneNumber);
            Assert.True(user.PhoneNumberConfirmed);
            Assert.True(user.IsActive);

            var savedOtp = await context.PasswordResetOtps
                .FirstOrDefaultAsync(x => x.UserId == testUserId);

            Assert.NotNull(savedOtp);
            Assert.Null(savedOtp.ConsumedAt);
            Assert.Null(savedOtp.VerifiedAt);
            Assert.True(savedOtp.ExpiresAt > DateTime.UtcNow);

            var hasher = new PasswordHasher<ApplicationUser>();

            var hashResult = hasher.VerifyHashedPassword(
                user,
                savedOtp.CodeHash,
                testCode);

            Assert.NotEqual(
                PasswordVerificationResult.Failed,
                hashResult);

            var allOtps = await context.PasswordResetOtps
    .IgnoreQueryFilters()
    .Where(x => x.UserId == testUserId)
    .OrderByDescending(x => x.SentAt)
    .ToListAsync();

            foreach (var item in allOtps)
            {
                Console.WriteLine(
                    $"OTP Id={item.Id}, " +
                    $"SentAt={item.SentAt:O}, " +
                    $"ConsumedAt={item.ConsumedAt:O}, " +
                    $"IsDeleted={item.IsDeleted}");
            }

        }
        await using (var context = database.NewContext())
        {
            var rows = await context.PasswordResetOtps
                .IgnoreQueryFilters()
                .Where(x => x.UserId == testUserId)
                .Select(x => new
                {
                    x.Id,
                    x.UserId,
                    x.IsDeleted,
                    x.ConsumedAt,
                    x.VerifiedAt,
                    x.ExpiresAt,
                    x.SentAt
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                Console.WriteLine(
                    $"ID={row.Id}, User={row.UserId}, " +
                    $"Deleted={row.IsDeleted}, " +
                    $"Consumed={row.ConsumedAt}, " +
                    $"Verified={row.VerifiedAt}, " +
                    $"Expires={row.ExpiresAt:O}, " +
                    $"Sent={row.SentAt:O}");
            }

            Assert.Single(rows);
        }
        // دو درخواست هم‌زمان با یک کد صحیح
        var results = await Task.WhenAll(
            VerifyAsync(),
            VerifyAsync());

        var verifiedCount = results.Count(x => x.IsVerified);
        var failedCount = results.Count(x => !x.IsVerified);

        Assert.True(
            verifiedCount == 1,
            $"Expected 1 verified request, but got {verifiedCount}. " +
            $"Messages: {string.Join(" | ", results.Select(x => x.Message))}");

        Assert.Equal(1, failedCount);

        // بررسی وضعیت نهایی رکورد در دیتابیس
        await using var verificationContext = database.NewContext();

        var otp = await verificationContext.PasswordResetOtps
            .SingleAsync(x => x.UserId == testUserId);

        Assert.NotNull(otp.VerifiedAt);
        Assert.Null(otp.ConsumedAt);
        Assert.Equal(0, otp.FailedAttempts);
    }
    [Fact]
    public async Task CompletePasswordReset_WithUnverifiedOtp_ShouldFail()
    {
        using var database = new TestDatabase();
        await using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "unverified-otp-user",
            UserName = "unverified-otp-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true,
            PasswordHash = "old-password-hash"
        };

        var otp = new PasswordResetOtp
        {
            UserId = user.Id,
            CodeHash = "test-hash",
            SentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            VerifiedAt = null
        };

        context.Users.Add(user);
        context.PasswordResetOtps.Add(otp);
        await context.SaveChangesAsync();

        var oldPasswordHash = user.PasswordHash;

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            new FakeSystemSmsService());

        var result = await service.CompletePasswordResetAsync(
            otp.Id,
            "NewPassword123!");

        Assert.False(result.IsSucceeded);
        Assert.Equal(oldPasswordHash, user.PasswordHash);

        var savedOtp = await context.PasswordResetOtps
            .SingleAsync(x => x.Id == otp.Id);

        Assert.Null(savedOtp.ConsumedAt);
    }

    [Fact]
    public async Task CompletePasswordReset_WithConsumedOtp_ShouldFail()
    {
        using var database = new TestDatabase();
        await using var context = database.NewContext();

        var now = DateTime.UtcNow;

        var user = new ApplicationUser
        {
            Id = "consumed-otp-user",
            UserName = "consumed-otp-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = true,
            PasswordHash = "old-password-hash"
        };

        var otp = new PasswordResetOtp
        {
            UserId = user.Id,
            CodeHash = "test-hash",
            SentAt = now.AddMinutes(-2),
            ExpiresAt = now.AddMinutes(3),
            VerifiedAt = now.AddMinutes(-1),
            ConsumedAt = now.AddSeconds(-30)
        };

        context.Users.Add(user);
        context.PasswordResetOtps.Add(otp);
        await context.SaveChangesAsync();

        var oldPasswordHash = user.PasswordHash;

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            new FakeSystemSmsService());

        var result = await service.CompletePasswordResetAsync(
            otp.Id,
            "NewPassword123!");

        Assert.False(result.IsSucceeded);
        Assert.Equal(oldPasswordHash, user.PasswordHash);

        var savedOtp = await context.PasswordResetOtps
            .SingleAsync(x => x.Id == otp.Id);

        Assert.NotNull(savedOtp.ConsumedAt);
    }

    [Fact]
    public async Task CompletePasswordReset_WithInactiveUser_ShouldFail()
    {
        using var database = new TestDatabase();
        await using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "inactive-otp-user",
            UserName = "inactive-otp-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = false,
            PasswordHash = "old-password-hash"
        };

        var otp = new PasswordResetOtp
        {
            UserId = user.Id,
            CodeHash = "test-hash",
            SentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            VerifiedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.PasswordResetOtps.Add(otp);
        await context.SaveChangesAsync();

        var oldPasswordHash = user.PasswordHash;

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            new FakeSystemSmsService());

        var result = await service.CompletePasswordResetAsync(
            otp.Id,
            "NewPassword123!");

        Assert.False(result.IsSucceeded);
        Assert.Equal(oldPasswordHash, user.PasswordHash);

        var savedOtp = await context.PasswordResetOtps
            .SingleAsync(x => x.Id == otp.Id);

        Assert.Null(savedOtp.ConsumedAt);
    }
    [Fact]
    public async Task CompletePasswordReset_WithUnconfirmedPhone_ShouldFail()
    {
        using var database = new TestDatabase();
        await using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "unconfirmed-phone-user",
            UserName = "unconfirmed-phone-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = false,
            IsActive = true,
            PasswordHash = "old-password-hash"
        };

        var otp = new PasswordResetOtp
        {
            UserId = user.Id,
            CodeHash = "test-hash",
            SentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            VerifiedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        context.PasswordResetOtps.Add(otp);
        await context.SaveChangesAsync();

        var oldPasswordHash = user.PasswordHash;

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            new FakeSystemSmsService());

        var result = await service.CompletePasswordResetAsync(
            otp.Id,
            "NewPassword123!");

        Assert.False(result.IsSucceeded);
        Assert.Equal(oldPasswordHash, user.PasswordHash);

        var savedOtp = await context.PasswordResetOtps
            .SingleAsync(x => x.Id == otp.Id);

        Assert.Null(savedOtp.ConsumedAt);
    }

    [PasswordResetSqlServerFact]
    public async Task CompletePasswordReset_ConcurrentRequests_ShouldSucceedOnlyOnce()
    {
        var database = new PasswordResetSqlServerTestDatabase();

        const string testUserId = "otp-complete-concurrency-user";
        const string testMobile = "09123456787";
        const string oldPasswordHash = "old-password-hash";
        const string newPassword = "NewPassword123!";

        int otpId;

        // آماده‌سازی کاربر و OTP تأییدشده
        await using (var context = database.NewContext())
        {
            await context.PasswordResetOtps
                .IgnoreQueryFilters()
                .Where(x => x.UserId == testUserId)
                .ExecuteDeleteAsync();

            var oldUser = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == testUserId);

            if (oldUser is not null)
                context.Users.Remove(oldUser);

            await context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                Id = testUserId,
                UserName = testMobile,
                PhoneNumber = testMobile,
                PhoneNumberConfirmed = true,
                IsActive = true,
                PasswordHash = oldPasswordHash
            };

            context.Users.Add(user);

            var now = DateTime.UtcNow;

            var otp = new PasswordResetOtp
            {
                UserId = testUserId,
                CodeHash = "test-hash",
                SentAt = now.AddMinutes(-2),
                ExpiresAt = now.AddMinutes(3),
                VerifiedAt = now.AddMinutes(-1),
                FailedAttempts = 0,
                CreatedAt = now
            };

            context.PasswordResetOtps.Add(otp);

            await context.SaveChangesAsync();

            otpId = otp.Id;
        }

        // هر درخواست باید Context مستقل داشته باشد.
        async Task<PasswordResetOtpCompletionResult> CompleteAsync()
        {
            await using var context = database.NewContext();

            var userManager = new FakeUserManager(
                new UserStore<ApplicationUser>(context));

            var service = new PasswordResetOtpService(
                context,
                userManager,
                new PasswordHasher<ApplicationUser>(),
                new FakeSystemSmsService());

            return await service.CompletePasswordResetAsync(
                otpId,
                newPassword);
        }

        // اجرای هم‌زمان دو درخواست برای یک OTP
        var results = await Task.WhenAll(
            CompleteAsync(),
            CompleteAsync());

        Assert.Single(results.Where(x => x.IsSucceeded));
        Assert.Single(results.Where(x => !x.IsSucceeded));

        // بررسی وضعیت نهایی دیتابیس
        await using var verificationContext = database.NewContext();

        var savedUser = await verificationContext.Users
            .SingleAsync(x => x.Id == testUserId);

        Assert.NotNull(savedUser.PasswordHash);
        Assert.NotEqual(oldPasswordHash, savedUser.PasswordHash);

        var hasher = new PasswordHasher<ApplicationUser>();

        var passwordVerification = hasher.VerifyHashedPassword(
            savedUser,
            savedUser.PasswordHash,
            newPassword);

        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            passwordVerification);

        var savedOtp = await verificationContext.PasswordResetOtps
            .SingleAsync(x => x.Id == otpId);

        Assert.NotNull(savedOtp.ConsumedAt);
    }

    [Fact]
    public async Task RequestCode_WithUnconfirmedPhone_ShouldNotSendSms()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "otp-unconfirmed-user",
            UserName = "otp-unconfirmed-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = false,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var smsService = new FakeSystemSmsService();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            smsService);

        var result = await service.RequestCodeAsync(user.PhoneNumber);

        // پاسخ عمومی برای جلوگیری از تشخیص شماره‌های ثبت‌شده
        Assert.True(result.IsAccepted);

        Assert.Equal(0, smsService.SendCount);
        Assert.Empty(context.PasswordResetOtps);
    }

    [Fact]
    public async Task RequestCode_WithInactiveUser_ShouldNotSendSms()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var user = new ApplicationUser
        {
            Id = "otp-inactive-user",
            UserName = "otp-inactive-user",
            PhoneNumber = "09123456789",
            PhoneNumberConfirmed = true,
            IsActive = false
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var smsService = new FakeSystemSmsService();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            smsService);

        var result = await service.RequestCodeAsync(user.PhoneNumber);

        Assert.True(result.IsAccepted);
        Assert.Equal(0, smsService.SendCount);
        Assert.Empty(context.PasswordResetOtps);
    }

    [Fact]
    public async Task RequestCode_WithUnknownMobile_ShouldNotRevealAccountExistence()
    {
        using var database = new TestDatabase();
        using var context = database.NewContext();

        var smsService = new FakeSystemSmsService();

        var service = new PasswordResetOtpService(
            context,
            CreateUserManager(context),
            new PasswordHasher<ApplicationUser>(),
            smsService);

        var result = await service.RequestCodeAsync("09111111111");

        Assert.True(result.IsAccepted);
        Assert.Equal(0, smsService.SendCount);
        Assert.Empty(context.PasswordResetOtps);
    }

}