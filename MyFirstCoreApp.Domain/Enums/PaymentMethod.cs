namespace MyFirstCoreApp.Domain.Enums
{
    public enum PaymentMethod
    {
        Cash = 1,           // نقد
        BankTransfer = 2,   // انتقال بانکی
        Card = 3,           // کارت به کارت
        Cheque = 4,         // چک (از چک‌های قبلی)
        Pos = 5,            // کارتخوان
        Internet = 6,       // اینترنتی
        Other = 99
    }
}