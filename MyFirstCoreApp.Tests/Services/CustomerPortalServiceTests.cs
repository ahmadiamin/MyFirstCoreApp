using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class CustomerPortalServiceTests : IDisposable
{
    private const int TenantId = 1;
    private readonly TestDatabase _db = new();
    private readonly IDataProtectionProvider _dataProtection = new EphemeralDataProtectionProvider();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Share_StoresOnlyTokenHash_AndShowsOnlyPostedSalesInvoicesForItsCustomer()
    {
        _db.Tenant.TenantId = TenantId;
        int contactId;
        await using (var context = _db.NewContext())
        {
            context.Tenants.Add(new Tenant { Id = TenantId, Name = "شرکت تست", IsActive = true });
            var contact = new Contact
            {
                TenantId = TenantId,
                Name = "مشتری پورتال",
                ContactType = ContactType.Customer,
                IsActive = true
            };
            context.Contacts.Add(contact);
            await context.SaveChangesAsync();
            contactId = contact.Id;

            context.Invoices.AddRange(
                MakeInvoice(contactId, InvoiceType.Sale, InvoiceStatus.Posted, 100m, 25m),
                MakeInvoice(contactId, InvoiceType.Sale, InvoiceStatus.Draft, 500m, 0m),
                MakeInvoice(contactId, InvoiceType.Purchase, InvoiceStatus.Posted, 300m, 0m));
            await context.SaveChangesAsync();

            var unit = new Unit { TenantId = TenantId, Name = "عدد", Symbol = "عدد", IsActive = true };
            context.Units.Add(unit);
            await context.SaveChangesAsync();
            var product = new Product { TenantId = TenantId, Name = "کالای مشتری", UnitId = unit.Id, IsActive = true };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            var postedInvoiceId = await context.Invoices
                .Where(x => x.ContactId == contactId && x.InvoiceType == InvoiceType.Sale && x.Status == InvoiceStatus.Posted)
                .Select(x => x.Id).SingleAsync();
            context.InvoiceItems.Add(new InvoiceItem
            {
                TenantId = TenantId,
                InvoiceId = postedInvoiceId,
                ProductId = product.Id,
                Quantity = 1m,
                UnitPrice = 100m,
                LineTotal = 100m
            });
            await context.SaveChangesAsync();
        }

        await using var serviceContext = _db.NewContext();
        var service = CreateService(serviceContext);
        var created = await service.CreateShareAsync(
            TenantId, contactId, canViewLedger: false, canViewSalesInvoices: true,
            expiresAtUtc: null, createdBy: "tester");

        var stored = await serviceContext.CustomerPortalShares.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.NotEqual(created.Token, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal(43, created.Token.Length);
        Assert.NotNull(stored.ProtectedToken);
        Assert.NotEqual(created.Token, stored.ProtectedToken);
        var listed = Assert.Single(await service.GetSharesAsync(TenantId, contactId));
        Assert.Equal(created.Token, listed.Token);

        var portal = await service.GetPortalAsync(created.Token, new DateOnly(2026, 3, 21));
        Assert.NotNull(portal);
        Assert.False(portal.CanViewLedger);
        var invoice = Assert.Single(portal.Invoices);
        Assert.Equal(100m, invoice.TotalAmount);
        Assert.Equal(75m, invoice.RemainingAmount);

        var details = await service.GetInvoiceAsync(created.Token, invoice.Id);
        Assert.NotNull(details);
        Assert.Equal(invoice.Id, details.Invoice.Id);
        Assert.Equal("کالای مشتری", Assert.Single(details.Items).Description);

        var expiry = DateTime.UtcNow.AddDays(10);
        await service.UpdateShareAsync(TenantId, contactId, created.Id,
            new CustomerPortalShareUpdate
            {
                CanViewLedger = true,
                CanViewSalesInvoices = true,
                ExpiresAtUtc = expiry,
                UpdatedBy = "tester"
            });
        var edited = Assert.Single(await service.GetSharesAsync(TenantId, contactId));
        Assert.Equal(created.Token, edited.Token);
        Assert.True(edited.CanViewLedger);
        Assert.Equal(expiry, edited.ExpiresAtUtc);

        await service.RevokeShareAsync(TenantId, created.Id);
        Assert.Null(await service.GetPortalAsync(created.Token, new DateOnly(2026, 3, 21)));
        Assert.Null(await service.GetInvoiceAsync(created.Token, invoice.Id));
    }

    [Fact]
    public async Task Share_RequiresAtLeastOnePermission_AndDoesNotCrossTenantOnRevocation()
    {
        _db.Tenant.TenantId = TenantId;
        int contactId;
        await using (var context = _db.NewContext())
        {
            context.Tenants.Add(new Tenant { Id = TenantId, Name = "شرکت تست", IsActive = true });
            var contact = new Contact
            {
                TenantId = TenantId,
                Name = "مشتری تست",
                ContactType = ContactType.Customer,
                IsActive = true
            };
            context.Contacts.Add(contact);
            await context.SaveChangesAsync();
            contactId = contact.Id;
        }

        await using var serviceContext = _db.NewContext();
        var service = CreateService(serviceContext);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateShareAsync(
            TenantId, contactId, false, false, null, "tester"));

        var created = await service.CreateShareAsync(TenantId, contactId, false, true, null, "tester");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevokeShareAsync(2, created.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateShareAsync(2, contactId, created.Id,
            new CustomerPortalShareUpdate { CanViewSalesInvoices = true }));
        Assert.NotNull(await service.GetPortalAsync(created.Token, new DateOnly(2026, 3, 21)));
    }

    private static Invoice MakeInvoice(int contactId, InvoiceType type, InvoiceStatus status,
        decimal total, decimal paid) => new()
    {
        TenantId = TenantId,
        ContactId = contactId,
        InvoiceType = type,
        Status = status,
        Number = (int)type * 100 + (int)status,
        Date = new DateOnly(2026, 3, 21),
        TotalAmount = total,
        PaidAmount = paid,
        SettlementDiscount = 0m
    };

    private CustomerPortalService CreateService(ApplicationDbContext context) =>
        new(context, reports: null!, TimeProvider.System, _dataProtection);
}
