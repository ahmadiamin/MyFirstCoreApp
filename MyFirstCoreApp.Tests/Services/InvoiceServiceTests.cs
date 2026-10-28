using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Application.Invoicing;
using MyFirstCoreApp.Domain.Entities.Accounting;
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

public class InvoiceServiceTests : IDisposable
{
    private const int TenantA = 1;

    private readonly TestDatabase _db = new();

    // 2026-03-20 21:00 UTC = 2026-03-21 00:30 به وقت ایران ← «امروز» = 2026-03-21
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

    public InvoiceServiceTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // ثبت
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_WithNewContactAndNewProducts_CreatesEverythingTogether()
    {
        var request = NewRequest(InvoiceType.Sale,
            Item("لپ‌تاپ", qty: 2, price: 1_000, tax: 9),
            Item("موس", qty: 1, price: 500));
        request.ContactId = null;
        request.NewContactName = "  مشتری جدید ";

        var created = await CreateAsync(request);

        Assert.Equal(1, created.Number);
        Assert.Equal(InvoiceStatus.Draft, created.Status);
        Assert.Equal(2_680, created.TotalAmount); // 2000 + 180 مالیات + 500

        await using var verify = _db.NewContext();

        var contact = await verify.Contacts.SingleAsync();
        Assert.Equal("مشتری جدید", contact.Name);
        Assert.Equal(ContactType.Customer, contact.ContactType);
        Assert.Equal(contact.Id, (await verify.Invoices.SingleAsync()).ContactId);

        Assert.Equal(2, await verify.Products.CountAsync());
        Assert.Equal(1, await verify.Units.CountAsync());

        var history = await verify.InvoiceHistories.SingleAsync();
        Assert.Equal("user-1", history.UserId);
    }

    [Fact]
    public async Task Create_PurchaseWithNewContact_CreatesSupplier()
    {
        var request = NewRequest(InvoiceType.Purchase, Item("کالا", 1, 100));
        request.ContactId = null;
        request.NewContactName = "فروشنده";

        await CreateAsync(request);

        await using var verify = _db.NewContext();
        Assert.Equal(ContactType.Supplier, (await verify.Contacts.SingleAsync()).ContactType);
    }

    [Fact]
    public async Task Create_ServiceProduct_IgnoresWarehouseSelection()
    {
        int serviceId;
        await using (var context = _db.NewContext())
        {
            var unit = new Unit { TenantId = TenantA, Name = "خدمت", Symbol = "خدمت", IsActive = true };
            context.Units.Add(unit);
            await context.SaveChangesAsync();
            var service = new Product
            {
                TenantId = TenantA,
                Name = "خدمت مشاوره",
                UnitId = unit.Id,
                IsActive = true,
                TrackInventory = false
            };
            context.Products.Add(service);
            await context.SaveChangesAsync();
            serviceId = service.Id;
        }

        var contactId = await AddContactAsync();
        var request = NewRequest(InvoiceType.Sale, contactId,
            new InvoiceItemRequest
            {
                ProductId = serviceId,
                WarehouseId = int.MaxValue,
                Quantity = 1,
                UnitPrice = 1_000
            });

        var created = await CreateAsync(request);

        Assert.Null(created.Items.Single().WarehouseId);
    }

