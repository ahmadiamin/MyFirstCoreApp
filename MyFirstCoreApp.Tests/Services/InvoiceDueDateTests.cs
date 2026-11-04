using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Application.Invoicing;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>سررسید پرداخت فاکتور: ثبت، ویرایش، تمدید و قواعد آن.</summary>
public class InvoiceDueDateTests : IDisposable
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

    public InvoiceDueDateTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // ثبت و ویرایش
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_StoresTheDueDate_AndNoDueDateMeansNull()
    {
        var contact = await AddContactAsync();

        var withDue = await CreateAsync(NewRequest(InvoiceType.Sale, contact, due: new DateOnly(2026, 4, 20)));
        var without = await CreateAsync(NewRequest(InvoiceType.Sale, contact, due: null));

        Assert.Equal(new DateOnly(2026, 4, 20), (await LoadAsync(withDue.Id)).DueDate);
        Assert.Null((await LoadAsync(without.Id)).DueDate);
    }

    [Fact]
    public async Task Create_SameDayDue_IsAllowed_ButBeforeTheInvoiceDateIsRejected()
    {
        var contact = await AddContactAsync();

        await CreateAsync(NewRequest(InvoiceType.Sale, contact, due: new DateOnly(2026, 3, 22)));   // = تاریخ فاکتور

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAsync(NewRequest(InvoiceType.Sale, contact, due: new DateOnly(2026, 3, 21))));

        Assert.Contains("سررسید", ex.Message);
    }

    [Fact]
    public async Task PreInvoice_NeverHasADueDate()
    {
        var contact = await AddContactAsync();

        // حتی با سررسیدِ نامعتبر (قبل از تاریخ) خطا نمی‌دهد؛ پیش‌فاکتور سررسید ندارد و نادیده گرفته می‌شود
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact, due: new DateOnly(2026, 5, 1)));

        Assert.Null((await LoadAsync(pre.Id)).DueDate);
    }

    [Fact]
    public async Task Update_CanChangeAndClearTheDueDate()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(InvoiceType.Sale, contact, due: new DateOnly(2026, 4, 20)));

        var edit = NewRequest(InvoiceType.Sale, contact, due: new DateOnly(2026, 5, 22));
        edit.Id = created.Id;
        await using (var context = _db.NewContext())
            await NewService(context).UpdateAsync(edit, TenantA);

        Assert.Equal(new DateOnly(2026, 5, 22), (await LoadAsync(created.Id)).DueDate);

        var clear = NewRequest(InvoiceType.Sale, contact, due: null);
        clear.Id = created.Id;
        await using (var context = _db.NewContext())
            await NewService(context).UpdateAsync(clear, TenantA);

        Assert.Null((await LoadAsync(created.Id)).DueDate);
    }

    [Fact]
    public async Task ConvertingAPreInvoice_ProducesASaleWithoutDueDate()
    {
        var contact = await AddContactAsync();
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact, due: null));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsSentAsync(pre.Id, TenantA);
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsApprovedAsync(pre.Id, TenantA, null);

        Invoice sale;
        await using (var context = _db.NewContext())
            sale = await NewService(context).ConvertToInvoiceAsync(pre.Id, TenantA);

        Assert.Null((await LoadAsync(sale.Id)).DueDate);
    }

    // ---------------------------------------------------------------
    // تمدید
    // ---------------------------------------------------------------

    [Fact]
    public async Task Extend_ChangesThePostedInvoiceDueDate_AndLogsBothPersianDatesAndTheReason()
    {
        var invoice = await CreatePostedAsync(due: new DateOnly(2026, 4, 20));          // 1405/01/31

        await using (var context = _db.NewContext())
            await NewService(context).ExtendDueDateAsync(invoice.Id, TenantA, new DateOnly(2026, 5, 22), "توافق تلفنی");   // 1405/03/01

        Assert.Equal(new DateOnly(2026, 5, 22), (await LoadAsync(invoice.Id)).DueDate);

        await using var verify = _db.NewContext();
        var history = await verify.InvoiceHistories
            .Where(h => h.InvoiceId == invoice.Id)
            .OrderByDescending(h => h.Id)
            .FirstAsync();

        Assert.Contains("1405/01/31", history.Description);
        Assert.Contains("1405/03/01", history.Description);
        Assert.Contains("توافق تلفنی", history.Description);
        Assert.Equal(InvoiceStatus.Posted, history.ToStatus);       // وضعیت عوض نشده
    }

    [Fact]
    public async Task Extend_CanSetADueDateOnAPostedInvoiceThatHadNone()
    {
        var invoice = await CreatePostedAsync(due: null);

        await using (var context = _db.NewContext())
            await NewService(context).ExtendDueDateAsync(invoice.Id, TenantA, new DateOnly(2026, 6, 1), null);

        await using var verify = _db.NewContext();
        var history = await verify.InvoiceHistories.Where(h => h.InvoiceId == invoice.Id).OrderByDescending(h => h.Id).FirstAsync();

        Assert.Equal(new DateOnly(2026, 6, 1), (await LoadAsync(invoice.Id)).DueDate);
        Assert.Contains("بدون سررسید", history.Description);
    }

    [Fact]
    public async Task Extend_IsRejectedForDraftsPreInvoicesSettledInvoicesAndBadDates()
    {
        var contact = await AddContactAsync();

        var draft = await CreateAsync(NewRequest(InvoiceType.Sale, contact, due: new DateOnly(2026, 4, 20)));
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact, due: null));
        var posted = await CreatePostedAsync(due: new DateOnly(2026, 4, 20));
        var settled = await CreatePostedAsync(due: new DateOnly(2026, 4, 20));

        await using (var context = _db.NewContext())
        {
            var entity = await context.Invoices.SingleAsync(i => i.Id == settled.Id);
            entity.PaidAmount = entity.TotalAmount;
            await context.SaveChangesAsync();
        }

        var later = new DateOnly(2026, 6, 1);

        await AssertExtendRejectedAsync(draft.Id, later, "قطعی");
        await AssertExtendRejectedAsync(pre.Id, later, "پیش‌فاکتور");
        await AssertExtendRejectedAsync(settled.Id, later, "تسویه");
        await AssertExtendRejectedAsync(posted.Id, default, "الزامی");
        await AssertExtendRejectedAsync(posted.Id, new DateOnly(2026, 3, 1), "قبل از تاریخ فاکتور");
        await AssertExtendRejectedAsync(posted.Id, new DateOnly(2026, 4, 20), "همین");      // همان تاریخ فعلی
    }

    [Fact]
    public async Task Extend_CannotTouchAnotherTenantsInvoice()
    {
        var invoice = await CreatePostedAsync(due: new DateOnly(2026, 4, 20));

        _db.Tenant.TenantId = TenantB;

        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(context).ExtendDueDateAsync(invoice.Id, TenantB, new DateOnly(2026, 6, 1), null));

        Assert.Contains("پیدا نشد", ex.Message);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task AssertExtendRejectedAsync(int invoiceId, DateOnly newDue, string expectedPart)
    {
        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(context).ExtendDueDateAsync(invoiceId, TenantA, newDue, null));

        Assert.Contains(expectedPart, ex.Message);
    }

    private InvoiceService NewService(ApplicationDbContext context)
        => new(new StockMovementService(context, new WarehouseService(context)), context, new TransactionService(context), new SystemAccountService(context), _db.User, _time);

    private static InvoiceRequest NewRequest(InvoiceType type, int contactId, DateOnly? due)
        => new()
        {
            Date = new DateOnly(2026, 3, 22),
            DueDate = due,
            InvoiceType = type,
            ContactId = contactId,
            Items =
            {
                new InvoiceItemRequest { NewProductName = "کالا " + Guid.NewGuid().ToString("N")[..6], Quantity = 1, UnitPrice = 1_000 }
            }
        };

    private async Task<Invoice> CreateAsync(InvoiceRequest request)
    {
        await using var context = _db.NewContext();
        return await NewService(context).CreateAsync(request, TenantA);
    }

    private async Task EnsureDefaultWarehouseAsync()
    {
        await using var context = _db.NewContext();

        var exists = await context.Warehouses.AnyAsync(w =>
            w.TenantId == TenantA &&
            w.IsActive &&
            w.IsDefault);

        if (!exists)
            await _db.AddDefaultWarehouseAsync(TenantA);
    }

    private async Task<Invoice> CreatePostedAsync(DateOnly? due)
    {
        await EnsureDefaultWarehouseAsync();

        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(InvoiceType.Sale, contact, due));

        await AddOpeningStockForInvoiceAsync(created.Id);

        await using var context = _db.NewContext();
        await NewService(context).PostAsync(created.Id, TenantA);

        return created;
    }

    private async Task<Invoice> LoadAsync(int id)
    {
        await using var context = _db.NewContext();
        return await context.Invoices.SingleAsync(i => i.Id == id);
    }

    private async Task<int> AddContactAsync()
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = "مشتری " + Guid.NewGuid().ToString("N")[..6], ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }
    private async Task AddOpeningStockForInvoiceAsync(int invoiceId)
    {
        var previousTenant = _db.Tenant.TenantId;
        _db.Tenant.TenantId = TenantA;

        try
        {
            await using var context = _db.NewContext();

            var invoice = await context.Invoices
                .Include(x => x.Items)
                .FirstAsync(x =>
                    x.Id == invoiceId &&
                    x.TenantId == TenantA);

            var trackedItems = invoice.Items
                .Where(x => x.ProductId > 0)
                .ToList();

            if (trackedItems.Count == 0)
                return;

            var productIds = trackedItems
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var trackedProductIds = await context.Products
                .Where(x =>
                    x.TenantId == TenantA &&
                    x.TrackInventory &&
                    productIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var trackedProductIdSet = trackedProductIds.ToHashSet();

            var warehouseService = new WarehouseService(context);
            var stockService = new StockMovementService(context, warehouseService);

            foreach (var group in trackedItems
                .Where(x => trackedProductIdSet.Contains(x.ProductId))
                .GroupBy(x => x.WarehouseId))
            {
                var warehouseId = group.Key
                    ?? await warehouseService.GetDefaultWarehouseIdAsync(TenantA);

                var movementId = await stockService.CreateAsync(
                    new StockMovementRequest
                    {
                        Date = invoice.Date.AddDays(-1),
                        MovementType = (int)StockMovementType.Opening,
                        DestinationWarehouseId = warehouseId,
                        Description = "موجودی اولیه تست",
                        Items = group.Select(x => new StockMovementItemRequest
                        {
                            ProductId = x.ProductId,
                            Quantity = x.Quantity,
                            UnitCost = x.UnitPrice
                        }).ToList()
                    },
                    TenantA);

                await stockService.PostAsync(movementId, TenantA);
            }
        }
        finally
        {
            _db.Tenant.TenantId = previousTenant;
        }
    }
}
