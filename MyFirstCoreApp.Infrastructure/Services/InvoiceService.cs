using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Invoicing;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// قواعد این سرویس:
///  - ورودی ثبت/ویرایش «درخواست» است (InvoiceRequest)، نه Entity؛ مبالغ، شماره و وضعیت هرگز از بیرون نمی‌آیند.
///  - هر عملیات چندمرحله‌ای داخل یک تراکنش دیتابیس است.
///  - مبالغ فقط با InvoiceCalculator و باقی‌مانده فقط با InvoiceBalance محاسبه می‌شود.
///  - سند حسابداری فقط از مسیر ITransactionService ساخته/حذف می‌شود.
///  - Invoice.TransactionId همیشه سند اصلی فاکتور است؛ سند تخفیف تسویه آن را عوض نمی‌کند.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private const int MaxNumberingAttempts = 3;
    private const string DocumentReferenceType = "Invoice";

    private readonly ApplicationDbContext _context;
    private readonly IStockMovementService _stockMovementService;
    private readonly ITransactionService _transactionService;
    private readonly ISystemAccountService _systemAccounts;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly IInvoiceService _invoiceService;
    public InvoiceService(
        IStockMovementService stockMovementService,
        ApplicationDbContext context,
        ITransactionService transactionService,
        ISystemAccountService systemAccounts,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _stockMovementService = stockMovementService;
        _context = context;
        _transactionService = transactionService;
        _systemAccounts = systemAccounts;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    // ============================================
    // شماره‌گذاری
    // ============================================

    public async Task<int> GetNextNumberAsync(int tenantId, InvoiceType invoiceType)
    {
        // رکوردهای حذف‌شده‌ی نرم هم شمرده می‌شوند تا شماره هرگز تکرار نشود
        var maxNumber = await _context.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.InvoiceType == invoiceType)
            .MaxAsync(i => (int?)i.Number) ?? 0;

        return maxNumber + 1;
    }

    // ============================================
    // ثبت
    // ============================================

    public Task<Invoice> CreateAsync(InvoiceRequest request, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            ValidateRequest(request);

            var contactId = await ResolveContactAsync(request, tenantId);
            var items = await BuildItemsAsync(request, tenantId);

            var invoice = new Invoice
            {
                TenantId = tenantId,
                Number = 0, // خودکار
                Date = request.Date,
                InvoiceType = request.InvoiceType,
                ContactId = contactId,
                Status = InvoiceStatus.Draft,
                Description = request.Description?.Trim(),
                Items = items
            };

            InvoiceCalculator.Calculate(invoice);

            await SaveNewInvoiceAsync(invoice, "ایجاد فاکتور");

            return invoice;
        });
    }

    // ============================================
    // ویرایش
    // ============================================

    public Task UpdateAsync(InvoiceRequest request, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            ValidateRequest(request);

            var invoice = await GetInvoiceWithItemsAsync(request.Id, tenantId);

            if (invoice.Status == InvoiceStatus.Posted)
                throw new InvalidOperationException("فاکتور قطعی قابل ویرایش نیست.");

            if (invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Revised)
                throw new InvalidOperationException("فقط فاکتور پیش‌نویس یا پیش‌فاکتور اصلاح‌شده قابل ویرایش است.");

            // شماره‌گذاری برای هر نوع جداست؛ تغییر نوع، شماره را تکراری می‌کرد
            if (invoice.InvoiceType != request.InvoiceType)
                throw new InvalidOperationException("نوع فاکتور بعد از ثبت قابل تغییر نیست.");

            var contactId = await ResolveContactAsync(request, tenantId);
            var newItems = await BuildItemsAsync(request, tenantId);

            invoice.Date = request.Date;
            invoice.ContactId = contactId;
            invoice.Description = request.Description?.Trim();

            // اقلام قدیمی حذف نرم (Interceptor)، اقلام جدید اضافه
            _context.InvoiceItems.RemoveRange(invoice.Items.ToList());
            foreach (var item in newItems)
                invoice.Items.Add(item);

            // جمع‌ها فقط از روی اقلام جدید (Items هنوز اقلام در حال حذف را هم دارد)
            InvoiceCalculator.Calculate(invoice, newItems);

            AddHistory(invoice, invoice.Status, invoice.Status, "ویرایش فاکتور");
            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // حذف
    // ============================================

    public Task DeleteAsync(int id, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Histories)
                .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId)
                ?? throw new InvalidOperationException("فاکتور پیدا نشد.");

            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("فقط فاکتور پیش‌نویس قابل حذف است.");

            // اگر این فاکتور از تبدیل یک پیش‌فاکتور ساخته شده، پیش‌فاکتور نباید «تبدیل‌شده» بماند
            if (invoice.ReferenceInvoiceId.HasValue)
            {
                var source = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.Id == invoice.ReferenceInvoiceId.Value);

                if (source is { Status: InvoiceStatus.Converted })
                {
                    source.Status = InvoiceStatus.Approved;
                    AddHistory(source, InvoiceStatus.Converted, InvoiceStatus.Approved,
                        $"فاکتور فروش شماره {invoice.Number} حذف شد؛ پیش‌فاکتور دوباره قابل تبدیل است.");
                }
            }

            // حذف واقعی را Interceptor به حذف نرم (UPDATE) تبدیل می‌کند
            _context.InvoiceItems.RemoveRange(invoice.Items.ToList());
            _context.InvoiceHistories.RemoveRange(invoice.Histories.ToList());
            _context.Invoices.Remove(invoice);

            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // قطعی
    // ============================================

    public Task PostAsync(int invoiceId, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var invoice = await GetInvoiceWithItemsAsync(invoiceId, tenantId);

            if (invoice.InvoiceType == InvoiceType.PreInvoice)
                throw new InvalidOperationException("پیش‌فاکتور قابل قطعی کردن نیست. ابتدا آن را به فاکتور تبدیل کنید.");

            if (invoice.Status == InvoiceStatus.Posted)
                throw new InvalidOperationException("فاکتور قبلاً قطعی شده است.");

            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("فقط فاکتور پیش‌نویس قابل قطعی شدن است.");

            if (!invoice.ContactId.HasValue)
                throw new InvalidOperationException("برای قطعی کردن، طرف حساب فاکتور الزامی است.");

            if (invoice.Items.Count == 0)
                throw new InvalidOperationException("فاکتور بدون قلم قابل قطعی شدن نیست.");

            if (invoice.TotalAmount <= 0)
                throw new InvalidOperationException("مبلغ فاکتور باید بیشتر از صفر باشد.");

            var transaction = await BuildInvoiceTransactionAsync(invoice, tenantId);
            await _transactionService.CreateAsync(transaction);

            invoice.TransactionId = transaction.Id;
            invoice.Status = InvoiceStatus.Posted;

            AddHistory(invoice, InvoiceStatus.Draft, InvoiceStatus.Posted, "قطعی شد");

            await _stockMovementService.CreateFromInvoiceAsync(invoice.Id, tenantId);
            await _context.SaveChangesAsync();
        });
    }

    public Task UnpostAsync(int invoiceId, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var invoice = await GetInvoiceWithItemsAsync(invoiceId, tenantId);

            if (invoice.Status != InvoiceStatus.Posted)
                throw new InvalidOperationException("فاکتور قطعی نیست.");

            // قبلاً می‌شد فاکتوری که پرداخت گرفته را به پیش‌نویس برگرداند و دفتر با فاکتور ناهمخوان می‌شد
            if (invoice.PaidAmount > 0)
                throw new InvalidOperationException(
                    "این فاکتور پرداخت دارد. ابتدا پرداخت‌های مرتبط را به پیش‌نویس برگردانید.");

            if (invoice.SettlementDiscount > 0)
                throw new InvalidOperationException("این فاکتور با تخفیف تسویه شده و قابل برگشت نیست.");

            // سند اصلی (TransactionId) + هر سند خودکار دیگرِ همین فاکتور (مثلاً یتیم‌های داده‌ی قدیمی)
            var documentIds = await _context.Transactions
                .Where(t => t.ReferenceType == DocumentReferenceType && t.ReferenceId == invoice.Id)
                .Select(t => t.Id)
                .ToListAsync();

            if (invoice.TransactionId.HasValue && !documentIds.Contains(invoice.TransactionId.Value))
            {
                var exists = await _context.Transactions.AnyAsync(t => t.Id == invoice.TransactionId.Value);
                if (exists) documentIds.Add(invoice.TransactionId.Value);
            }

            if (invoice.InvoiceType is InvoiceType.Sale or InvoiceType.Purchase)
            {
                await _stockMovementService.ReverseInvoiceAsync(
                    invoice.Id,
                    tenantId);
            }

            foreach (var documentId in documentIds)
                await _transactionService.DeleteAsync(documentId, tenantId);

            invoice.TransactionId = null;
            invoice.Status = InvoiceStatus.Draft;

            AddHistory(invoice, InvoiceStatus.Posted, InvoiceStatus.Draft, "برگشت به پیش‌نویس");
            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // گردش کار پیش‌فاکتور
    // ============================================

    public async Task MarkAsSentAsync(int invoiceId, int tenantId)
    {
        var invoice = await GetPreInvoiceAsync(invoiceId, tenantId, "فقط پیش‌فاکتور قابل ارسال است.");

        if (invoice.Status != InvoiceStatus.Draft &&
            invoice.Status != InvoiceStatus.Revised &&
            invoice.Status != InvoiceStatus.Postponed)
            throw new InvalidOperationException("فقط پیش‌فاکتور پیش‌نویس، اصلاح‌شده یا به‌تعویق‌افتاده قابل ارسال است.");

        var now = UtcNow();
        invoice.SentDate = now;
        invoice.LastSentDate = now;

        await ChangeStatusAsync(invoice, InvoiceStatus.Sent, "ارسال به مشتری");
    }

    public async Task MarkAsRevisedAsync(int invoiceId, int tenantId, string? description)
    {
        var invoice = await GetPreInvoiceAsync(invoiceId, tenantId, "فقط پیش‌فاکتور قابل اصلاح است.");

        if (invoice.Status != InvoiceStatus.Sent &&
            invoice.Status != InvoiceStatus.Rejected &&
            invoice.Status != InvoiceStatus.Postponed)
            throw new InvalidOperationException("وضعیت فعلی اجازه اصلاح نمی‌دهد.");

        invoice.RevisionCount++;

        await ChangeStatusAsync(invoice, InvoiceStatus.Revised, description ?? "اصلاح پیش‌فاکتور");
    }

    public async Task MarkAsApprovedAsync(int invoiceId, int tenantId, string? description)
    {
        var invoice = await GetPreInvoiceAsync(invoiceId, tenantId, "فقط پیش‌فاکتور قابل تایید است.");

        if (invoice.Status != InvoiceStatus.Sent && invoice.Status != InvoiceStatus.Revised)
            throw new InvalidOperationException("وضعیت فعلی اجازه تایید نمی‌دهد.");

        invoice.ApprovedDate = UtcNow();
        invoice.CustomerResponse = description;

        await ChangeStatusAsync(invoice, InvoiceStatus.Approved, description ?? "تایید مشتری");
    }

    public async Task MarkAsRejectedAsync(int invoiceId, int tenantId, string? reason)
    {
        var invoice = await GetPreInvoiceAsync(invoiceId, tenantId, "فقط پیش‌فاکتور قابل رد است.");

        if (invoice.Status != InvoiceStatus.Sent && invoice.Status != InvoiceStatus.Revised)
            throw new InvalidOperationException("وضعیت فعلی اجازه رد نمی‌دهد.");

        invoice.RejectedDate = UtcNow();
        invoice.CustomerResponse = reason;

        await ChangeStatusAsync(invoice, InvoiceStatus.Rejected, reason ?? "رد مشتری");
    }

    public async Task MarkAsPostponedAsync(int invoiceId, int tenantId, DateOnly nextFollowUpDate, string? reason)
    {
        var invoice = await GetPreInvoiceAsync(invoiceId, tenantId, "فقط پیش‌فاکتور قابل به تعویق است.");

        if (invoice.Status != InvoiceStatus.Sent && invoice.Status != InvoiceStatus.Revised)
            throw new InvalidOperationException("وضعیت فعلی اجازه به تعویق نمی‌دهد.");

        if (nextFollowUpDate == default)
            throw new InvalidOperationException("تاریخ پیگیری الزامی است.");

        invoice.NextFollowUpDate = nextFollowUpDate;
        invoice.CustomerResponse = reason;

        // تاریخ شمسی در متن تاریخچه (قبلاً میلادی نوشته می‌شد)
        await ChangeStatusAsync(invoice, InvoiceStatus.Postponed,
            $"به تعویق افتاد تا {PersianDate.Format(nextFollowUpDate)} - {reason}");
    }

    public async Task MarkAsCancelledAsync(int invoiceId, int tenantId, string? reason)
    {
        var invoice = await GetPreInvoiceAsync(invoiceId, tenantId, "فقط پیش‌فاکتور قابل لغو است.");

        if (invoice.Status == InvoiceStatus.Converted)
            throw new InvalidOperationException("پیش‌فاکتور تبدیل شده قابل لغو نیست.");

        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("پیش‌فاکتور قبلاً لغو شده است.");

        await ChangeStatusAsync(invoice, InvoiceStatus.Cancelled, reason ?? "لغو شد");
    }

    // ============================================
    // تبدیل پیش‌فاکتور به فاکتور
    // ============================================

    public Task<Invoice> ConvertToInvoiceAsync(int preInvoiceId, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var preInvoice = await GetInvoiceWithItemsAsync(preInvoiceId, tenantId);

            if (preInvoice.InvoiceType != InvoiceType.PreInvoice)
                throw new InvalidOperationException("این فاکتور، پیش‌فاکتور نیست.");

            if (preInvoice.Status == InvoiceStatus.Converted)
                throw new InvalidOperationException("این پیش‌فاکتور قبلاً تبدیل شده است.");

            if (preInvoice.Status != InvoiceStatus.Approved)
                throw new InvalidOperationException(
                    "فقط پیش‌فاکتور تایید شده قابل تبدیل به فاکتور است. ابتدا آن را تایید کنید.");

            // مبلغ‌ها عیناً کپی می‌شوند (همان مبلغی که مشتری تایید کرده)؛ مبلغ تخفیف و مالیات
            // هر قلم هم کپی می‌شود (قبلاً کپی نمی‌شد و در فاکتور صفر می‌ماند)
            var saleInvoice = new Invoice
            {
                TenantId = tenantId,
                Number = 0, // خودکار
                Date = _timeProvider.Today(),
                InvoiceType = InvoiceType.Sale,
                ContactId = preInvoice.ContactId,
                Status = InvoiceStatus.Draft,
                Description = preInvoice.Description,
                ReferenceInvoiceId = preInvoice.Id,
                SubTotal = preInvoice.SubTotal,
                DiscountAmount = preInvoice.DiscountAmount,
                TaxAmount = preInvoice.TaxAmount,
                TotalAmount = preInvoice.TotalAmount,
                Items = preInvoice.Items.Select(i => new InvoiceItem
                {
                    TenantId = tenantId,
                    ProductId = i.ProductId,
                    WarehouseId = i.WarehouseId,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    DiscountPercent = i.DiscountPercent,
                    DiscountAmount = i.DiscountAmount,
                    TaxRate = i.TaxRate,
                    TaxAmount = i.TaxAmount,
                    LineTotal = i.LineTotal
                }).ToList()
            };

            await SaveNewInvoiceAsync(saleInvoice, $"ایجاد از پیش‌فاکتور شماره {preInvoice.Number}");

            AddHistory(preInvoice, InvoiceStatus.Approved, InvoiceStatus.Converted,
                $"تبدیل به فاکتور فروش شماره {saleInvoice.Number}");
            preInvoice.Status = InvoiceStatus.Converted;

            await _context.SaveChangesAsync();

            return saleInvoice;
        });
    }

    // ============================================
    // تسویه با تخفیف
    // ============================================

    public Task SettleWithDiscountAsync(int invoiceId, int tenantId, decimal discountAmount)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var invoice = await GetInvoiceAsync(invoiceId, tenantId);

            if (invoice.InvoiceType == InvoiceType.PreInvoice)
                throw new InvalidOperationException("پیش‌فاکتور قابل تسویه نیست.");

            if (invoice.Status != InvoiceStatus.Posted)
                throw new InvalidOperationException("فقط فاکتور قطعی قابل تسویه است.");

            var remaining = InvoiceBalance.Remaining(invoice);

            if (remaining <= 0)
                throw new InvalidOperationException("این فاکتور قبلاً تسویه شده است.");

            if (discountAmount <= 0)
                throw new InvalidOperationException("مبلغ تخفیف باید بیشتر از صفر باشد.");

            if (decimal.Truncate(discountAmount) != discountAmount)
                throw new InvalidOperationException("مبلغ تخفیف باید عدد صحیح (ریال) باشد.");

            if (discountAmount > remaining)
                throw new InvalidOperationException(
                    $"مبلغ تخفیف ({discountAmount:N0}) بیشتر از باقی‌مانده ({remaining:N0}) است.");

            invoice.SettlementDiscount += discountAmount;

            // سند تخفیف جداگانه است؛ Invoice.TransactionId (سند اصلی) دست نمی‌خورد
            var transaction = await BuildSettlementTransactionAsync(invoice, discountAmount, tenantId);
            await _transactionService.CreateAsync(transaction);

            AddHistory(invoice, invoice.Status, invoice.Status, $"تسویه با تخفیف {discountAmount:N0} ریال");
            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // اعتبارسنجی درخواست
    // ============================================

    private static void ValidateRequest(InvoiceRequest request)
    {
        if (request.Date == default)
            throw new InvalidOperationException("تاریخ فاکتور الزامی است.");

        if (!Enum.IsDefined(request.InvoiceType))
            throw new InvalidOperationException("نوع فاکتور نامعتبر است.");

        if (!(request.ContactId is > 0) && string.IsNullOrWhiteSpace(request.NewContactName))
            throw new InvalidOperationException("لطفاً طرف حساب را انتخاب کنید.");

        if (request.Items.Count == 0)
            throw new InvalidOperationException("حداقل یک قلم با کالا و تعداد لازم است.");

        foreach (var item in request.Items)
        {
            if (item.ProductId <= 0 && string.IsNullOrWhiteSpace(item.NewProductName))
                throw new InvalidOperationException("برای هر قلم باید کالا انتخاب شود.");

            if (item.Quantity <= 0)
                throw new InvalidOperationException("تعداد باید بیشتر از صفر باشد.");

            if (item.UnitPrice < 0)
                throw new InvalidOperationException("قیمت واحد نمی‌تواند منفی باشد.");

            if (decimal.Truncate(item.UnitPrice) != item.UnitPrice)
                throw new InvalidOperationException("قیمت واحد باید عدد صحیح (ریال) باشد.");

            if (item.DiscountPercent < 0 || item.DiscountPercent > 100)
                throw new InvalidOperationException("درصد تخفیف باید بین ۰ تا ۱۰۰ باشد.");

            if (item.TaxRate < 0 || item.TaxRate > 100)
                throw new InvalidOperationException("نرخ مالیات باید بین ۰ تا ۱۰۰ باشد.");
        }

        var hasDuplicateProduct = request.Items
            .Where(i => i.ProductId > 0)
            .GroupBy(i => i.ProductId)
            .Any(g => g.Count() > 1);

        if (hasDuplicateProduct)
            throw new InvalidOperationException("کالاهای تکراری در فاکتور وجود دارد.");

        var hasDuplicateNewProduct = request.Items
            .Where(i => i.ProductId <= 0)
            .GroupBy(i => i.NewProductName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1);

        if (hasDuplicateNewProduct)
            throw new InvalidOperationException("کالاهای جدید تکراری در فاکتور وجود دارد.");
    }

    // ============================================
    // طرف حساب و کالا (شامل ساخت سریع)
    // ============================================

    private async Task<int> ResolveContactAsync(InvoiceRequest request, int tenantId)
    {
        if (request.ContactId is > 0)
        {
            var exists = await _context.Contacts.AnyAsync(c => c.Id == request.ContactId.Value);
            if (!exists)
                throw new InvalidOperationException("طرف حساب معتبر نیست.");

            return request.ContactId.Value;
        }

        var contact = new Contact
        {
            TenantId = tenantId,
            Name = request.NewContactName!.Trim(),
            ContactType = request.InvoiceType == InvoiceType.Purchase
                ? ContactType.Supplier
                : ContactType.Customer,
            IsActive = true
        };

        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();

        return contact.Id;
    }

    private async Task<List<InvoiceItem>> BuildItemsAsync(InvoiceRequest request, int tenantId)
    {
        var existingIds = request.Items
            .Where(i => i.ProductId > 0)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var existingProducts = new Dictionary<int, Product>();
        if (existingIds.Count > 0)
        {
            var products = await _context.Products
                .Where(p => p.TenantId == tenantId && existingIds.Contains(p.Id))
                .ToListAsync();
            if (products.Count != existingIds.Count)
                throw new InvalidOperationException("کالای انتخاب‌شده معتبر نیست.");

            existingProducts = products.ToDictionary(p => p.Id);
        }

        var warehouseIds = request.Items
            .Where(i => i.WarehouseId.HasValue &&
                (i.ProductId <= 0 || existingProducts[i.ProductId].TrackInventory))
            .Select(i => i.WarehouseId!.Value)
            .Distinct()
            .ToList();

        if (warehouseIds.Count > 0)
        {
            var validWarehouseCount = await _context.Warehouses.CountAsync(w =>
                w.TenantId == tenantId && w.IsActive && warehouseIds.Contains(w.Id));

            if (validWarehouseCount != warehouseIds.Count)
                throw new InvalidOperationException(
                    "یکی از انبارهای انتخاب‌شده وجود ندارد، غیرفعال است یا متعلق به شرکت جاری نیست.");
        }

        Unit? defaultUnit = null;
        var items = new List<InvoiceItem>();

        foreach (var item in request.Items)
        {
            var productId = item.ProductId;
            var trackInventory = productId <= 0 || existingProducts[productId].TrackInventory;

            if (productId <= 0)
            {
                defaultUnit ??= await GetOrCreateDefaultUnitAsync(tenantId);

                var product = new Product
                {
                    TenantId = tenantId,
                    Name = item.NewProductName!.Trim(),
                    UnitId = defaultUnit.Id,
                    SalePrice = 0,
                    LastPurchasePrice = 0,
                    IsActive = true
                };
                if (request.InvoiceType == InvoiceType.Purchase)
                    product.LastPurchasePrice = item.UnitPrice;
                else
                    product.SalePrice = item.UnitPrice;

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                productId = product.Id;
            }
            else if (request.InvoiceType == InvoiceType.Purchase)
            {
                var product = existingProducts[productId];
                product.LastPurchasePrice = item.UnitPrice;
                product.LastPriceUpdatedAt = DateTime.UtcNow;
            }

            items.Add(new InvoiceItem
            {
                TenantId = tenantId,
                ProductId = productId,
                WarehouseId = trackInventory ? item.WarehouseId : null,
                Description = item.Description?.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent,
                TaxRate = item.TaxRate
            });
        }

        return items;
    }

    private async Task<Unit> GetOrCreateDefaultUnitAsync(int tenantId)
    {
        var unit = await _context.Units
                       .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Name == "عدد")
                   ?? await _context.Units
                       .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.IsActive);

        if (unit != null) return unit;

        unit = new Unit
        {
            TenantId = tenantId,
            Name = "عدد",
            Symbol = "عدد",
            SortOrder = 1,
            IsActive = true
        };

        _context.Units.Add(unit);
        await _context.SaveChangesAsync();

        return unit;
    }

    // ============================================
    // ذخیره‌ی فاکتور جدید با شماره‌گذاری خودکار
    // ============================================

    private async Task SaveNewInvoiceAsync(Invoice invoice, string historyDescription)
    {
        var autoNumber = invoice.Number == 0;

        for (var attempt = 1; ; attempt++)
        {
            if (autoNumber)
                invoice.Number = await GetNextNumberAsync(invoice.TenantId, invoice.InvoiceType);

            _context.Invoices.Add(invoice);

            try
            {
                await _context.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (autoNumber && attempt < MaxNumberingAttempts && ex.IsDuplicateKey())
            {
                DetachNew(invoice);
            }
        }

        AddHistory(invoice, null, invoice.Status, historyDescription);
        await _context.SaveChangesAsync();
    }

    private void DetachNew(Invoice invoice)
    {
        foreach (var item in invoice.Items)
            _context.Entry(item).State = EntityState.Detached;

        _context.Entry(invoice).State = EntityState.Detached;
    }

    // ============================================
    // سندهای حسابداری
    // ============================================

    private async Task<Transaction> BuildInvoiceTransactionAsync(Invoice invoice, int tenantId)
    {
        var typeName = invoice.InvoiceType == InvoiceType.Sale ? "فروش" : "خرید";
        var contactName = await GetContactNameAsync(invoice.ContactId);
        var partyLabel = invoice.InvoiceType == InvoiceType.Sale ? "مشتری" : "فروشنده";

        var description = $"سند خودکار فاکتور {typeName} شماره {invoice.Number}";
        var lineDescription = $"فاکتور {typeName} شماره {invoice.Number}";

        if (!string.IsNullOrEmpty(contactName))
        {
            description += $" - {partyLabel}: {contactName}";
            lineDescription += $" - {partyLabel}: {contactName}";
        }

        var lines = new List<TransactionLine>();

        if (invoice.InvoiceType == InvoiceType.Sale)
        {
            var receivable = await _systemAccounts.GetOrCreateAsync(tenantId, "دریافتنی", AccountType.Receivable);
            var sales = await _systemAccounts.GetOrCreateAsync(tenantId, "درآمد فروش", AccountType.Income);

            lines.Add(new TransactionLine
            {
                AccountId = receivable.Id,
                ContactId = invoice.ContactId,
                Debit = invoice.TotalAmount,
                Description = lineDescription
            });
            lines.Add(new TransactionLine
            {
                AccountId = sales.Id,
                Credit = invoice.TotalAmount,
                Description = lineDescription
            });
        }
        else
        {
            var purchases = await _systemAccounts.GetOrCreateAsync(tenantId, "خرید کالا", AccountType.Expense);
            var payable = await _systemAccounts.GetOrCreateAsync(tenantId, "پرداختنی", AccountType.Payable);

            lines.Add(new TransactionLine
            {
                AccountId = purchases.Id,
                Debit = invoice.TotalAmount,
                Description = lineDescription
            });
            lines.Add(new TransactionLine
            {
                AccountId = payable.Id,
                ContactId = invoice.ContactId,
                Credit = invoice.TotalAmount,
                Description = lineDescription
            });
        }

        return new Transaction
        {
            TenantId = tenantId,
            Number = 0, // شماره‌گذاری خودکار در TransactionService
            Date = invoice.Date,
            Description = description,
            Reference = invoice.Number.ToString(),
            IsPosted = true,
            ReferenceType = DocumentReferenceType,
            ReferenceId = invoice.Id,
            Lines = lines
        };
    }

    private async Task<Transaction> BuildSettlementTransactionAsync(Invoice invoice, decimal discountAmount, int tenantId)
    {
        var typeName = invoice.InvoiceType == InvoiceType.Sale ? "فروش" : "خرید";
        var contactName = await GetContactNameAsync(invoice.ContactId);

        var description = $"تسویه با تخفیف فاکتور {typeName} شماره {invoice.Number}";
        if (!string.IsNullOrEmpty(contactName))
            description += $" - {contactName}";

        var lines = new List<TransactionLine>();

        if (invoice.InvoiceType == InvoiceType.Sale)
        {
            // فروش: تخفیف فروش (بدهکار) / دریافتنی (بستانکار)
            var discount = await _systemAccounts.GetOrCreateAsync(tenantId, "تخفیف فروش", AccountType.Expense);
            var receivable = await _systemAccounts.GetOrCreateAsync(tenantId, "دریافتنی", AccountType.Receivable);

            lines.Add(new TransactionLine { AccountId = discount.Id, Debit = discountAmount, Description = description });
            lines.Add(new TransactionLine
            {
                AccountId = receivable.Id,
                ContactId = invoice.ContactId,
                Credit = discountAmount,
                Description = description
            });
        }
        else
        {
            // خرید: پرداختنی (بدهکار) / تخفیف خرید (بستانکار)
            var discount = await _systemAccounts.GetOrCreateAsync(tenantId, "تخفیف خرید", AccountType.Income);
            var payable = await _systemAccounts.GetOrCreateAsync(tenantId, "پرداختنی", AccountType.Payable);

            lines.Add(new TransactionLine
            {
                AccountId = payable.Id,
                ContactId = invoice.ContactId,
                Debit = discountAmount,
                Description = description
            });
            lines.Add(new TransactionLine { AccountId = discount.Id, Credit = discountAmount, Description = description });
        }

        return new Transaction
        {
            TenantId = tenantId,
            Number = 0,
            Date = _timeProvider.Today(),
            Description = description,
            Reference = invoice.Number.ToString(),
            IsPosted = true,
            ReferenceType = DocumentReferenceType,
            ReferenceId = invoice.Id,
            Lines = lines
        };
    }

    private Task<string?> GetContactNameAsync(int? contactId)
    {
        if (!contactId.HasValue) return Task.FromResult<string?>(null);

        return _context.Contacts
            .Where(c => c.Id == contactId.Value)
            .Select(c => (string?)c.Name)
            .FirstOrDefaultAsync();
    }

    // ============================================
    // Helpers
    // ============================================

    private async Task<Invoice> GetInvoiceAsync(int invoiceId, int tenantId)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId);

        return invoice ?? throw new InvalidOperationException("فاکتور پیدا نشد.");
    }

    private async Task<Invoice> GetInvoiceWithItemsAsync(int invoiceId, int tenantId)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId);

        return invoice ?? throw new InvalidOperationException("فاکتور پیدا نشد.");
    }

    private async Task<Invoice> GetPreInvoiceAsync(int invoiceId, int tenantId, string notPreInvoiceMessage)
    {
        var invoice = await GetInvoiceAsync(invoiceId, tenantId);

        if (invoice.InvoiceType != InvoiceType.PreInvoice)
            throw new InvalidOperationException(notPreInvoiceMessage);

        return invoice;
    }

    /// <summary>تغییر وضعیت + تاریخچه + ذخیره (فاکتور و تاریخچه با یک SaveChanges، پس اتمیک است).</summary>
    private async Task ChangeStatusAsync(Invoice invoice, InvoiceStatus newStatus, string description)
    {
        var oldStatus = invoice.Status;
        invoice.Status = newStatus;

        AddHistory(invoice, oldStatus, newStatus, description);
        await _context.SaveChangesAsync();
    }

    private void AddHistory(Invoice invoice, InvoiceStatus? from, InvoiceStatus to, string? description)
    {
        _context.InvoiceHistories.Add(new InvoiceHistory
        {
            TenantId = invoice.TenantId,
            InvoiceId = invoice.Id,
            FromStatus = from,
            ToStatus = to,
            Description = description,
            Date = UtcNow(),
            UserId = _currentUser.UserId
        });
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
}
