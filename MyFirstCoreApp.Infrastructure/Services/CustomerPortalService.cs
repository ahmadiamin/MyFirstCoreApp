using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Reports;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class CustomerPortalService : ICustomerPortalService
{
    private const int InvoicePageSize = 50;
    private readonly ApplicationDbContext _context;
    private readonly IReportService _reports;
    private readonly TimeProvider _timeProvider;
    private readonly IDataProtector _tokenProtector;

    public CustomerPortalService(ApplicationDbContext context, IReportService reports,
        TimeProvider timeProvider, IDataProtectionProvider dataProtectionProvider)
    {
        _context = context;
        _reports = reports;
        _timeProvider = timeProvider;
        _tokenProtector = dataProtectionProvider.CreateProtector("MyFirstCoreApp.CustomerPortalShare.Token.v1");
    }

    public async Task<IReadOnlyList<CustomerPortalShareListItem>> GetSharesAsync(int tenantId, int contactId)
    {
        await EnsureCustomerAsync(tenantId, contactId);
        var shares = await _context.CustomerPortalShares.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId 
                && !x.IsDeleted && x.ContactId == contactId 
                && x.Contact.TenantId == tenantId 
                && !x.Contact.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CustomerPortalShareListItem
            {
                Id = x.Id,
                ContactId = x.ContactId,
                ContactName = x.Contact.Name,
                Token = x.ProtectedToken,
                LastSmsRequestId = x.LastSmsRequestId,
                LastSmsSentAtUtc = x.LastSmsSentAtUtc,
                CanViewLedger = x.CanViewLedger,
                CanViewSalesInvoices = x.CanViewSalesInvoices,
                CreatedAtUtc = x.CreatedAt,
                ExpiresAtUtc = x.ExpiresAtUtc,
                RevokedAtUtc = x.RevokedAtUtc
            }).ToListAsync();
        foreach (var share in shares)
            share.Token = TryUnprotectToken(share.Token);
        return shares;
    }

    public async Task<CustomerPortalCreatedShare> CreateShareAsync(
        int tenantId, int contactId, bool canViewLedger, bool canViewSalesInvoices,
        DateTime? expiresAtUtc, string? createdBy)
    {
        if (!canViewLedger && !canViewSalesInvoices)
            throw new InvalidOperationException("حداقل یک نوع دسترسی برای لینک انتخاب کنید.");
        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= _timeProvider.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("تاریخ انقضای لینک باید در آینده باشد.");

        await EnsureCustomerAsync(tenantId, contactId);
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var entity = new CustomerPortalShare
        {
            TenantId = tenantId,
            ContactId = contactId,
            TokenHash = HashToken(token),
            ProtectedToken = _tokenProtector.Protect(token),
            CanViewLedger = canViewLedger,
            CanViewSalesInvoices = canViewSalesInvoices,
            ExpiresAtUtc = expiresAtUtc,
            CreatedBy = createdBy
        };

        _context.CustomerPortalShares.Add(entity);
        await _context.SaveChangesAsync();
        return new CustomerPortalCreatedShare
        {
            Id = entity.Id,
            Token = token,
            ExpiresAtUtc = entity.ExpiresAtUtc
        };
    }

    public async Task UpdateShareAsync(int tenantId, int contactId, int shareId, CustomerPortalShareUpdate update)
    {
        if (!update.CanViewLedger && !update.CanViewSalesInvoices)
            throw new InvalidOperationException("حداقل یک نوع دسترسی برای لینک انتخاب کنید.");
        if (update.ExpiresAtUtc.HasValue && update.ExpiresAtUtc.Value <= _timeProvider.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("تاریخ انقضای لینک باید در آینده باشد.");

        var share = await _context.CustomerPortalShares.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == shareId && x.TenantId == tenantId &&
                                      x.ContactId == contactId && !x.IsDeleted)
            ?? throw new InvalidOperationException("لینک دسترسی پیدا نشد.");
        if (share.RevokedAtUtc.HasValue)
            throw new InvalidOperationException("لینک لغوشده قابل ویرایش نیست.");

        share.CanViewLedger = update.CanViewLedger;
        share.CanViewSalesInvoices = update.CanViewSalesInvoices;
        share.ExpiresAtUtc = update.ExpiresAtUtc;
        share.UpdatedBy = update.UpdatedBy;
        await _context.SaveChangesAsync();
    }

    public async Task RevokeShareAsync(int tenantId, int shareId)
    {
        var share = await _context.CustomerPortalShares.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == shareId && x.TenantId == tenantId && !x.IsDeleted)
            ?? throw new InvalidOperationException("لینک دسترسی پیدا نشد.");
        if (share.RevokedAtUtc.HasValue) return;
        share.RevokedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();
    }

    public async Task<CustomerPortalPage?> GetPortalAsync(
        string token, DateOnly today, int page = 1, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAsync(token, cancellationToken);
        if (access is null) return null;

        ContactReport? ledger = null;
        if (access.CanViewLedger)
        {
            var fiscalYear = await _context.FiscalYears.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == access.TenantId && !x.IsDeleted &&
                            x.StartDate <= today && x.EndDate >= today)
                .Select(x => new { x.StartDate })
                .FirstOrDefaultAsync(cancellationToken);
            var from = fiscalYear?.StartDate ?? PersianDate.StartOfYear(today);
            ledger = await _reports.GetContactAsync(access.TenantId,
                new ContactReportQuery
                {
                    ContactId = access.ContactId,
                    From = from,
                    To = today,
                    OnlyPosted = true
                }, cancellationToken);
        }

        var invoiceRows = new List<CustomerPortalInvoiceRow>();
        var invoicePage = 1;
        var invoicePageCount = 1;
        if (access.CanViewSalesInvoices)
        {
            var query = PublicSalesInvoices(access.TenantId, access.ContactId);
            var count = await query.CountAsync(cancellationToken);
            invoicePageCount = Math.Max(1, (int)Math.Ceiling(count / (double)InvoicePageSize));
            invoicePage = Math.Clamp(page, 1, invoicePageCount);
            invoiceRows = await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Number)
                .Skip((invoicePage - 1) * InvoicePageSize).Take(InvoicePageSize)
                .Select(x => new CustomerPortalInvoiceRow
                {
                    Id = x.Id,
                    Number = x.Number,
                    Date = x.Date,
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    RemainingAmount = x.TotalAmount - x.PaidAmount - x.SettlementDiscount
                }).ToListAsync(cancellationToken);
        }

        var tenantName = await _context.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == access.TenantId && !x.IsDeleted && x.IsActive)
            .Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);

        return new CustomerPortalPage
        {
            CustomerName = access.ContactName,
            TenantName = tenantName,
            CanViewLedger = access.CanViewLedger,
            CanViewSalesInvoices = access.CanViewSalesInvoices,
            Ledger = ledger,
            Invoices = invoiceRows,
            InvoicePage = invoicePage,
            InvoicePageCount = invoicePageCount
        };
    }

    public async Task<CustomerPortalInvoiceDetails?> GetInvoiceAsync(
        string token, int invoiceId, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAsync(token, cancellationToken);
        if (access is null || !access.CanViewSalesInvoices) return null;

        var invoice = await PublicSalesInvoices(access.TenantId, access.ContactId)
            .Where(x => x.Id == invoiceId)
            .Select(x => new CustomerPortalInvoiceDetails
            {
                Invoice = new CustomerPortalInvoiceRow
                {
                    Id = x.Id,
                    Number = x.Number,
                    Date = x.Date,
                    TotalAmount = x.TotalAmount,
                    PaidAmount = x.PaidAmount,
                    RemainingAmount = x.TotalAmount - x.PaidAmount - x.SettlementDiscount
                },
                Description = x.Description
            }).FirstOrDefaultAsync(cancellationToken);
        if (invoice is null) return null;

        var items = await _context.InvoiceItems.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == access.TenantId && !x.IsDeleted && x.InvoiceId == invoiceId)
            .OrderBy(x => x.Id)
            .Select(x => new CustomerPortalInvoiceItem
            {
                Description = x.Description ?? (x.Product.TenantId == access.TenantId && !x.Product.IsDeleted
                    ? x.Product.Name : "قلم فاکتور"),
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                LineTotal = x.LineTotal
            }).ToListAsync(cancellationToken);

        return new CustomerPortalInvoiceDetails { Invoice = invoice.Invoice, Description = invoice.Description, Items = items };
    }

    private IQueryable<MyFirstCoreApp.Domain.Entities.Invoicing.Invoice> PublicSalesInvoices(int tenantId, int contactId) =>
        _context.Invoices.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.ContactId == contactId &&
                        x.InvoiceType == InvoiceType.Sale && x.Status == InvoiceStatus.Posted);

    private async Task<AccessInfo?> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 40 or > 64) return null;
        var hash = HashToken(token);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var share = await _context.CustomerPortalShares.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TokenHash == hash && !x.IsDeleted && !x.RevokedAtUtc.HasValue &&
                        (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now))
            .Select(x => new AccessInfo(x.TenantId, x.ContactId, x.CanViewLedger, x.CanViewSalesInvoices, x.Contact.Name))
            .FirstOrDefaultAsync(cancellationToken);
        if (share is null) return null;

        var tenantActive = await _context.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.Id == share.TenantId && !x.IsDeleted && x.IsActive, cancellationToken);
        var contactActive = await _context.Contacts.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.Id == share.ContactId && x.TenantId == share.TenantId && !x.IsDeleted && x.IsActive &&
                           (x.ContactType == ContactType.Customer || x.ContactType == ContactType.Both), cancellationToken);
        return tenantActive && contactActive ? share : null;
    }

    private async Task EnsureCustomerAsync(int tenantId, int contactId)
    {
        var exists = await _context.Contacts.IgnoreQueryFilters().AnyAsync(x =>
            x.Id == contactId && x.TenantId == tenantId && !x.IsDeleted && x.IsActive &&
            (x.ContactType == ContactType.Customer || x.ContactType == ContactType.Both));
        if (!exists) throw new InvalidOperationException("مشتری پیدا نشد یا فعال نیست.");
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string? TryUnprotectToken(string? protectedToken)
    {
        if (string.IsNullOrWhiteSpace(protectedToken)) return null;
        try { return _tokenProtector.Unprotect(protectedToken); }
        catch (CryptographicException) { return null; }
    }

    private sealed record AccessInfo(int TenantId, int ContactId, bool CanViewLedger,
        bool CanViewSalesInvoices, string ContactName);
}