    [Fact]
    public async Task Create_PurchaseUpdatesExistingProductLastPurchasePrice()
    {
        int productId;
        await using (var context = _db.NewContext())
        {
            var unit = new Unit { TenantId = TenantA, Name = "عدد", Symbol = "عدد", IsActive = true };
            context.Units.Add(unit);
            await context.SaveChangesAsync();
            var product = new Product
            {
                TenantId = TenantA,
                Name = "کالای خریداری‌شده",
                UnitId = unit.Id,
                IsActive = true,
                TrackInventory = true,
                LastPurchasePrice = 100
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;
        }

        var contactId = await AddContactAsync();
        await CreateAsync(NewRequest(InvoiceType.Purchase, contactId,
            new InvoiceItemRequest
            {
                ProductId = productId,
                Quantity = 2,
                UnitPrice = 250
            }));

        await using var verify = _db.NewContext();
        var updatedProduct = await verify.Products.SingleAsync(p => p.Id == productId);
        Assert.Equal(250, updatedProduct.LastPurchasePrice);
        Assert.NotNull(updatedProduct.LastPriceUpdatedAt);
    }

    [Fact]
    public async Task Create_InvalidRequests_AreRejected_AndLeaveNothingBehind()
    {
        var cases = new (Action<InvoiceRequest> Mutate, string Expected)[]
        {
            (r => r.Date = default, "تاریخ"),
            (r => { r.ContactId = null; r.NewContactName = null; }, "طرف حساب"),
            (r => r.Items.Clear(), "حداقل یک قلم"),
            (r => r.Items[0].Quantity = 0, "تعداد"),
            (r => r.Items[0].UnitPrice = -1, "منفی"),
            (r => r.Items[0].UnitPrice = 10.5m, "عدد صحیح"),
            (r => r.Items[0].DiscountPercent = 101, "درصد تخفیف"),
            (r => r.Items[0].TaxRate = -1, "نرخ مالیات"),
            (r => r.Items.Add(Item("کالا یک", 1, 100)), "تکراری"),
        };

        foreach (var (mutate, expected) in cases)
        {
            var request = NewRequest(InvoiceType.Sale, Item("کالا یک", 1, 100));
            request.ContactId = null;
            request.NewContactName = "مشتری";
            mutate(request);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(request));
            Assert.Contains(expected, ex.Message);
        }

        await using var verify = _db.NewContext();
        Assert.Equal(0, await verify.Invoices.CountAsync());
        Assert.Equal(0, await verify.Contacts.CountAsync());
        Assert.Equal(0, await verify.Products.CountAsync());
    }

    [Fact]
    public async Task Create_FailureAfterQuickCreate_RollsBackCreatedContact()
    {
        // طرف‌حساب جدید ساخته می‌شود، سپس شناسه‌ی کالای ناموجود خطا می‌دهد
        var request = NewRequest(InvoiceType.Sale, new InvoiceItemRequest { ProductId = 999_999, Quantity = 1, UnitPrice = 100 });
        request.ContactId = null;
        request.NewContactName = "مشتری که نباید بماند";

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(request));

