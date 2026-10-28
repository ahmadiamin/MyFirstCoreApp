using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class StockMovementServiceTests : IDisposable
{
    private const int TenantA = 1;

    private static readonly DateOnly TestDate = new(2026, 3, 21);

    private readonly TestDatabase _db = new();

    public void Dispose()
    {
        _db.Dispose();
    }

    private StockMovementService NewService(
        ApplicationDbContext context)
    {
        return new StockMovementService(
            context,
            new WarehouseService(context));
    }

    private async Task<(int ProductId, int WarehouseId)>
        CreateInventoryDataAsync()
    {
        _db.Tenant.TenantId = TenantA;
        var previousTenant = _db.Tenant.TenantId;

        try
        {
            await using var context = _db.NewContext();

            var unit = new Unit
            {
                TenantId = TenantA,
                Name = "عدد",
                Symbol = "عدد",
                IsActive = true
            };

            context.Units.Add(unit);
            await context.SaveChangesAsync();

            var product = new Product
            {
                TenantId = TenantA,
                Name = "کالای تست",
                Code = "TEST-001",
                UnitId = unit.Id,
                IsActive = true
            };

            var warehouse = new Warehouse
            {
                TenantId = TenantA,
                Code = "TEST-WH",
                Name = "انبار تست",
                IsActive = true,
                IsDefault = true
            };

            context.Products.Add(product);
            context.Warehouses.Add(warehouse);

            await context.SaveChangesAsync();

            return (product.Id, warehouse.Id);
        }
        finally
        {
            _db.Tenant.TenantId = previousTenant;
        }
    }

    private async Task<int> CreateAndPostAsync(
        int productId,
        int warehouseId,
        StockMovementType type,
        decimal quantity,
        decimal unitCost = 100m)
    {
        var previousTenant = _db.Tenant.TenantId;
        _db.Tenant.TenantId = TenantA;

        try
        {
            await using var context = _db.NewContext();

            var service = NewService(context);

            var request = new StockMovementRequest
            {
                Date = TestDate,
                MovementType = (int)type,

                SourceWarehouseId =
                    type == StockMovementType.Issue
                        ? warehouseId
                        : null,

                DestinationWarehouseId =
                    type == StockMovementType.Receipt
                    || type == StockMovementType.Opening
                        ? warehouseId
                        : null,

                Description = $"تست {type}",

                Items =
                [
                    new StockMovementItemRequest
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        UnitCost = unitCost
                    }
                ]
            };

            var id = await service.CreateAsync(request, TenantA);
            await service.PostAsync(id, TenantA);

            return id;
        }
        finally
        {
            _db.Tenant.TenantId = previousTenant;
        }
    }

    [Fact]
    public async Task Post_Transfer_DecreasesSourceAndIncreasesDestinationBalance()
    {
        // Arrange
        var (productId, sourceWarehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        // ابتدا 10 واحد وارد انبار مبدأ می‌کنیم.
        await CreateAndPostAsync(
            productId,
            sourceWarehouseId,
            StockMovementType.Receipt,
            10m);

        int destinationWarehouseId;

        _db.Tenant.TenantId = TenantA;

        await using (var context = _db.NewContext())
        {
            var destinationWarehouse = new Warehouse
            {
                TenantId = TenantA,
                Code = "WH-002",
                Name = "انبار مقصد تست",
                IsActive = true,
                IsDefault = false
            };

            context.Warehouses.Add(destinationWarehouse);
            await context.SaveChangesAsync();

            destinationWarehouseId = destinationWarehouse.Id;
        }

        var service = NewService(_db.NewContext());

        var request = new StockMovementRequest
        {
            Date = TestDate.AddDays(1),
            MovementType = (int)StockMovementType.Transfer,
            SourceWarehouseId = sourceWarehouseId,
            DestinationWarehouseId = destinationWarehouseId,
            Description = "انتقال آزمایشی",
            Items =
            [
                new StockMovementItemRequest
            {
                ProductId = productId,
                Quantity = 4m
            }
            ]
        };

        // Act
        var movement = await service.CreateAsync(request, TenantA);
        await service.PostAsync(movement, TenantA);

        var sourceKardex = await service.GetKardexAsync(
            productId,
            sourceWarehouseId,
            null,
            null,
            TenantA);

        var destinationKardex = await service.GetKardexAsync(
            productId,
            destinationWarehouseId,
            null,
            null,
            TenantA);

        await using var reportContext = _db.NewContext();
        var stockBalanceService = new StockBalanceService(reportContext);
        var allBalances = await stockBalanceService.GetAllBalancesAsync(TenantA);
        var sourceBalance = Assert.Single(allBalances, x =>
            x.ProductId == productId && x.WarehouseId == sourceWarehouseId);
        var destinationBalance = Assert.Single(allBalances, x =>
            x.ProductId == productId && x.WarehouseId == destinationWarehouseId);
        var filteredBalances = await stockBalanceService.GetAllBalancesAsync(
            TenantA, destinationWarehouseId);
        var dormantBalances = await stockBalanceService.GetDormantBalancesAsync(
            TenantA, TestDate.AddDays(2), TestDate.AddDays(4));

        // Assert
        Assert.Equal(6m, sourceKardex.Last().Balance);
        Assert.Equal(4m, destinationKardex.Last().Balance);
        Assert.Equal(6m, sourceBalance.Quantity);
        Assert.Equal(4m, destinationBalance.Quantity);
        Assert.Single(filteredBalances);
        Assert.Equal(destinationWarehouseId, filteredBalances[0].WarehouseId);
        Assert.Equal(2, dormantBalances.Count);
        Assert.Contains(dormantBalances, x =>
            x.ProductId == productId && x.WarehouseId == sourceWarehouseId && x.Quantity == 6m);
        Assert.Contains(dormantBalances, x =>
            x.ProductId == productId && x.WarehouseId == destinationWarehouseId && x.Quantity == 4m);
    }
    [Fact]
    public async Task GetKardex_DoesNotExposeAnotherTenantMovements()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10m);

        var previousTenantId = _db.Tenant.TenantId;

        try
        {
            // Act: تغییر شرکت جاری به شرکت دوم
            _db.Tenant.TenantId = 2;

            await using var context = _db.NewContext();
            var service = NewService(context);

            var kardex = await service.GetKardexAsync(
                productId,
                warehouseId,
                null,
                null,
                2);

            // Assert
            Assert.Empty(kardex);
        }
        finally
        {
            _db.Tenant.TenantId = previousTenantId;
        }
    }
    [Fact]
    public async Task Post_Receipt_IncreasesKardexBalance()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;
        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10);

        await using var context = _db.NewContext();

        var result = await NewService(context).GetKardexAsync(
            productId,
            warehouseId,
            null,
            null,
            TenantA);

        var row = Assert.Single(result);

        Assert.Equal(10m, row.Incoming);
        Assert.Equal(0m, row.Outgoing);
        Assert.Equal(10m, row.Balance);
    }

    [Fact]
    public async Task Post_Issue_DecreasesKardexBalance()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10);

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Issue,
            3);

        await using var context = _db.NewContext();

        var result = await NewService(context).GetKardexAsync(
            productId,
            warehouseId,
            null,
            null,
            TenantA);

        Assert.Equal(2, result.Count);

        Assert.Equal(10m, result[0].Balance);
        Assert.Equal(3m, result[1].Outgoing);
        Assert.Equal(7m, result[1].Balance);
    }

    [Fact]
    public async Task Post_IssueGreaterThanStock_ThrowsException()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            5);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await CreateAndPostAsync(
                    productId,
                    warehouseId,
                    StockMovementType.Issue,
                    6);
            });

        Assert.Contains("موجودی", exception.Message);

        await using var context = _db.NewContext();

        var result = await NewService(context).GetKardexAsync(
            productId,
            warehouseId,
            null,
            null,
            TenantA);

        var row = Assert.Single(result);

        Assert.Equal(5m, row.Balance);
    }

    [Fact]
    public async Task GetKardex_WithDateRange_PreservesOpeningBalance()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10);

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);

            var request = new StockMovementRequest
            {
                Date = TestDate.AddDays(2),
                MovementType = (int)StockMovementType.Issue,
                SourceWarehouseId = warehouseId,
                Description = "حواله در بازه گزارش",
                Items =
                [
                    new StockMovementItemRequest
                    {
                        ProductId = productId,
                        Quantity = 3
                    }
                ]
            };

            var id = await service.CreateAsync(request, TenantA);
            await service.PostAsync(id, TenantA);
        }

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);

            var request = new StockMovementRequest
            {
                Date = TestDate.AddDays(4),
                MovementType = (int)StockMovementType.Receipt,
                DestinationWarehouseId = warehouseId,
                Description = "رسید بعد از بازه",
                Items =
                [
                    new StockMovementItemRequest
                    {
                        ProductId = productId,
                        Quantity = 5
                    }
                ]
            };

            var id = await service.CreateAsync(request, TenantA);
            await service.PostAsync(id, TenantA);
        }

        await using var reportContext = _db.NewContext();

        var result = await NewService(reportContext).GetKardexAsync(
            productId,
            warehouseId,
            TestDate.AddDays(1),
            TestDate.AddDays(3),
            TenantA);

        var row = Assert.Single(result);

        Assert.Equal(3m, row.Outgoing);
        Assert.Equal(7m, row.Balance);
    }

    [Fact]
    public async Task Cancel_DraftMovement_DoesNotAffectKardex()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        var previousTenant = _db.Tenant.TenantId;
        _db.Tenant.TenantId = TenantA;

        try
        {
            int movementId;

            await using (var context = _db.NewContext())
            {
                var service = NewService(context);

                movementId = await service.CreateAsync(
                    new StockMovementRequest
                    {
                        Date = TestDate,
                        MovementType = (int)StockMovementType.Receipt,
                        DestinationWarehouseId = warehouseId,
                        Items =
                        [
                            new StockMovementItemRequest
                            {
                                ProductId = productId,
                                Quantity = 10
                            }
                        ]
                    },
                    TenantA);

                await service.CancelAsync(movementId, TenantA);
            }

            await using var reportContext = _db.NewContext();

            var result = await NewService(reportContext).GetKardexAsync(
                productId,
                warehouseId,
                null,
                null,
                TenantA);

            Assert.Empty(result);
        }
        finally
        {
            _db.Tenant.TenantId = previousTenant;
        }
    }
    [Fact]
    public async Task Post_AlreadyPostedMovement_ThrowsException()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        var movement = await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10m);

        // Act
        await using var context = _db.NewContext();
        var service = NewService(context);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PostAsync(movement, TenantA));
    }

    [Fact]
    public async Task Cancel_PostedMovement_ThrowsException()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        var movement = await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10m);

        // Act
        await using var context = _db.NewContext();
        var service = NewService(context);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CancelAsync(movement, TenantA));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_InvalidQuantity_ThrowsException(int quantity)
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        await using var context = _db.NewContext();
        var service = NewService(context);

        var request = new StockMovementRequest
        {
            Date = TestDate,
            MovementType = (int)StockMovementType.Receipt,
            DestinationWarehouseId = warehouseId,
            Items =
            [
                new StockMovementItemRequest
            {
                ProductId = productId,
                Quantity = (decimal)quantity
            }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, TenantA));
    }
    [Fact]
    public async Task Create_InactiveProduct_ThrowsException()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        await using (var context = _db.NewContext())
        {
            var product = await context.Products
                .SingleAsync(x => x.Id == productId);

            product.IsActive = false;
            await context.SaveChangesAsync();
        }

        await using var testContext = _db.NewContext();
        var service = NewService(testContext);

        var request = new StockMovementRequest
        {
            Date = TestDate,
            MovementType = (int)StockMovementType.Receipt,
            DestinationWarehouseId = warehouseId,
            Items =
            [
                new StockMovementItemRequest
            {
                ProductId = productId,
                Quantity = 1m
            }
            ]
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, TenantA));
    }
    [Fact]
    public async Task Create_ProductFromAnotherTenant_ThrowsException()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await using var context = _db.NewContext();
                var service = NewService(context);

                var request = new StockMovementRequest
                {
                    Date = TestDate,
                    MovementType = (int)StockMovementType.Receipt,
                    DestinationWarehouseId = warehouseId,
                    Items =
                    [
                        new StockMovementItemRequest
                    {
                        ProductId = productId,
                        Quantity = 1m
                    }
                    ]
                };

                await service.CreateAsync(request, 2);
            });
    }
    [Fact]
    public async Task Create_InactiveWarehouse_ThrowsException()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        await using (var context = _db.NewContext())
        {
            var warehouse = await context.Warehouses
                .SingleAsync(x => x.Id == warehouseId);

            warehouse.IsActive = false;
            await context.SaveChangesAsync();
        }

        await using var testContext = _db.NewContext();
        var service = NewService(testContext);

        var request = new StockMovementRequest
        {
            Date = TestDate,
            MovementType = (int)StockMovementType.Receipt,
            DestinationWarehouseId = warehouseId,
            Items =
            [
                new StockMovementItemRequest
            {
                ProductId = productId,
                Quantity = 1m
            }
            ]
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(request, TenantA));
    }
    [Fact]
    public async Task Create_WarehouseFromAnotherTenant_ThrowsException()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        int tenant2WarehouseId;

        _db.Tenant.TenantId = 2;

        await using (var context = _db.NewContext())
        {
            var warehouse = new Warehouse
            {
                TenantId = 2,
                Code = "WH-T2",
                Name = "انبار شرکت دوم",
                IsActive = true,
                IsDefault = false
            };

            context.Warehouses.Add(warehouse);
            await context.SaveChangesAsync();

            tenant2WarehouseId = warehouse.Id;
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await using var context = _db.NewContext();
                var service = NewService(context);

                var request = new StockMovementRequest
                {
                    Date = TestDate,
                    MovementType = (int)StockMovementType.Receipt,
                    DestinationWarehouseId = tenant2WarehouseId,
                    Items =
                    [
                        new StockMovementItemRequest
                    {
                        ProductId = productId,
                        Quantity = 1m
                    }
                    ]
                };

                await service.CreateAsync(request, TenantA);
            });
    }
    [Fact]
    public async Task GetKardex_NoMovementsInDateRange_ReturnsEmptyList()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        var service = NewService(_db.NewContext());

        // Act
        var kardex = await service.GetKardexAsync(
            productId,
            warehouseId,
            TestDate.AddDays(1),
            TestDate.AddDays(5),
            TenantA);

        // Assert
        Assert.Empty(kardex);
    }
    [Fact]
    public async Task GetKardex_DateRange_IncludesFromAndToDates()
    {
        // Arrange
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        async Task CreateReceiptAsync(DateOnly date, decimal quantity)
        {
            await using var context = _db.NewContext();
            var service = NewService(context);

            var id = await service.CreateAsync(
                new StockMovementRequest
                {
                    Date = date,
                    MovementType = (int)StockMovementType.Receipt,
                    DestinationWarehouseId = warehouseId,
                    Description = "تست بازه تاریخ",
                    Items =
                    [
                        new StockMovementItemRequest
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        UnitCost = 100m
                    }
                    ]
                },
                TenantA);

            await service.PostAsync(id, TenantA);
        }

        // موجودی قبل از شروع بازه
        await CreateReceiptAsync(TestDate.AddDays(-1), 5m);

        // دقیقاً در تاریخ شروع
        await CreateReceiptAsync(TestDate, 2m);

        // دقیقاً در تاریخ پایان
        await CreateReceiptAsync(TestDate.AddDays(1), 3m);

        // بعد از پایان بازه؛ نباید در ردیف‌های گزارش باشد
        await CreateReceiptAsync(TestDate.AddDays(2), 7m);

        // Act
        await using var reportContext = _db.NewContext();

        var result = await NewService(reportContext).GetKardexAsync(
            productId,
            warehouseId,
            TestDate,
            TestDate.AddDays(1),
            TenantA);

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal(TestDate, result[0].Date);
        Assert.Equal(2m, result[0].Incoming);
        Assert.Equal(7m, result[0].Balance);

        Assert.Equal(TestDate.AddDays(1), result[1].Date);
        Assert.Equal(3m, result[1].Incoming);
        Assert.Equal(10m, result[1].Balance);
    }
    private async Task<int> CreateInvoiceAsync(int productId, InvoiceType invoiceType, decimal quantity, decimal unitPrice = 100m, int? warehouseId = null)
    {
        var previousTenant = _db.Tenant.TenantId;
        _db.Tenant.TenantId = TenantA;

        try
        {
            await using var context = _db.NewContext();

            var contact = new Contact
            {
                Name = "طرف حساب تست",
                ContactType = ContactType.Both,
                IsActive = true
            };

            context.Contacts.Add(contact);
            await context.SaveChangesAsync();

            var invoice = new Invoice
            {
                TenantId = TenantA,
                Number = 1,
                Date = TestDate,
                InvoiceType = invoiceType,
                Status = InvoiceStatus.Draft,
                ContactId = contact.Id,
                Items =
                [
                    new InvoiceItem
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    DiscountPercent = 0,
                    DiscountAmount = 0,
                    TaxRate = 0,
                    TaxAmount = 0,
                    LineTotal = quantity * unitPrice
                }
                ]
            };

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            return invoice.Id;
        }
        finally
        {
            _db.Tenant.TenantId = previousTenant;
        }
    }
    [Fact]
    public async Task CreateFromInvoice_Purchase_IncreasesKardexBalance()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Purchase,
            5m);

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);

            await service.CreateFromInvoiceAsync(
                invoiceId,
                TenantA);
        }

        await using var verify = _db.NewContext();

        var movements = await verify.StockMovements
            .Where(x => x.ReferenceInvoiceId == invoiceId)
            .ToListAsync();

        Assert.Single(movements);
        Assert.Equal(StockMovementType.Receipt, movements[0].MovementType);
        Assert.Equal(StockMovementStatus.Posted, movements[0].Status);

        var kardex = await NewService(verify).GetKardexAsync(
            productId,
            warehouseId,
            null,
            null,
            TenantA);

        Assert.Equal(5m, kardex.Last().Balance);
    }

    [Fact]
    public async Task CreateFromInvoice_Purchase_UsesSelectedWarehouse()
    {
        var (productId, defaultWarehouseId) = await CreateInventoryDataAsync();
        int selectedWarehouseId;

        await using (var context = _db.NewContext())
        {
            var warehouse = new Warehouse
            {
                TenantId = TenantA,
                Code = "WH-SELECTED",
                Name = "انبار انتخابی",
                IsActive = true
            };
            context.Warehouses.Add(warehouse);
            await context.SaveChangesAsync();
            selectedWarehouseId = warehouse.Id;
        }

        var invoiceId = await CreateInvoiceAsync(
            productId, InvoiceType.Purchase, 4m, warehouseId: selectedWarehouseId);

        await using (var context = _db.NewContext())
            await NewService(context).CreateFromInvoiceAsync(invoiceId, TenantA);

        await using var verify = _db.NewContext();
        var movement = await verify.StockMovements
            .SingleAsync(x => x.ReferenceInvoiceId == invoiceId);

        Assert.Equal(selectedWarehouseId, movement.DestinationWarehouseId);
        Assert.NotEqual(defaultWarehouseId, movement.DestinationWarehouseId);
        Assert.Equal(0m, await NewService(verify).GetBalanceAsync(
            productId, defaultWarehouseId, TenantA));
        Assert.Equal(4m, await NewService(verify).GetBalanceAsync(
            productId, selectedWarehouseId, TenantA));
    }
    [Fact]
    public async Task CreateFromInvoice_Twice_ThrowsException()
    {
        var (productId, _) = await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Purchase,
            5m);

        await using (var context = _db.NewContext())
        {
            await NewService(context).CreateFromInvoiceAsync(
                invoiceId,
                TenantA);
        }

        await using (var context = _db.NewContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).CreateFromInvoiceAsync(
                    invoiceId,
                    TenantA));
        }

        await using var verify = _db.NewContext();

        var movements = await verify.StockMovements
            .Where(x => x.ReferenceInvoiceId == invoiceId)
            .ToListAsync();

        Assert.Single(movements);
    }
    [Fact]
    public async Task ReverseInvoice_Sale_RestoresStock()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10m);

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Sale,
            3m);

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);
            await service.CreateFromInvoiceAsync(invoiceId, TenantA);
        }

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);
            await service.ReverseInvoiceAsync(invoiceId, TenantA);
        }

        await using var verify = _db.NewContext();

        var movements = await verify.StockMovements
            .Where(x => x.ReferenceInvoiceId == invoiceId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.Equal(StockMovementType.Issue, movements[0].MovementType);
        Assert.Equal(StockMovementType.Receipt, movements[1].MovementType);
        Assert.Equal(movements[0].Id, movements[1].ReversesMovementId);
        Assert.Equal(StockMovementStatus.Posted, movements[1].Status);

        var kardex = await NewService(verify).GetKardexAsync(
            productId, warehouseId, null, null, TenantA);

        Assert.Equal(10m, kardex.Last().Balance);
    }
    [Fact]
    public async Task ReverseInvoice_Twice_ThrowsException()
    {
        var (productId, warehouseId) =
            await CreateInventoryDataAsync();

        _db.Tenant.TenantId = TenantA;

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Receipt,
            10m);

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Sale,
            3m);

        await using (var context = _db.NewContext())
        {
            await NewService(context).CreateFromInvoiceAsync(
                invoiceId, TenantA);
        }

        await using (var context = _db.NewContext())
        {
            await NewService(context).ReverseInvoiceAsync(
                invoiceId, TenantA);
        }

        await using (var context = _db.NewContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).ReverseInvoiceAsync(
                    invoiceId, TenantA));
        }

        await using var verify = _db.NewContext();

        var movements = await verify.StockMovements
            .Where(x => x.ReferenceInvoiceId == invoiceId)
            .ToListAsync();

        Assert.Equal(2, movements.Count);
    }
    [Fact]
    public async Task ReverseInvoice_Purchase_ReducesStock()
    {
        var (productId, warehouseId) = await CreateInventoryDataAsync();

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Opening,
            10m);

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Purchase,
            quantity: 5m);

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);
            await service.CreateFromInvoiceAsync(invoiceId, TenantA);
            await service.ReverseInvoiceAsync(invoiceId, TenantA);
        }

        await using var verifyContext = _db.NewContext();
        var verifyService = NewService(verifyContext);

        var kardex = await verifyService.GetKardexAsync(
            productId,
            warehouseId,
            null,
            null,
            TenantA);

        Assert.Equal(10m, kardex[^1].Balance);

        var movements = await verifyContext.StockMovements
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == TenantA &&
                        x.ReferenceInvoiceId == invoiceId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.Equal(StockMovementType.Receipt, movements[0].MovementType);
        Assert.Equal(StockMovementType.Issue, movements[1].MovementType);
        Assert.Equal(movements[0].Id, movements[1].ReversesMovementId);
        Assert.Equal(StockMovementStatus.Posted, movements[1].Status);
    }
    [Fact]
    public async Task ReverseInvoice_Purchase_Twice_ThrowsException()
    {
        var (productId, warehouseId) = await CreateInventoryDataAsync();

        await CreateAndPostAsync(
            productId,
            warehouseId,
            StockMovementType.Opening,
            10m);

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Purchase,
            quantity: 5m);

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);
            await service.CreateFromInvoiceAsync(invoiceId, TenantA);
            await service.ReverseInvoiceAsync(invoiceId, TenantA);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ReverseInvoiceAsync(invoiceId, TenantA));
        }

        await using var verifyContext = _db.NewContext();

        var count = await verifyContext.StockMovements
            .IgnoreQueryFilters()
            .CountAsync(x =>
                x.TenantId == TenantA &&
                x.ReferenceInvoiceId == invoiceId);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ReverseInvoice_Purchase_WhenStockIsInsufficient_ThrowsAndRollsBack()
    {
        var previousTenant = _db.Tenant.TenantId;
        _db.Tenant.TenantId = TenantA;

        try
        {
            var (productId, warehouseId) = await CreateInventoryDataAsync();

            // موجودی اولیه: 10
            await CreateAndPostAsync(
                productId,
                warehouseId,
                StockMovementType.Opening,
                10m);

            // فاکتور خرید: ورود 5 عدد، موجودی = 15
            var invoiceId = await CreateInvoiceAsync(
                productId,
                InvoiceType.Purchase,
                quantity: 5m);

            await using (var context = _db.NewContext())
            {
                var service = NewService(context);
                await service.CreateFromInvoiceAsync(invoiceId, TenantA);
            }

            // خروج 14 عدد، موجودی = 1
            await CreateAndPostAsync(
                productId,
                warehouseId,
                StockMovementType.Issue,
                14m);

            // برگشت خرید به خروج 5 عدد نیاز دارد؛ موجودی کافی نیست.
            await using (var context = _db.NewContext())
            {
                var service = NewService(context);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.ReverseInvoiceAsync(invoiceId, TenantA));
            }

            // بررسی اینکه گردش معکوس ناقص ثبت نشده باشد.
            await using (var context = _db.NewContext())
            {
                var movements = await context.StockMovements
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.TenantId == TenantA &&
                        x.ReferenceInvoiceId == invoiceId)
                    .ToListAsync();

                Assert.Single(movements);

                Assert.Null(movements[0].ReversesMovementId);
                Assert.Equal(
                    StockMovementStatus.Posted,
                    movements[0].Status);

                var service = NewService(context);

                var kardex = await service.GetKardexAsync(
                    productId,
                    warehouseId,
                    null,
                    null,
                    TenantA);

                Assert.Equal(1m, kardex[^1].Balance);
            }
        }
        finally
        {
            _db.Tenant.TenantId = previousTenant;
        }
    }

    [Fact]
    public async Task ReverseInvoice_WithoutActiveMovement_ThrowsException()
    {
        var previousTenant = _db.Tenant.TenantId;
        _db.Tenant.TenantId = TenantA;

        try
        {
            await using var context = _db.NewContext();
            var service = NewService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ReverseInvoiceAsync(99999, TenantA));

            var count = await context.StockMovements
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.TenantId == TenantA &&
                    x.ReferenceInvoiceId == 99999);

            Assert.Equal(0, count);
        }
        finally
        {
            _db.Tenant.TenantId = previousTenant;
        }
    }

    [Fact]
    public async Task ReverseInvoice_Purchase_InsufficientStock_RollsBackReversal()
    {
        var (productId, warehouseId) = await CreateInventoryDataAsync();

        // فاکتور خرید ۵ عدد کالا
        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Purchase,
            quantity: 5m);

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);

            // ورود ۵ عدد از طریق فاکتور خرید
            await service.CreateFromInvoiceAsync(invoiceId, TenantA);

            // مصرف تمام موجودی؛ موجودی فعلی صفر می‌شود.
            await CreateAndPostAsync(
                productId,
                warehouseId,
                StockMovementType.Issue,
                5m);

            // برگشت خرید باید به دلیل کمبود موجودی ناموفق شود.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ReverseInvoiceAsync(invoiceId, TenantA));
        }

        // بررسی وضعیت دیتابیس پس از شکست عملیات
        await using var verifyContext = _db.NewContext();

        var invoiceMovements = await verifyContext.StockMovements
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == TenantA &&
                x.ReferenceInvoiceId == invoiceId)
            .ToListAsync();

        // فقط گردش ورود اصلی باید باقی مانده باشد.
        Assert.Single(invoiceMovements);

        Assert.Equal(
            StockMovementStatus.Posted,
            invoiceMovements[0].Status);

        Assert.Null(invoiceMovements[0].ReversesMovementId);

        // گردش خروج مصرف کالا نیز باید باقی مانده باشد.
        var totalMovements = await verifyContext.StockMovements
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == TenantA);

        Assert.Equal(2, totalMovements);

        // موجودی همچنان صفر است.
        var serviceAfterFailure = NewService(verifyContext);

        var kardex = await serviceAfterFailure.GetKardexAsync(
            productId,
            warehouseId,
            null,
            null,
            TenantA);

        Assert.Equal(0m, kardex[^1].Balance);
    }

    [Fact]
    public async Task ReverseInvoice_WithoutStockMovement_ThrowsException()
    {
        var (productId, _) = await CreateInventoryDataAsync();

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Purchase,
            quantity: 5m);

        await using var context = _db.NewContext();
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReverseInvoiceAsync(invoiceId, TenantA));

        var count = await context.StockMovements
            .IgnoreQueryFilters()
            .CountAsync(x =>
                x.TenantId == TenantA &&
                x.ReferenceInvoiceId == invoiceId);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ReverseInvoice_NonExistingInvoiceId_ThrowsException()
    {
        await CreateInventoryDataAsync();

        await using var context = _db.NewContext();
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReverseInvoiceAsync(999999, TenantA));

        var count = await context.StockMovements
            .IgnoreQueryFilters()
            .CountAsync(x =>
                x.TenantId == TenantA &&
                x.ReferenceInvoiceId == 999999);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ReverseInvoice_ReversesAllActiveOriginalMovements()
    {
        var (productId, warehouseId) = await CreateInventoryDataAsync();

        var invoiceId = await CreateInvoiceAsync(
            productId,
            InvoiceType.Purchase,
            quantity: 5m);

        await using var context = _db.NewContext();
        var service = NewService(context);

        for (var i = 0; i < 2; i++)
        {
            var movementId = await service.CreateAsync(
                new StockMovementRequest
                {
                    Date = TestDate,
                    MovementType = (int)StockMovementType.Receipt,
                    DestinationWarehouseId = warehouseId,
                    ReferenceInvoiceId = invoiceId,
                    Description = "گردش آزمایشی تکراری",
                    Items =
                    [
                        new StockMovementItemRequest
                    {
                        ProductId = productId,
                        Quantity = 5m,
                        UnitCost = 100m
                    }
                    ]
                },
                TenantA);

            await service.PostAsync(movementId, TenantA);
        }

        await service.ReverseInvoiceAsync(invoiceId, TenantA);

        var movements = await context.StockMovements
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == TenantA &&
                x.ReferenceInvoiceId == invoiceId)
            .ToListAsync();

        Assert.Equal(4, movements.Count);
        Assert.Equal(2, movements.Count(x => x.ReversesMovementId.HasValue));
        Assert.All(movements, x => Assert.Equal(StockMovementStatus.Posted, x.Status));
    }

    [Fact]
    public async Task InventoryAnalytics_UsesMovingWeightedAverageAndValuesTransfersAtSourceAverage()
    {
        var (productId, sourceId) = await CreateInventoryDataAsync();
        await CreateAndPostAsync(productId, sourceId, StockMovementType.Receipt, 10m, 100m);
        await CreateAndPostAsync(productId, sourceId, StockMovementType.Issue, 4m);
        await CreateAndPostAsync(productId, sourceId, StockMovementType.Receipt, 10m, 200m);

        int destinationId;
        await using (var context = _db.NewContext())
        {
            var destination = new Warehouse
            {
                TenantId = TenantA,
                Code = "WH-VALUATION",
                Name = "انبار ارزش‌گذاری",
                IsActive = true
            };
            context.Warehouses.Add(destination);
            await context.SaveChangesAsync();
            destinationId = destination.Id;
        }

        await using (var context = _db.NewContext())
        {
            var service = NewService(context);
            var transferId = await service.CreateAsync(new StockMovementRequest
            {
                Date = TestDate.AddDays(1),
                MovementType = (int)StockMovementType.Transfer,
                SourceWarehouseId = sourceId,
                DestinationWarehouseId = destinationId,
                Items = [new StockMovementItemRequest { ProductId = productId, Quantity = 4m }]
            }, TenantA);
            await service.PostAsync(transferId, TenantA);
        }

        _db.Tenant.TenantId = TenantA;
        await using var reportContext = _db.NewContext();
        var report = new InventoryAnalyticsService(reportContext);
        var rows = await report.GetInventoryValuationAsync(TenantA, TestDate.AddDays(1));
        var source = Assert.Single(rows, x => x.WarehouseId == sourceId);
        var destinationRow = Assert.Single(rows, x => x.WarehouseId == destinationId);

        Assert.Equal(12m, source.Quantity);
        Assert.Equal(1950m, source.TotalValue);
        Assert.Equal(4m, destinationRow.Quantity);
        Assert.Equal(650m, destinationRow.TotalValue);
        Assert.Equal(2600m, rows.Sum(x => x.TotalValue));
    }

    [Fact]
    public async Task InventoryAnalytics_ReportsShortageAgainstProductMinimum()
    {
        var (productId, warehouseId) = await CreateInventoryDataAsync();
        _db.Tenant.TenantId = TenantA;
        await using (var context = _db.NewContext())
        {
            var product = await context.Products.SingleAsync(x => x.Id == productId);
            product.MinimumStock = 12m;
            await context.SaveChangesAsync();
        }
        await CreateAndPostAsync(productId, warehouseId, StockMovementType.Receipt, 10m);

        _db.Tenant.TenantId = TenantA;
        await using var reportContext = _db.NewContext();
        var report = new InventoryAnalyticsService(reportContext);
        var row = Assert.Single(await report.GetBelowMinimumStockAsync(TenantA));

        Assert.Equal(10m, row.CurrentStock);
        Assert.Equal(12m, row.MinimumStock);
        Assert.Equal(2m, row.Shortage);
    }

}
