using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Tenancy;

public sealed class TenantSmsSetting : BaseEntity
{
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string? ProtectedApiKey { get; set; }
    public string LineNumber { get; set; } = "90008361";
    public string? PortalLinkText { get; set; }
    public bool ChequeReminderEnabled { get; set; }
    public bool ChequeReminderDayBeforeEnabled { get; set; }
    public bool ChequeReminderDueDayEnabled { get; set; }
    public TimeOnly ChequeReminderTime { get; set; } = new(9, 0);
    public string? ChequeReminderMobile { get; set; }
    public string? ChequeReminderText { get; set; }
}
