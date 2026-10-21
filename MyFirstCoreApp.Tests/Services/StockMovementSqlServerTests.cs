using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class StockMovementSqlServerTests
{
    [SqlServerFact]
    public async Task StockMovement_DuplicateReversalMovementId_IsRejectedByDatabase()
    {
        using var db = new SqlServerTestDatabase();

        const int tenantId = 1;

        var (productId, warehouseId) =
            await db.CreateInventoryDataAsync(tenantId);

        await using var context = db.NewContext();

        var original = new StockMovement
        {
            TenantId = tenantId,
            Number = "SM-ORIGINAL-001",
            Date = new DateOnly(2026, 3, 21),
            MovementType = StockMovementType.Receipt,
            Status = StockMovementStatus.Draft,
            DestinationWarehouseId = warehouseId,
            Items =
            {
                new StockMovementItem
                {
                    TenantId = tenantId,
                    ProductId = productId,
                    Quantity = 10,
                    UnitCost = 100
                }
            }
        };

        context.StockMovements.Add(original);
        await context.SaveChangesAsync();

        var firstReversal = new StockMovement
        {
            TenantId = tenantId,
            Number = "SM-REVERSAL-001",
            Date = new DateOnly(2026, 3, 21),
            MovementType = StockMovementType.Issue,
            Status = StockMovementStatus.Draft,
            SourceWarehouseId = warehouseId,
            ReversesMovementId = original.Id,
            Items =
            {
                new StockMovementItem
                {
                    TenantId = tenantId,
                    ProductId = productId,
                    Quantity = 1,
                    UnitCost = 100
                }
            }
        };

        context.StockMovements.Add(firstReversal);
        await context.SaveChangesAsync();

        var duplicateReversal = new StockMovement
        {
            TenantId = tenantId,
            Number = "SM-REVERSAL-002",
            Date = new DateOnly(2026, 3, 21),
            MovementType = StockMovementType.Issue,
            Status = StockMovementStatus.Draft,
            SourceWarehouseId = warehouseId,
            ReversesMovementId = original.Id,
            Items =
            {
                new StockMovementItem
                {
                    TenantId = tenantId,
                    ProductId = productId,
                    Quantity = 1,
                    UnitCost = 100
                }
            }
        };

        context.StockMovements.Add(duplicateReversal);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        context.ChangeTracker.Clear();

        var reversalCount = await context.StockMovements
            .IgnoreQueryFilters()
            .CountAsync(x =>
                x.TenantId == tenantId &&
                x.ReversesMovementId == original.Id);

        Assert.Equal(1, reversalCount);
    }

    [SqlServerFact]
    public async Task StockMovement_MultipleNullReversalIds_AreAllowed()
    {
        using var db = new SqlServerTestDatabase();

        const int tenantId = 1;

        var (_, warehouseId) =
            await db.CreateInventoryDataAsync(tenantId);

        await using var context = db.NewContext();

        var movements = new[]
        {
            new StockMovement
            {
                TenantId = tenantId,
                Number = "SM-NULL-001",
                Date = new DateOnly(2026, 3, 21),
                MovementType = StockMovementType.Receipt,
                Status = StockMovementStatus.Draft,
                DestinationWarehouseId = warehouseId
            },
            new StockMovement
            {
                TenantId = tenantId,
                Number = "SM-NULL-002",
                Date = new DateOnly(2026, 3, 21),
                MovementType = StockMovementType.Receipt,
                Status = StockMovementStatus.Draft,
                DestinationWarehouseId = warehouseId
            }
        };

        context.StockMovements.AddRange(movements);

        await context.SaveChangesAsync();

        Assert.All(movements, movement =>
            Assert.Null(movement.ReversesMovementId));

        var count = await context.StockMovements
            .IgnoreQueryFilters()
            .CountAsync(x =>
                x.TenantId == tenantId &&
                x.Number.StartsWith("SM-NULL-"));

        Assert.Equal(2, count);
    }
    [SqlServerFact]
    public async Task ReverseInvoice_ConcurrentCalls_CreateOnlyOneReversal()
    {
        using var db = new SqlServerTestDatabase();

        const int tenantId = 1;
        const decimal initialQuantity = 10m;
        const decimal saleQuantity = 3m;

        var (productId, warehouseId) =
            await db.CreateInventoryDataAsync(tenantId);

        db.Tenant.TenantId = tenantId;

        // 1. ایجاد موجودی اولیه
        await using (var context = db.NewContext())
        {
            var receipt = new StockMovement
            {
                TenantId = tenantId,
                Number = "SM-CONCURRENT-RECEIPT",
                Date = new DateOnly(2026, 3, 21),
                MovementType = StockMovementType.Receipt,
                Status = StockMovementStatus.Posted,
                DestinationWarehouseId = warehouseId,
                Items =
            {
                new StockMovementItem
                {
                    TenantId = tenantId,
                    ProductId = productId,
                    Quantity = initialQuantity,
                    UnitCost = 100m
                }
            }
            };

            context.StockMovements.Add(receipt);
            await context.SaveChangesAsync();
        }

        // 2. ساخت طرف حساب و فاکتور فروش
        int invoiceId;

        await using (var context = db.NewContext())
        {
            var contact = new Contact
            {
                Name = "طرف حساب تست هم‌زمانی",
                ContactType = ContactType.Both,
                IsActive = true
            };

            context.Contacts.Add(contact);
            await context.SaveChangesAsync();

            var invoice = new Invoice
            {
                TenantId = tenantId,
                Number = 900001,
                Date = new DateOnly(2026, 3, 21),
                InvoiceType = InvoiceType.Sale,
                Status = InvoiceStatus.Draft,
                ContactId = contact.Id,
                Items =
                [
                    new InvoiceItem
                {
                    ProductId = productId,
                    Quantity = saleQuantity,
                    UnitPrice = 100m,
                    DiscountPercent = 0,
                    DiscountAmount = 0,
                    TaxRate = 0,
                    TaxAmount = 0,
                    LineTotal = saleQuantity * 100m
                }
                ]
            };

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            invoiceId = invoice.Id;
        }

        // 3. ثبت گردش خروج ناشی از فاکتور
        await using (var context = db.NewContext())
        {
            var service = new StockMovementService(
                context,
                new WarehouseService(context));

            await service.CreateFromInvoiceAsync(invoiceId, tenantId);
        }

        // 4. اجرای هم‌زمان دو درخواست معکوس‌سازی
        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Exception?> TryReverseAsync()
        {
            await startGate.Task;

            try
            {
                await using var context = db.NewContext();

                var service = new StockMovementService(
                    context,
                    new WarehouseService(context));

                await service.ReverseInvoiceAsync(invoiceId, tenantId);

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var firstTask = Task.Run(TryReverseAsync);
        var secondTask = Task.Run(TryReverseAsync);

        startGate.SetResult();

        var results = await Task.WhenAll(firstTask, secondTask);

        // دقیقاً یکی از درخواست‌ها باید موفق شود.
        Assert.Single(results.Where(x => x is null));

        // 5. بررسی نتیجه در دیتابیس
        await using var verify = db.NewContext();

        var originalMovements = await verify.StockMovements
            .Where(x =>
                x.TenantId == tenantId &&
                x.ReferenceInvoiceId == invoiceId &&
                x.ReversesMovementId == null &&
                x.Status == StockMovementStatus.Posted)
            .ToListAsync();

        Assert.Single(originalMovements);

        var original = originalMovements[0];

        var reversals = await verify.StockMovements
            .Where(x =>
                x.TenantId == tenantId &&
                x.ReversesMovementId == original.Id &&
                x.Status == StockMovementStatus.Posted)
            .ToListAsync();

        Assert.Single(reversals);
        Assert.Equal(
            StockMovementType.Receipt,
            reversals[0].MovementType);

        // 6. بررسی برگشت موجودی به مقدار اولیه
        var kardex = await new StockMovementService(
            verify,
            new WarehouseService(verify))
            .GetKardexAsync(
                productId,
                warehouseId,
                null,
                null,
                tenantId);

        Assert.Equal(initialQuantity, kardex.Last().Balance);
    }
}
