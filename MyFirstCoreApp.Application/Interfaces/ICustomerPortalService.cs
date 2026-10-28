using MyFirstCoreApp.Application.CustomerPortal;

namespace MyFirstCoreApp.Application.Interfaces;

public interface ICustomerPortalService
{
    Task<IReadOnlyList<CustomerPortalShareListItem>> GetSharesAsync(int tenantId, int contactId);
    Task<CustomerPortalCreatedShare> CreateShareAsync(
        int tenantId, int contactId, bool canViewLedger, bool canViewSalesInvoices,
        DateTime? expiresAtUtc, string? createdBy);
    Task UpdateShareAsync(int tenantId, int contactId, int shareId, CustomerPortalShareUpdate update);
    Task RevokeShareAsync(int tenantId, int shareId);
    Task<CustomerPortalPage?> GetPortalAsync(
        string token, DateOnly today, int page = 1, CancellationToken cancellationToken = default);
    Task<CustomerPortalInvoiceDetails?> GetInvoiceAsync(
        string token, int invoiceId, CancellationToken cancellationToken = default);
}
