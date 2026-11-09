using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Data;

namespace MyFirstCoreApp.Infrastructure.Services;

public class StockMovementService : IStockMovementService
{
    private readonly ApplicationDbContext _context;
    private readonly IWarehouseService _warehouseService;
    public StockMovementService(ApplicationDbContext context, IWarehouseService warehouseService)
    {
        _context = context;
        _warehouseService = warehouseService;
    }

    public Task<int> CreateAsync(
        StockMovementRequest request,
        int tenantId)
    {
        return _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                ValidateRequest(request);

                await ValidateWarehousesAsync(request, tenantId);
                await ValidateProductsAsync(request.Items, tenantId);
                await ValidateReferenceInvoiceAsync(
                    request.ReferenceInvoiceId, tenantId);

                var movement = new StockMovement
                {
                    TenantId = tenantId,
                    Number = await GenerateNumberAsync(
                        request.Date, tenantId),
                    Date = request.Date,
                    MovementType = (StockMovementType)request.MovementType,
                    Status = StockMovementStatus.Draft,
                    SourceWarehouseId = request.SourceWarehouseId,
                    DestinationWarehouseId = request.DestinationWarehouseId,
                    ReferenceInvoiceId = request.ReferenceInvoiceId,
                    Description = request.Description?.Trim(),
                    Items = BuildItems(request.Items, tenantId)
                };

                _context.StockMovements.Add(movement);

                await _context.SaveChangesAsync();

                return movement.Id;
            });
    }

    public async Task UpdateAsync(
        StockMovementRequest request,
        int tenantId)
    {
        await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable,
        async () =>
        {
            if (request.Id <= 0)
                throw new InvalidOperationException("شناسه سند معتبر نیست.");

            ValidateRequest(request);

            var movement = await _context.StockMovements
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x =>
                    x.Id == request.Id &&
                    x.TenantId == tenantId);

            if (movement is null)
                throw new InvalidOperationException("سند گردش انبار پیدا نشد.");

            if (movement.Status != StockMovementStatus.Draft)
                throw new InvalidOperationException("فقط سند پیش‌نویس قابل ویرایش است.");

            await ValidateWarehousesAsync(request, tenantId);
            await ValidateProductsAsync(request.Items, tenantId);
            await ValidateReferenceInvoiceAsync(request.ReferenceInvoiceId, tenantId);

            _context.StockMovementItems.RemoveRange(movement.Items);
            movement.Items.Clear();

            movement.Date = request.Date;
            movement.MovementType = (StockMovementType)request.MovementType;
            movement.SourceWarehouseId = request.SourceWarehouseId;
            movement.DestinationWarehouseId = request.DestinationWarehouseId;
            movement.ReferenceInvoiceId = request.ReferenceInvoiceId;
            movement.Description = request.Description?.Trim();

            foreach (var item in BuildItems(request.Items, tenantId))
                movement.Items.Add(item);

            await _context.SaveChangesAsync();
        });
    }
    public Task PostAsync(int id, int tenantId)
    {
        // ثبت عادی گردش انبار؛ بدون مجوز منفی‌شدن موجودی
        return PostInternalAsync(
            id,
            tenantId,
            allowNegativeStockForSale: false);
    }

    private async Task PostInternalAsync(
        int id,
        int tenantId,
        bool allowNegativeStockForSale,
        bool allowNegativeStockForPurchaseReturn = false,
        bool allowNegativeStockForPurchaseReversal = false)
    {
        await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                var movement = await _context.StockMovements.Include(x => x.Items)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.TenantId == tenantId);

                if (movement is null)
                    throw new InvalidOperationException("سند گردش انبار پیدا نشد.");

                if (movement.Status != StockMovementStatus.Draft)
                    throw new InvalidOperationException("فقط سند پیش‌نویس قابل قطعی‌شدن است.");

                if (movement.Items.Count == 0)
                    throw new InvalidOperationException("سند باید حداقل یک ردیف کالا داشته باشد.");

                var request = new StockMovementRequest
                {
                    Id = movement.Id,
                    Date = movement.Date,
                    MovementType = (int)movement.MovementType,
                    SourceWarehouseId = movement.SourceWarehouseId,
                    DestinationWarehouseId = movement.DestinationWarehouseId,
                    ReferenceInvoiceId = movement.ReferenceInvoiceId,
                    Description = movement.Description,
                    Items = movement.Items.Select(x =>
                        new StockMovementItemRequest
                        {
                            ProductId = x.ProductId,
                            Quantity = x.Quantity,
                            UnitCost = x.UnitCost,
                            Description = x.Description
                        }).ToList()
                };

                ValidateRequest(request);
                await ValidateWarehousesAsync(request, tenantId);
                await ValidateProductsAsync(request.Items, tenantId);
                await ValidateReferenceInvoiceAsync(request.ReferenceInvoiceId, tenantId);
                var canAllowNegativeStock =
                    await CanAllowNegativeStockForSaleAsync(
                        movement,
                        tenantId,
                        allowNegativeStockForSale)
                    || await CanAllowNegativeStockForPurchaseReturnAsync(
                        movement,
                        tenantId,
                        allowNegativeStockForPurchaseReturn)
                    || await CanAllowNegativeStockForPurchaseReversalAsync(
                        movement,
                        tenantId,
                        allowNegativeStockForPurchaseReversal);
                // حواله و انتقال نباید باعث منفی‌شدن موجودی انبار مبدأ شوند.
                if (!canAllowNegativeStock && movement.MovementType is StockMovementType.Issue or StockMovementType.Transfer)
                {
                    var requiredByProduct = movement.Items
                        .GroupBy(x => x.ProductId)
                        .Select(g => new
                        {
                            ProductId = g.Key,
                            Quantity = g.Sum(x => x.Quantity)
                        });

                    foreach (var item in requiredByProduct)
                    {
                        await ValidateHistoricalStockAsync(
                            item.ProductId,
                            movement.SourceWarehouseId!.Value,
                            movement.Date,
                            item.Quantity,
                            tenantId);

                        var balance = await GetBalanceInternalAsync(
                            item.ProductId,
                            movement.SourceWarehouseId!.Value,
                            tenantId);

                        if (balance < item.Quantity)
                        {
                            throw new InvalidOperationException($"موجودی کالا با شناسه {item.ProductId} کافی نیست. " + $"موجودی فعلی: {balance}، مقدار موردنیاز: {item.Quantity}.");
                        }
                    }
                }

                movement.Status = StockMovementStatus.Posted;
                movement.PostedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            });
    }
    
    public async Task CancelAsync(int id, int tenantId)
    {
        await _context.ExecuteInTransactionAsync(IsolationLevel.Serializable,
            async () =>
            {
                var movement = await _context.StockMovements
                .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId);

                if (movement is null)
                    throw new InvalidOperationException("سند گردش انبار پیدا نشد.");

                if (movement.Status != StockMovementStatus.Draft)
                    throw new InvalidOperationException(
                        "فقط سند پیش‌نویس قابل لغو است. " +
                        "برای اصلاح سند قطعی‌شده باید گردش معکوس ثبت شود.");

                movement.Status = StockMovementStatus.Cancelled;

                await _context.SaveChangesAsync();
            });
    }

    private static void ValidateRequest(StockMovementRequest request)
    {
        if (request.Date == default)
            throw new InvalidOperationException("تاریخ سند معتبر نیست.");

        if (!Enum.IsDefined(typeof(StockMovementType), request.MovementType))
            throw new InvalidOperationException("نوع گردش انبار معتبر نیست.");

        if (request.Items is null || request.Items.Count == 0)
            throw new InvalidOperationException(
                "سند باید حداقل یک ردیف کالا داشته باشد.");

        if (request.Items.Any(x =>
            x.ProductId <= 0 || x.Quantity <= 0))
        {
            throw new InvalidOperationException(
                "کالا باید انتخاب شده باشد و مقدار آن بیشتر از صفر باشد.");
        }

        if (request.Items.Any(x => x.UnitCost < 0))
            throw new InvalidOperationException(
                "بهای واحد نمی‌تواند منفی باشد.");

        if (request.Items
            .GroupBy(x => x.ProductId)
            .Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException(
                "در هر سند، هر کالا را فقط یک بار وارد کنید.");
        }

        var type = (StockMovementType)request.MovementType;

        switch (type)
        {
            case StockMovementType.Opening:
            case StockMovementType.Receipt:
                if (request.SourceWarehouseId.HasValue ||
                    !request.DestinationWarehouseId.HasValue)
                {
                    throw new InvalidOperationException(
                        "برای موجودی اول دوره و رسید، فقط انبار مقصد باید مشخص شود.");
                }
                break;

            case StockMovementType.Issue:
                if (!request.SourceWarehouseId.HasValue ||
                    request.DestinationWarehouseId.HasValue)
                {
                    throw new InvalidOperationException(
                        "برای حواله، فقط انبار مبدأ باید مشخص شود.");
                }
                break;

            case StockMovementType.Transfer:
                if (!request.SourceWarehouseId.HasValue ||
                    !request.DestinationWarehouseId.HasValue)
                {
                    throw new InvalidOperationException(
                        "برای انتقال، انبار مبدأ و مقصد باید مشخص شوند.");
                }

                if (request.SourceWarehouseId ==
                    request.DestinationWarehouseId)
                {
                    throw new InvalidOperationException(
                        "انبار مبدأ و مقصد انتقال باید متفاوت باشند.");
                }
                break;
        }
    }

    private async Task ValidateWarehousesAsync(
        StockMovementRequest request,
        int tenantId)
    {
        var warehouseIds = new[]
        {
            request.SourceWarehouseId,
            request.DestinationWarehouseId
        }
        .Where(x => x.HasValue)
        .Select(x => x!.Value)
        .Distinct()
        .ToList();

        var validCount = await _context.Warehouses
            .CountAsync(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                warehouseIds.Contains(x.Id));

        if (validCount != warehouseIds.Count)
            throw new InvalidOperationException(
                "یکی از انبارها وجود ندارد، غیرفعال است یا متعلق به شرکت جاری نیست.");
    }

    private async Task ValidateProductsAsync(
        List<StockMovementItemRequest> items,
        int tenantId)
    {
        var productIds = items
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var validCount = await _context.Products
            .CountAsync(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                x.TrackInventory &&
                productIds.Contains(x.Id));

        if (validCount != productIds.Count)
        {
            throw new InvalidOperationException(
                "یکی از کالاها وجود ندارد، غیرفعال است، " +
                "متعلق به شرکت جاری نیست یا موجودی انبار آن کنترل نمی‌شود.");
        }
    }

    private async Task ValidateReferenceInvoiceAsync(
        int? invoiceId,
        int tenantId)
    {
        if (!invoiceId.HasValue)
            return;

        var exists = await _context.Invoices
            .AnyAsync(x =>
                x.Id == invoiceId.Value &&
                x.TenantId == tenantId);

        if (!exists)
            throw new InvalidOperationException(
                "فاکتور مرجع پیدا نشد یا متعلق به شرکت جاری نیست.");
    }

    private async Task<string> GenerateNumberAsync(
        DateOnly date,
        int tenantId)
    {
        var prefix = $"ST-{date.Year}-";

        // شماره اسناد حذف‌شده نیز در نظر گرفته می‌شود
        // تا شماره قدیمی دوباره استفاده نشود.
        var numbers = await _context.StockMovements
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Number.StartsWith(prefix))
            .Select(x => x.Number)
            .ToListAsync();

        var maxNumber = numbers
            .Select(x => x.Substring(prefix.Length))
            .Select(x => int.TryParse(x, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{maxNumber + 1:000000}";
    }

    private static List<StockMovementItem> BuildItems(
        List<StockMovementItemRequest> items,
        int tenantId)
    {
        return items.Select(x => new StockMovementItem
        {
            TenantId = tenantId,
            ProductId = x.ProductId,
            Quantity = x.Quantity,
            UnitCost = x.UnitCost,
            Description = x.Description?.Trim()
        }).ToList();
    }

    private async Task<bool> CanAllowNegativeStockForSaleAsync(
    StockMovement movement,
    int tenantId,
    bool allowNegativeStockForSale)
    {
        // این قابلیت فقط از مسیر داخلی ثبت فاکتور قابل فعال‌شدن است.
        if (!allowNegativeStockForSale)
            return false;

        // فقط حواله خروج مربوط به فاکتور فروش
        if (movement.MovementType != StockMovementType.Issue ||
            !movement.ReferenceInvoiceId.HasValue ||
            movement.ReversesMovementId.HasValue)
        {
            return false;
        }

        // اطمینان از اینکه سند واقعاً مربوط به فاکتور فروش همین شرکت است.
        var isSaleInvoice = await _context.Invoices.AnyAsync(x =>
            x.Id == movement.ReferenceInvoiceId.Value &&
            x.TenantId == tenantId &&
            x.InvoiceType == InvoiceType.Sale);

        if (!isSaleInvoice)
            return false;

        // بررسی تنظیم اختصاصی شرکت
        return await _context.InvoiceBehaviorSettings.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.AllowNegativeStockOnSales);
    }

    private async Task<bool> CanAllowNegativeStockForPurchaseReturnAsync(
        StockMovement movement,
        int tenantId,
        bool allowNegativeStockForPurchaseReturn)
    {
        // این قابلیت فقط از مسیر داخلی ثبت فاکتور قابل فعال‌شدن است.
        if (!allowNegativeStockForPurchaseReturn)
            return false;

        // فقط حواله خروج مستقیم و غیرمعکوس مربوط به برگشت از خرید
        if (movement.MovementType != StockMovementType.Issue ||
            !movement.ReferenceInvoiceId.HasValue ||
            movement.ReversesMovementId.HasValue)
        {
            return false;
        }

        var isPurchaseReturnInvoice = await _context.Invoices.AnyAsync(x =>
            x.Id == movement.ReferenceInvoiceId.Value &&
            x.TenantId == tenantId &&
            x.InvoiceType == InvoiceType.PurchaseReturn);

        if (!isPurchaseReturnInvoice)
            return false;

        return await _context.InvoiceBehaviorSettings.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.AllowNegativeStockOnPurchaseReturns);
    }

    private async Task<bool> CanAllowNegativeStockForPurchaseReversalAsync(
        StockMovement movement,
        int tenantId,
        bool allowNegativeStockForPurchaseReversal)
    {
        // این مجوز فقط هنگام برگشت گردش انبار فاکتور خرید به پیش‌نویس فعال می‌شود.
        if (!allowNegativeStockForPurchaseReversal ||
            movement.MovementType != StockMovementType.Issue ||
            !movement.ReferenceInvoiceId.HasValue ||
            !movement.ReversesMovementId.HasValue)
        {
            return false;
        }

        var isPurchaseInvoiceReversal = await (
            from invoice in _context.Invoices
            join originalMovement in _context.StockMovements
                on invoice.Id equals originalMovement.ReferenceInvoiceId
            where invoice.Id == movement.ReferenceInvoiceId.Value
                  && invoice.TenantId == tenantId
                  && invoice.InvoiceType == InvoiceType.Purchase
                  && originalMovement.Id == movement.ReversesMovementId.Value
                  && originalMovement.TenantId == tenantId
                  && originalMovement.MovementType == StockMovementType.Receipt
                  && originalMovement.ReversesMovementId == null
            select invoice.Id
        ).AnyAsync();

        if (!isPurchaseInvoiceReversal)
            return false;

        return await _context.InvoiceBehaviorSettings.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.AllowNegativeStockOnPurchaseReversal);
    }

    private async Task<decimal> GetBalanceInternalAsync(
        int productId,
        int warehouseId,
        int tenantId)
    {
        var movements = _context.StockMovements
            .Where(x =>
                x.TenantId == tenantId &&
                x.Status == StockMovementStatus.Posted);

        var items = _context.StockMovementItems
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProductId == productId);

        return await (
            from item in items
            join movement in movements
                on item.StockMovementId equals movement.Id
            select
                (movement.DestinationWarehouseId == warehouseId
                    ? item.Quantity : 0m)
                -
                (movement.SourceWarehouseId == warehouseId
                    ? item.Quantity : 0m)
        ).SumAsync();
    }

    private async Task ValidateHistoricalStockAsync(
        int productId,
        int warehouseId,
        DateOnly movementDate,
        decimal outgoingQuantity,
        int tenantId)
    {
        var dailyMovements = await (
            from item in _context.StockMovementItems
            join movement in _context.StockMovements
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && item.ProductId == productId
                  && movement.Status == StockMovementStatus.Posted
                  && (movement.SourceWarehouseId == warehouseId
                      || movement.DestinationWarehouseId == warehouseId)
            select new
            {
                movement.Date,
                Delta =
                    (movement.DestinationWarehouseId == warehouseId
                        ? item.Quantity : 0m)
                    -
                    (movement.SourceWarehouseId == warehouseId
                        ? item.Quantity : 0m)
            }
        ).ToListAsync();

        var balance = dailyMovements
            .Where(x => x.Date < movementDate)
            .Sum(x => x.Delta);

        var futureDailyChanges = dailyMovements
            .Where(x => x.Date >= movementDate)
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Delta));

        var datesToCheck = futureDailyChanges.Keys
            .Append(movementDate)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var productName = await _context.Products
            .Where(x =>
                x.Id == productId &&
                x.TenantId == tenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync();

        var displayName = string.IsNullOrWhiteSpace(productName)
            ? $"شناسه {productId}"
            : $"«{productName}» (شناسه {productId})";

        foreach (var date in datesToCheck)
        {
            futureDailyChanges.TryGetValue(date, out var dailyChange);

            if (date == movementDate)
                dailyChange -= outgoingQuantity;

            balance += dailyChange;

            if (balance < 0)
            {
                throw new InvalidOperationException(
                    $"موجودی کالای {displayName} در انبار {warehouseId} " +
                    $"پس از ثبت سند در تاریخ {@PersianDate.Format(date)} منفی می‌شود. " +
                    $"موجودی محاسبه‌شده: {balance}.");
            }
        }
    }

    public async Task CreateFromInvoiceAsync(int invoiceId, int tenantId)
    {
        var invoice = await _context.Invoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == invoiceId &&
                x.TenantId == tenantId);

        if (invoice is null)
            throw new InvalidOperationException("فاکتور پیدا نشد.");

        if (invoice.InvoiceType == InvoiceType.PreInvoice)
            return;

        if (invoice.InvoiceType is not (
            InvoiceType.Sale
            or InvoiceType.Purchase
            or InvoiceType.SalesReturn
            or InvoiceType.PurchaseReturn))
        {
            throw new InvalidOperationException(
                "نوع فاکتور برای ثبت گردش انبار معتبر نیست.");
        }

        if (invoice.Items.Count == 0)
            throw new InvalidOperationException(
                "فاکتور بدون قلم کالا قابل ثبت در انبار نیست.");

        // فقط اقلامی که موجودی انبار برای آن‌ها کنترل می‌شود.
        var productIds = invoice.Items
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var trackedProductIds = await _context.Products
            .Where(x =>
                x.TenantId == tenantId &&
                x.TrackInventory &&
                productIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        var trackedProductIdSet = trackedProductIds.ToHashSet();

        var trackedItems = invoice.Items
            .Where(x => trackedProductIdSet.Contains(x.ProductId))
            .ToList();

        // فاکتور فقط خدمات است؛ نیازی به گردش انبار نیست.
        if (trackedItems.Count == 0)
            return;

        // از ثبت دوباره اثر انبار جلوگیری می‌کنیم.
        var hasActiveMovement = await _context.StockMovements
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ReferenceInvoiceId == invoiceId &&
                x.ReversesMovementId == null &&
                x.Status == StockMovementStatus.Posted &&
                !_context.StockMovements.Any(r =>
                    r.TenantId == tenantId &&
                    r.ReversesMovementId == x.Id &&
                    r.Status == StockMovementStatus.Posted));

        if (hasActiveMovement)
        {
            throw new InvalidOperationException(
                "برای این فاکتور قبلاً گردش انبار قطعی و فعال ثبت شده است.");
        }

        var isIssue = invoice.InvoiceType is InvoiceType.Sale or InvoiceType.PurchaseReturn;

        int? defaultWarehouseId = null;
        if (trackedItems.Any(item => !item.WarehouseId.HasValue))
            defaultWarehouseId = await _warehouseService.GetDefaultWarehouseIdAsync(tenantId);

        var movementsByWarehouse = trackedItems
            .GroupBy(item => item.WarehouseId ?? defaultWarehouseId!.Value)
            .ToList();

        foreach (var warehouseGroup in movementsByWarehouse)
        {
            var request = new StockMovementRequest
            {
                Date = invoice.Date,
                MovementType = isIssue
                    ? (int)StockMovementType.Issue
                    : (int)StockMovementType.Receipt,

                SourceWarehouseId = isIssue ? warehouseGroup.Key : null,
                DestinationWarehouseId = isIssue ? null : warehouseGroup.Key,

                ReferenceInvoiceId = invoice.Id,

                Description = invoice.InvoiceType switch
                {
                    InvoiceType.Sale =>
                        $"خروج کالا بابت فاکتور فروش شماره {invoice.Number}",

                    InvoiceType.Purchase =>
                        $"ورود کالا بابت فاکتور خرید شماره {invoice.Number}",

                    InvoiceType.SalesReturn =>
                        $"ورود کالا بابت برگشت از فروش شماره {invoice.Number}",

                    InvoiceType.PurchaseReturn =>
                        $"خروج کالا بابت برگشت از خرید شماره {invoice.Number}",

                    _ => "گردش انبار بابت فاکتور"
                },

                Items = warehouseGroup
                    .GroupBy(item => item.ProductId)
                    .Select(group => new StockMovementItemRequest
                    {
                        ProductId = group.Key,

                        Quantity = group.Sum(x => x.Quantity),

                        UnitCost = invoice.InvoiceType is
                            InvoiceType.Purchase or InvoiceType.SalesReturn
                                ? group.Sum(x => x.Quantity * x.UnitPrice) /
                                  group.Sum(x => x.Quantity)
                                : null,

                        Description = group
                            .Select(x => x.Description)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    })
                    .ToList()
            };

            var movementId = await CreateAsync(request, tenantId);

            await PostInternalAsync(
                movementId,
                tenantId,
                allowNegativeStockForSale: true,
                allowNegativeStockForPurchaseReturn:
                    invoice.InvoiceType == InvoiceType.PurchaseReturn);
        }

    }

    public async Task ReverseInvoiceAsync(int invoiceId, int tenantId)
    {
        await _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                var invoice = await _context.Invoices
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(i =>
                        i.Id == invoiceId &&
                        i.TenantId == tenantId);

                if (invoice is null)
                    throw new InvalidOperationException("فاکتور موردنظر پیدا نشد.");

                var originalMovements = await _context.StockMovements
                    .Include(x => x.Items)
                    .Where(x =>
                        x.TenantId == tenantId &&
                        x.ReferenceInvoiceId == invoiceId &&
                        x.ReversesMovementId == null &&
                        x.Status == StockMovementStatus.Posted &&
                        (x.MovementType == StockMovementType.Issue ||
                         x.MovementType == StockMovementType.Receipt))
                    .ToListAsync();

                var reversedMovementIds = await _context.StockMovements
                    .Where(x =>
                        x.TenantId == tenantId &&
                        x.ReversesMovementId.HasValue &&
                        x.Status == StockMovementStatus.Posted)
                    .Select(x => x.ReversesMovementId!.Value)
                    .ToHashSetAsync();

                var movements = originalMovements
                    .Where(x => !reversedMovementIds.Contains(x.Id))
                    .ToList();

                if (originalMovements.Count > 0 && movements.Count == 0)
                {
                    throw new InvalidOperationException(
                        "گردش انبار این فاکتور قبلاً برگشت داده شده است.");
                }

                if (originalMovements.Count == 0)
                {
                    var productIds = invoice.Items
                        .Select(x => x.ProductId)
                        .Distinct()
                        .ToList();

                    var hasTrackedItems = await _context.Products.AnyAsync(x =>
                        x.TenantId == tenantId &&
                        x.TrackInventory &&
                        productIds.Contains(x.Id));

                    if (invoice.Status == InvoiceStatus.Posted && hasTrackedItems)
                    {
                        throw new InvalidOperationException(
                            "فاکتور قطعی‌شده دارای کالای انبارگردانی‌شونده است، " +
                            "اما گردش انبار متناظر با آن پیدا نشد.");
                    }

                    return;
                }

                if (movements.Count == 0)
                    return;

                foreach (var original in movements)
                {
                    if (original.Items.Count == 0)
                        throw new InvalidOperationException(
                            "گردش انبار اصلی فاقد ردیف کالا است.");

                    var isReversingIssue =
                        original.MovementType == StockMovementType.Issue;

                    var reversalDate = DateOnly.FromDateTime(DateTime.Today);

                    var reversal = new StockMovement
                    {
                        TenantId = tenantId,
                        Number = await GenerateNumberAsync(reversalDate, tenantId),
                        Date = reversalDate,
                        MovementType = isReversingIssue
                            ? StockMovementType.Receipt
                            : StockMovementType.Issue,
                        Status = StockMovementStatus.Draft,

                        SourceWarehouseId = isReversingIssue
                            ? null
                            : original.DestinationWarehouseId,

                        DestinationWarehouseId = isReversingIssue
                            ? original.SourceWarehouseId
                            : null,

                        ReferenceInvoiceId = invoiceId,
                        ReversesMovementId = original.Id,
                        Description = $"گردش معکوس سند {original.Number}",

                        Items = BuildItems(
                            original.Items.Select(x =>
                                new StockMovementItemRequest
                                {
                                    ProductId = x.ProductId,
                                    Quantity = x.Quantity,
                                    UnitCost = x.UnitCost,
                                    Description = x.Description
                                }).ToList(),
                            tenantId)
                    };

                    _context.StockMovements.Add(reversal);
                    await _context.SaveChangesAsync();

                    await PostInternalAsync(
                        reversal.Id,
                        tenantId,
                        allowNegativeStockForSale: false,
                        allowNegativeStockForPurchaseReversal:
                            invoice.InvoiceType == InvoiceType.Purchase);
                }
            });
    }

    public Task<decimal> GetBalanceAsync(
    int productId,
    int warehouseId,
    int tenantId)
    {
        return GetBalanceInternalAsync(
            productId,
            warehouseId,
            tenantId);
    }

    public async Task<List<KardexRowDto>> GetKardexAsync(
        int productId,
        int warehouseId,
        DateOnly? from,
        DateOnly? to,
        int tenantId)
    {
        if (productId <= 0)
            throw new InvalidOperationException("شناسه کالا معتبر نیست.");

        if (warehouseId <= 0)
            throw new InvalidOperationException("شناسه انبار معتبر نیست.");

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new InvalidOperationException(
                "تاریخ شروع نباید بعد از تاریخ پایان باشد.");

        var query =
            from item in _context.StockMovementItems
            join movement in _context.StockMovements
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && item.ProductId == productId
                  && movement.Status == StockMovementStatus.Posted
                  && (movement.SourceWarehouseId == warehouseId
                      || movement.DestinationWarehouseId == warehouseId)
            select new
            {
                movement.Id,
                movement.Date,
                movement.Number,
                Description = movement.Description ?? item.Description ?? string.Empty,

                Incoming = movement.DestinationWarehouseId == warehouseId
                    ? item.Quantity
                    : 0m,

                Outgoing = movement.SourceWarehouseId == warehouseId
                    ? item.Quantity
                    : 0m
            };

        // اگر تاریخ پایان مشخص باشد، گردش‌های بعد از آن لازم نیستند.
        if (to.HasValue)
            query = query.Where(x => x.Date <= to.Value);

        // محاسبه مانده قبل از تاریخ شروع.
        // این مانده در اولین ردیف بازه نیز لحاظ می‌شود.
        decimal balance = 0m;

        if (from.HasValue)
        {
            var openingRows = await query
                .Where(x => x.Date < from.Value)
                .Select(x => new
                {
                    x.Incoming,
                    x.Outgoing
                })
                .ToListAsync();

            balance = openingRows.Sum(x => x.Incoming - x.Outgoing);
        }

        // دریافت گردش‌های داخل بازه گزارش.
        var movementsQuery = query;

        if (from.HasValue)
            movementsQuery = movementsQuery.Where(x => x.Date >= from.Value);

        var movements = await movementsQuery
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Number)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Date,
                x.Number,
                x.Description,
                x.Incoming,
                x.Outgoing
            })
            .ToListAsync();

        var result = new List<KardexRowDto>(movements.Count);

        foreach (var movement in movements)
        {
            balance += movement.Incoming - movement.Outgoing;

            result.Add(new KardexRowDto
            {
                Date = movement.Date,
                DocumentNumber = movement.Number,
                Description = movement.Description,
                Incoming = movement.Incoming,
                Outgoing = movement.Outgoing,
                Balance = balance
            });
        }

        return result;
    }
}