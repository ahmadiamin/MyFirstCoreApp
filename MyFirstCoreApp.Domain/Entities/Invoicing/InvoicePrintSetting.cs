using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Invoicing
{
    public class InvoicePrintSetting: TenantEntity
    {
        // نمایش مانده حساب در چاپ فاکتور
        public bool ShowBalance { get; set; } = true;

        // روش انتخاب شعر
        public InvoicePoemSelectionMode PoemSelectionMode { get; set; } = InvoicePoemSelectionMode.Random;

        // شعر انتخاب‌شده در حالت Selected
        public int? SelectedInvoicePoemId { get; set; }
        //توضیحات پایین فاکتور
        public string Description { get; set; } = string.Empty;

        public InvoicePoem? SelectedInvoicePoem { get; set; }
    }
}
