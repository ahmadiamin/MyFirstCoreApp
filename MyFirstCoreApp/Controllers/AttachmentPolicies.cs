using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Controllers;

/// <summary>
/// پیوست «دسترسی مستقل ندارد»؛ هر کس موجودیتِ مالک را ببیند/ویرایش کند، پیوست‌هایش را هم می‌بیند/ویرایش می‌کند.
/// برای سند (Transaction) دسترسی «قرض» هم کافی است، چون رسید قرض به سند قرض پیوست می‌شود.
/// </summary>
public static class AttachmentPolicies
{
    public static string[] View(AttachmentEntityType type) => type switch
    {
        AttachmentEntityType.Transaction => new[] { "Transaction.View", "Loan.View" },
        AttachmentEntityType.Cheque => new[] { "Cheque.View" },
        AttachmentEntityType.Payment => new[] { "Payment.View" },
        AttachmentEntityType.Invoice => new[] { "Invoice.View" },
        AttachmentEntityType.Product => new[] { "Product.View" },
        AttachmentEntityType.Contact => new[] { "Contact.View" },
        _ => Array.Empty<string>()
    };

    public static string[] Edit(AttachmentEntityType type) => type switch
    {
        AttachmentEntityType.Transaction => new[] { "Transaction.Edit", "Loan.Edit", "Loan.Create" },
        AttachmentEntityType.Cheque => new[] { "Cheque.Edit" },
        AttachmentEntityType.Payment => new[] { "Payment.Edit" },
        AttachmentEntityType.Invoice => new[] { "Invoice.Edit" },
        AttachmentEntityType.Product => new[] { "Product.Edit" },
        AttachmentEntityType.Contact => new[] { "Contact.Edit" },
        _ => Array.Empty<string>()
    };

    internal static string[] Edit(object entityType)
    {
        throw new NotImplementedException();
    }
}
