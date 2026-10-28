using MyFirstCoreApp.Application.CustomerPortal;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IFarazSmsService
{
    Task<FarazSmsSendResult> SendPortalLinkAsync(
        int tenantId, int contactId, int shareId, string mobile, string portalUrl, string? sentBy,
        CancellationToken cancellationToken = default);

    Task<FarazSmsSendResult> SendChequeReminderAsync(
        int tenantId, string mobile, string chequeNumber, decimal amount, DateOnly dueDate,
        string bankAccountName, string reminderKind, DateTime? scheduleIranLocal,
        CancellationToken cancellationToken = default);

    Task<FarazSmsDeliveryReport?> GetLatestDeliveryReportAsync(
        int tenantId, int contactId, int shareId, CancellationToken cancellationToken = default);
}
