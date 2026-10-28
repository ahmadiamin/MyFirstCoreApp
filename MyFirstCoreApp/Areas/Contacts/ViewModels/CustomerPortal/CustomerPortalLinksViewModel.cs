using MyFirstCoreApp.Application.CustomerPortal;

namespace MyFirstCoreApp.Areas.Contacts.ViewModels.CustomerPortal;

public sealed class CustomerPortalLinksViewModel
{
    public int ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public bool CanViewLedger { get; set; } = true;
    public bool CanViewSalesInvoices { get; set; } = true;
    public string? ExpiresOn { get; set; }
    public string? CreatedUrl { get; set; }
    public string? SmsNotice { get; set; }
    public IReadOnlyList<CustomerPortalShareListItem> Links { get; set; } = [];
}

public sealed class CustomerPortalShareEditViewModel
{
    public int ContactId { get; set; }
    public int Id { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public bool CanViewLedger { get; set; }
    public bool CanViewSalesInvoices { get; set; }
    public string? ExpiresOn { get; set; }
    public string? PortalUrl { get; set; }
}
