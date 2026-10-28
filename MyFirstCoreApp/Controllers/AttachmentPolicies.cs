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
        AttachmentEntityType.Transaction => ["Transaction.View", "Loan.View"],
        AttachmentEntityType.Cheque => ["Cheque.View"],
        AttachmentEntityType.Payment => ["Payment.View"],
        AttachmentEntityType.Invoice => ["Invoice.View"],
        AttachmentEntityType.Product => ["Product.View"],
        AttachmentEntityType.Contact => ["Contact.View"],
        _ => []
    };

    public static string[] Edit(AttachmentEntityType type) => type switch
    {
        AttachmentEntityType.Transaction => ["Transaction.Edit", "Loan.Edit", "Loan.Create"],
        AttachmentEntityType.Cheque => ["Cheque.Edit"],
        AttachmentEntityType.Payment => ["Payment.Edit"],
        AttachmentEntityType.Invoice => ["Invoice.Edit"],
        AttachmentEntityType.Product => ["Product.Edit"],
        AttachmentEntityType.Contact => ["Contact.Edit"],
        _ => []
    };
}