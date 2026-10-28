using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Banking;

public sealed class ChequeSmsReminderDispatch : TenantEntity
{
    public int ChequeId { get; set; }
    public DateOnly DueDate { get; set; }
    public ChequeSmsReminderKind Kind { get; set; }
    public ChequeSmsReminderStatus Status { get; set; }
    public string? RecipientMobile { get; set; }
    public string? ProviderRequestId { get; set; }
    public DateTime? ScheduledAtLocal { get; set; }
    public DateTime LastAttemptAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}

public enum ChequeSmsReminderKind
{
    DayBefore = 1,
    DueDay = 2
}

public enum ChequeSmsReminderStatus
{
    Pending = 1,
    Accepted = 2,
    Failed = 3
}
