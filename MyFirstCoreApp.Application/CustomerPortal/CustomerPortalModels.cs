using MyFirstCoreApp.Application.Reports;

namespace MyFirstCoreApp.Application.CustomerPortal;

public sealed class CustomerPortalShareListItem
{
    public int Id { get; init; }
    public int ContactId { get; init; }
    public string ContactName { get; init; } = string.Empty;
    public string? Token { get; set; }
    public string? LastSmsRequestId { get; init; }
    public DateTime? LastSmsSentAtUtc { get; init; }
    public bool CanViewLedger { get; init; }
    public bool CanViewSalesInvoices { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
}

public sealed class CustomerPortalShareUpdate
{
    public bool CanViewLedger { get; init; }
    public bool CanViewSalesInvoices { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
}

public sealed class CustomerPortalCreatedShare
{
    public int Id { get; init; }
    public string Token { get; init; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; init; }
}

public sealed class CustomerPortalInvoiceRow
{
    public int Id { get; init; }
    public int Number { get; init; }
    public DateOnly Date { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount { get; init; }
}

public sealed class CustomerPortalInvoiceItem
{
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class CustomerPortalInvoiceDetails
{
    public CustomerPortalInvoiceRow Invoice { get; init; } = new();
    public string? Description { get; init; }
    public IReadOnlyList<CustomerPortalInvoiceItem> Items { get; init; } = [];
}

public sealed class CustomerPortalPage
{
    public string CustomerName { get; init; } = string.Empty;
    public string? TenantName { get; init; }
    public bool CanViewLedger { get; init; }
    public bool CanViewSalesInvoices { get; init; }
    public ContactReport? Ledger { get; init; }
    public IReadOnlyList<CustomerPortalInvoiceRow> Invoices { get; init; } = [];
    public int InvoicePage { get; init; } = 1;
    public int InvoicePageCount { get; init; } = 1;
}

public sealed class CustomerPortalSmsStatusViewModel
{
    public int ContactId { get; init; }
    public int ShareId { get; init; }
    public string ContactName { get; init; } = string.Empty;
    public FarazSmsDeliveryReport Report { get; init; } = new(string.Empty, false, "{}", string.Empty);
}
