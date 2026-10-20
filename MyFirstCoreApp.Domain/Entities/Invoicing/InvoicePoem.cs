using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Invoicing
{
    public class InvoicePoem: TenantEntity
    {
        public string Text { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}