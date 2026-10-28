using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class ChequeReminderHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ChequeReminderHostedService> _logger;

    public ChequeReminderHostedService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
        ILogger<ChequeReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Cheque reminder processing failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sms = scope.ServiceProvider.GetRequiredService<IFarazSmsService>();
        var localNow = _timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var settings = await context.TenantSmsSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => !x.IsDeleted && x.ChequeReminderEnabled &&
                (x.ChequeReminderDayBeforeEnabled || x.ChequeReminderDueDayEnabled))
            .ToListAsync(cancellationToken);

        foreach (var setting in settings)
        {
            var checks = await context.Cheques.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == setting.TenantId && !x.IsDeleted &&
                    (x.Status == ChequeStatus.InProgress || x.Status == ChequeStatus.RePresented) &&
                    ((setting.ChequeReminderDayBeforeEnabled &&
                      (x.DueDate == today.AddDays(1) || x.DueDate == today.AddDays(2))) ||
                     (setting.ChequeReminderDueDayEnabled &&
                      (x.DueDate == today || x.DueDate == today.AddDays(1)))))
                .Select(x => new ReminderCheque(
                    x.Id, x.TenantId, x.ChequeNumber, x.Amount, x.DueDate, x.Direction,
                    x.BankAccount != null && !x.BankAccount.IsDeleted &&
                    x.BankAccount.TenantId == setting.TenantId ? x.BankAccount.Name : "حساب نامشخص"))
                .ToListAsync(cancellationToken);

            foreach (var cheque in checks)
            {
                var reminders = new List<(ChequeSmsReminderKind Kind, string Label)>();
                if (setting.ChequeReminderDayBeforeEnabled &&
                    (cheque.DueDate == today.AddDays(1) || cheque.DueDate == today.AddDays(2)))
                    reminders.Add((ChequeSmsReminderKind.DayBefore,
                        "\u06CC\u06A9 \u0631\u0648\u0632 \u0645\u0627\u0646\u062F\u0647"));
                if (setting.ChequeReminderDueDayEnabled &&
                    (cheque.DueDate == today || cheque.DueDate == today.AddDays(1)))
                    reminders.Add((ChequeSmsReminderKind.DueDay, "\u0627\u0645\u0631\u0648\u0632"));

                foreach (var reminder in reminders)
                {
                    var directionLabel = cheque.Direction == ChequeDirection.Received
                        ? "\u0686\u06A9 \u062F\u0631\u06CC\u0627\u0641\u062A\u06CC"
                        : "\u0686\u06A9 \u067E\u0631\u062F\u0627\u062E\u062A\u06CC";
                    await SendOnceAsync(context, sms, _timeProvider, setting, cheque,
                        reminder.Kind, $"{directionLabel} - {reminder.Label}", utcNow, cancellationToken);
                }
            }
        }
    }

    private static async Task SendOnceAsync(ApplicationDbContext context, IFarazSmsService sms,
        TimeProvider timeProvider, TenantSmsSetting setting, ReminderCheque cheque,
        ChequeSmsReminderKind kind, string label, DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var dispatch = await context.ChequeSmsReminderDispatches.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == cheque.TenantId && x.ChequeId == cheque.Id &&
                x.DueDate == cheque.DueDate && x.Kind == kind, cancellationToken);
        var recipientMobile = setting.ChequeReminderMobile!;
        var recipientChanged = dispatch is not null &&
            !string.Equals(dispatch.RecipientMobile, recipientMobile, StringComparison.Ordinal);

        if (dispatch is null)
        {
            dispatch = new ChequeSmsReminderDispatch
            {
                TenantId = cheque.TenantId,
                ChequeId = cheque.Id,
                DueDate = cheque.DueDate,
                Kind = kind,
                Status = ChequeSmsReminderStatus.Pending,
                CreatedAt = utcNow,
                CreatedBy = "system"
            };
            context.ChequeSmsReminderDispatches.Add(dispatch);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
                return;
            }
        }
        else if (dispatch.Status == ChequeSmsReminderStatus.Accepted && !recipientChanged)
        {
            return;
        }
        else if (!recipientChanged &&
                 (dispatch.AttemptCount >= 3 ||
                  (dispatch.Status == ChequeSmsReminderStatus.Pending &&
                   dispatch.LastAttemptAtUtc > utcNow.AddMinutes(-10)) ||
                  (dispatch.NextAttemptAtUtc.HasValue && dispatch.NextAttemptAtUtc > utcNow)))
        {
            return;
        }

        if (recipientChanged)
        {
            dispatch.AttemptCount = 0;
            dispatch.NextAttemptAtUtc = null;
        }

        var localNow = timeProvider.GetLocalNow();
        utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var targetDate = kind == ChequeSmsReminderKind.DayBefore
            ? cheque.DueDate.AddDays(-1)
            : cheque.DueDate;
        var targetTime = targetDate.ToDateTime(setting.ChequeReminderTime);
        var scheduledFor = targetDate > today ||
                           (targetDate == today && targetTime > localNow.DateTime.AddMinutes(5))
            ? targetTime
            : (DateTime?)null;

        dispatch.Status = ChequeSmsReminderStatus.Pending;
        dispatch.RecipientMobile = recipientMobile;
        dispatch.AttemptCount++;
        dispatch.LastAttemptAtUtc = utcNow;
        dispatch.NextAttemptAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);

        var result = string.IsNullOrWhiteSpace(recipientMobile)
            ? new MyFirstCoreApp.Application.CustomerPortal.FarazSmsSendResult(false, null,
                "شماره موبایل شرکت ثبت نشده است.")
            : await sms.SendChequeReminderAsync(cheque.TenantId, recipientMobile,
                cheque.ChequeNumber, cheque.Amount, cheque.DueDate, cheque.BankAccountName,
                label, scheduledFor, cancellationToken);

        dispatch.Status = result.IsAccepted ? ChequeSmsReminderStatus.Accepted : ChequeSmsReminderStatus.Failed;
        dispatch.ProviderRequestId = result.RequestId;
        dispatch.ScheduledAtLocal = scheduledFor;
        dispatch.LastError = result.IsAccepted ? null : result.Message;
        dispatch.NextAttemptAtUtc = result.IsAccepted || dispatch.AttemptCount >= 3
            ? null
            : utcNow.AddHours(1);
        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record ReminderCheque(int Id, int TenantId, string ChequeNumber, decimal Amount,
        DateOnly DueDate, ChequeDirection Direction, string BankAccountName);
}