        await using var verify = _db.NewContext();
        Assert.Equal(0, await verify.Contacts.CountAsync());
        Assert.Equal(0, await verify.Invoices.CountAsync());
    }

    [Fact]
    public async Task Create_SecondInvoiceOfSameType_GetsNextNumber_AndTypesAreNumberedSeparately()
    {
        var contact = await AddContactAsync();

        var sale1 = await CreateAsync(NewRequest(InvoiceType.Sale, contact, Item("الف", 1, 100)));
        var sale2 = await CreateAsync(NewRequest(InvoiceType.Sale, contact, Item("ب", 1, 100)));
        var purchase1 = await CreateAsync(NewRequest(InvoiceType.Purchase, contact, Item("ج", 1, 100)));

        Assert.Equal(1, sale1.Number);
        Assert.Equal(2, sale2.Number);
        Assert.Equal(1, purchase1.Number);
    }

    // ---------------------------------------------------------------
    // ویرایش
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_Draft_ReplacesItemsAndRecalculates()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(InvoiceType.Sale, contact, Item("کالا", 1, 1_000)));
        var productId = (await LoadInvoiceAsync(created.Id)).Items.Single().ProductId;

        var edit = NewRequest(InvoiceType.Sale, contact,
            new InvoiceItemRequest { ProductId = productId, Quantity = 3, UnitPrice = 2_000, TaxRate = 10 });
        edit.Id = created.Id;
        edit.Date = new DateOnly(2026, 4, 1);

        await using (var context = _db.NewContext())
            await NewService(context).UpdateAsync(edit, TenantA);

        var loaded = await LoadInvoiceAsync(created.Id);
        Assert.Equal(new DateOnly(2026, 4, 1), loaded.Date);
        Assert.Single(loaded.Items);
        Assert.Equal(6_600, loaded.TotalAmount); // 6000 + 600 مالیات

        // قلم قدیمی حذف نرم شده، نه واقعی
        await using var verify = _db.NewContext();
        Assert.Equal(2, await verify.InvoiceItems.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Update_ChangingInvoiceType_IsRejected()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(InvoiceType.Sale, contact, Item("کالا", 1, 100)));

        var edit = NewRequest(InvoiceType.Purchase, contact, Item("کالا", 1, 100));
        edit.Id = created.Id;

        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).UpdateAsync(edit, TenantA));
        Assert.Contains("نوع فاکتور", ex.Message);
    }

    [Fact]
    public async Task Update_PostedInvoice_IsRejected()
    {
        var created = await CreatePostedSaleAsync(1_000);

        var edit = NewRequest(InvoiceType.Sale, created.ContactId!.Value, Item("کالا", 1, 5_000));
        edit.Id = created.Id;

        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).UpdateAsync(edit, TenantA));
     Assert.Contains("قطعی", ex.Message);
    }

    // ---------------------------------------------------------------
    // قطعی / برگشت
    // ---------------------------------------------------------------

    [Fact]
    public async Task Post_Sale_CreatesPostedDocument()
    {
        var created = await CreatePostedSaleAsync(1_000);

        await using var verify = _db.NewContext();
        var invoice = await verify.Invoices.SingleAsync();
        var document = await verify.Transactions.Include(t => t.Lines).SingleAsync();

        Assert.Equal(InvoiceStatus.Posted, invoice.Status);
        Assert.Equal(document.Id, invoice.TransactionId);
        Assert.True(document.IsPosted);
        Assert.Equal("Invoice", document.ReferenceType);
        Assert.Equal(invoice.Id, document.ReferenceId);
        Assert.Equal(invoice.Date, document.Date);
        Assert.Equal(1_000, document.TotalAmount);

        // فروش: دریافتنی (با طرف حساب) بدهکار، درآمد بستانکار
        var debit = document.Lines.Single(l => l.Debit > 0);
        Assert.Equal(created.ContactId, debit.ContactId);
        Assert.Equal(1_000, document.Lines.Single(l => l.Credit > 0).Credit);
    }

    [Fact]
    public async Task Post_PreInvoice_IsRejected()
    {
        var contact = await AddContactAsync();
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact, Item("کالا", 1, 100)));

        await using var context = _db.NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).PostAsync(pre.Id, TenantA));
    }

    [Fact]
    public async Task Unpost_DeletesDocument_AndReturnsToDraft()
    {
        var created = await CreatePostedSaleAsync(1_000);

        await using (var context = _db.NewContext())
            await NewService(context).UnpostAsync(created.Id, TenantA);

        await using var verify = _db.NewContext();
        var invoice = await verify.Invoices.SingleAsync();

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Null(invoice.TransactionId);
        Assert.Empty(await verify.Transactions.ToListAsync());
        Assert.True((await verify.SetIncludingDeleted<Transaction>().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Unpost_InvoiceWithPayment_IsRejected()
    {
        var created = await CreatePostedSaleAsync(1_000);

        await using (var context = _db.NewContext())
        {
            var invoice = await context.Invoices.SingleAsync();
            invoice.PaidAmount = 300;
            await context.SaveChangesAsync();
        }

        await using var service = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(service).UnpostAsync(created.Id, TenantA));

        Assert.Contains("پرداخت", ex.Message);
    }

    [Fact]
    public async Task Delete_PostedInvoice_IsRejected_ButDraftIsSoftDeleted()
    {
        var posted = await CreatePostedSaleAsync(1_000);
        var contact = await AddContactAsync("مشتری دوم");
        var draft = await CreateAsync(NewRequest(InvoiceType.Sale, contact, Item("کالای دیگر", 1, 100)));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).DeleteAsync(posted.Id, TenantA));

        _db.User.UserId = "deleter";
        await using (var context = _db.NewContext())
            await NewService(context).DeleteAsync(draft.Id, TenantA);

        await using var verify = _db.NewContext();
        Assert.Single(await verify.Invoices.ToListAsync());

        var deleted = await verify.SetIncludingDeleted<Invoice>().SingleAsync(i => i.Id == draft.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal("deleter", deleted.DeletedBy);
    }

    // ---------------------------------------------------------------
    // تسویه با تخفیف
    // ---------------------------------------------------------------

    [Fact]
    public async Task Settle_KeepsMainTransactionId_AndCreatesSeparateDocument()
    {
        var created = await CreatePostedSaleAsync(1_000);
        int mainDocumentId;

        await using (var context = _db.NewContext())
            mainDocumentId = (await context.Invoices.SingleAsync()).TransactionId!.Value;

        await using (var context = _db.NewContext())
            await NewService(context).SettleWithDiscountAsync(created.Id, TenantA, 200);

        await using var verify = _db.NewContext();
        var invoice = await verify.Invoices.SingleAsync();

        // باگ قبلی: TransactionId با سند تخفیف بازنویسی می‌شد
        Assert.Equal(mainDocumentId, invoice.TransactionId);
        Assert.Equal(200, invoice.SettlementDiscount);
        Assert.Equal(800, invoice.RemainingAmount);
        Assert.Equal(2, await verify.Transactions.CountAsync());
    }

    [Fact]
    public async Task Settle_RejectsInvalidAmounts()
    {
        var created = await CreatePostedSaleAsync(1_000);

        foreach (var amount in new[] { 0m, -5m, 1_001m, 100.5m })
        {
            await using var context = _db.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).SettleWithDiscountAsync(created.Id, TenantA, amount));
        }

        await using var verify = _db.NewContext();
        Assert.Equal(0, (await verify.Invoices.SingleAsync()).SettlementDiscount);
        Assert.Equal(1, await verify.Transactions.CountAsync());
    }

    [Fact]
    public async Task Unpost_AfterSettlement_IsRejected()
    {
        var created = await CreatePostedSaleAsync(1_000);

        await using (var context = _db.NewContext())
            await NewService(context).SettleWithDiscountAsync(created.Id, TenantA, 100);

        await using var service = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(service).UnpostAsync(created.Id, TenantA));

        Assert.Contains("تسویه", ex.Message);
    }

    // ---------------------------------------------------------------
    // پیش‌فاکتور: گردش کار و تبدیل
    // ---------------------------------------------------------------

    [Fact]
    public async Task Convert_ApprovedPreInvoice_CopiesAllAmountsIncludingItemDiscountAndTax()
    {
        var pre = await CreateApprovedPreInvoiceAsync();

        Invoice sale;
        await using (var context = _db.NewContext())
            sale = await NewService(context).ConvertToInvoiceAsync(pre.Id, TenantA);

        var loadedPre = await LoadInvoiceAsync(pre.Id);
        var loadedSale = await LoadInvoiceAsync(sale.Id);

        Assert.Equal(InvoiceStatus.Converted, loadedPre.Status);
        Assert.Equal(InvoiceType.Sale, loadedSale.InvoiceType);
        Assert.Equal(InvoiceStatus.Draft, loadedSale.Status);
        Assert.Equal(pre.Id, loadedSale.ReferenceInvoiceId);
        Assert.Equal(1, loadedSale.Number);
        Assert.Equal(new DateOnly(2026, 3, 21), loadedSale.Date); // امروزِ ایران، نه تاریخ UTC

        Assert.Equal(loadedPre.TotalAmount, loadedSale.TotalAmount);
        Assert.Equal(loadedPre.DiscountAmount, loadedSale.DiscountAmount);
        Assert.Equal(loadedPre.TaxAmount, loadedSale.TaxAmount);

        // باگ قبلی: DiscountAmount و TaxAmount هر قلم کپی نمی‌شد
        var preItem = loadedPre.Items.Single();
        var saleItem = loadedSale.Items.Single();
        Assert.True(preItem.DiscountAmount > 0 && preItem.TaxAmount > 0);
        Assert.Equal(preItem.DiscountAmount, saleItem.DiscountAmount);
        Assert.Equal(preItem.TaxAmount, saleItem.TaxAmount);
        Assert.Equal(preItem.LineTotal, saleItem.LineTotal);

        await using var verify = _db.NewContext();
        var histories = await verify.InvoiceHistories.ToListAsync();
        Assert.Contains(histories, h => h.InvoiceId == sale.Id);   // تاریخچه‌ی ایجاد فاکتور تبدیل‌شده
    }

    [Fact]
    public async Task Convert_NotApprovedPreInvoice_IsRejected()
    {
        var contact = await AddContactAsync();
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact, Item("کالا", 1, 100)));

        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(context).ConvertToInvoiceAsync(pre.Id, TenantA));

        Assert.Contains("تایید", ex.Message);
    }

    [Fact]
    public async Task Delete_ConvertedSaleDraft_RevertsPreInvoiceToApproved()
    {
        var pre = await CreateApprovedPreInvoiceAsync();

        Invoice sale;
        await using (var context = _db.NewContext())
            sale = await NewService(context).ConvertToInvoiceAsync(pre.Id, TenantA);

        await using (var context = _db.NewContext())
            await NewService(context).DeleteAsync(sale.Id, TenantA);

        Assert.Equal(InvoiceStatus.Approved, (await LoadInvoiceAsync(pre.Id)).Status);
    }

    [Fact]
    public async Task Postpone_WritesPersianDateInHistory_AndStoresDateOnly()
    {
        var contact = await AddContactAsync();
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact, Item("کالا", 1, 100)));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsSentAsync(pre.Id, TenantA);

        // 2026-04-05 = 1405/01/16
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsPostponedAsync(pre.Id, TenantA, new DateOnly(2026, 4, 5), "بعد از تعطیلات");

        var loaded = await LoadInvoiceAsync(pre.Id);
        Assert.Equal(InvoiceStatus.Postponed, loaded.Status);
        Assert.Equal(new DateOnly(2026, 4, 5), loaded.NextFollowUpDate);

        await using var verify = _db.NewContext();
        var last = await verify.InvoiceHistories
            .Where(h => h.ToStatus == InvoiceStatus.Postponed)
            .SingleAsync();

        Assert.Contains("1405/01/16", last.Description);
    }

    [Fact]
    public async Task Cancel_Twice_IsRejected_AndWorkflowRulesAreEnforced()
    {
        var contact = await AddContactAsync();
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact, Item("کالا", 1, 100)));

        // تایید پیش‌نویس مجاز نیست (باید اول ارسال شود)
        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).MarkAsApprovedAsync(pre.Id, TenantA, null));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsCancelledAsync(pre.Id, TenantA, "انصراف");

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).MarkAsCancelledAsync(pre.Id, TenantA, null));
    }

    // ---------------------------------------------------------------
    // Arrange / Helpers
    // ---------------------------------------------------------------

    private InvoiceService NewService(ApplicationDbContext context) =>
        new(new StockMovementService(context, new WarehouseService(context)), context, new TransactionService(context), new SystemAccountService(context), _db.User, _time); 
    private async Task<Invoice> CreateAsync(InvoiceRequest request)
    {
        await using var context = _db.NewContext();
        return await NewService(context).CreateAsync(request, TenantA);
    }

    private async Task<Invoice> CreatePostedSaleAsync(decimal amount)
    {
        var contact = await AddContactAsync();

        var created = await CreateAsync(
            NewRequest(
                InvoiceType.Sale,
                contact,
                Item("کالای فروش", 1, amount)));

        await using var context = _db.NewContext();

        var warehouse = new Warehouse
        {
            TenantId = TenantA,
            Code = "TEST-WH",
            Name = "انبار تست",
            IsActive = true,
            IsDefault = true
        };

        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();

        var stockMovementService = new StockMovementService(
            context,
            new WarehouseService(context));

        // ثبت ورود یک واحد موجودی قبل از فروش
        var receiptId = await stockMovementService.CreateAsync(
            new StockMovementRequest
            {
                Date = created.Date,
                MovementType = (int)StockMovementType.Receipt,
                DestinationWarehouseId = warehouse.Id,
                Description = "ورود موجودی اولیه برای تست",
                Items =
                [
                    new StockMovementItemRequest
                {
                    ProductId = created.Items.Single().ProductId,
                    Quantity = 1,
                    UnitCost = amount
                }
                ]
            },
            TenantA);

        await stockMovementService.PostAsync(receiptId, TenantA);

        // اکنون موجودی کافی برای قطعی‌کردن فاکتور فروش وجود دارد
        await NewService(context).PostAsync(created.Id, TenantA);

        return await LoadInvoiceAsync(created.Id);
    }

    private async Task<Invoice> CreateApprovedPreInvoiceAsync()
    {
        var contact = await AddContactAsync();
        var pre = await CreateAsync(NewRequest(InvoiceType.PreInvoice, contact,
            Item("کالای پیش‌فاکتور", qty: 3, price: 1_234, discountPercent: 7.5m, tax: 9)));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsSentAsync(pre.Id, TenantA);

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsApprovedAsync(pre.Id, TenantA, "تایید شد");

        return pre;
    }

    private async Task<int> AddContactAsync(string name = "مشتری الف")
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = name, ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private async Task<Invoice> LoadInvoiceAsync(int id)
    {
        await using var context = _db.NewContext();
        return await context.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == id);
    }

    private static InvoiceRequest NewRequest(InvoiceType type, params InvoiceItemRequest[] items)
        => new()
        {
            Date = new DateOnly(2026, 3, 22),
            InvoiceType = type,
            ContactId = 1,
            Items = items.ToList()
        };

    private static InvoiceRequest NewRequest(InvoiceType type, int contactId, params InvoiceItemRequest[] items)
    {
        var request = NewRequest(type, items);
        request.ContactId = contactId;
        return request;
    }

    private static InvoiceItemRequest Item(string newProductName, decimal qty, decimal price,
        decimal discountPercent = 0, decimal tax = 0)
        => new()
        {
            NewProductName = newProductName,
            Quantity = qty,
            UnitPrice = price,
            DiscountPercent = discountPercent,
            TaxRate = tax
        };
}
