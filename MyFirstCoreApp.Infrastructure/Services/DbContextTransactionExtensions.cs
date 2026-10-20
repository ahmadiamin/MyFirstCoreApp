using Microsoft.EntityFrameworkCore;

namespace MyFirstCoreApp.Infrastructure.Services;

internal static class DbContextTransactionExtensions
{
    private const string ConcurrencyMessage =
        "اطلاعات هم‌زمان توسط کاربر دیگری تغییر کرد. لطفاً صفحه را تازه کنید و دوباره تلاش کنید.";

    /// <summary>
    /// عملیات را داخل «یک تراکنش دیتابیس» اجرا می‌کند: یا همه‌ی SaveChangesها اعمال می‌شود یا هیچ‌کدام.
    /// اگر تراکنشی از قبل باز باشد (مثلاً سرویسی سرویس دیگر را صدا بزند) به همان می‌پیوندد.
    /// تداخل هم‌زمانی (DbUpdateConcurrencyException) به پیام فارسی تبدیل می‌شود.
    /// </summary>
    public static async Task ExecuteInTransactionAsync(this DbContext context, Func<Task> action)
    {
        await context.ExecuteInTransactionAsync<object?>(async () =>
        {
            await action();
            return null;
        });
    }

    public static async Task<T> ExecuteInTransactionAsync<T>(this DbContext context, Func<Task<T>> action)
    {
        try
        {
            if (context.Database.CurrentTransaction != null)
                return await action();

            await using var transaction = await context.Database.BeginTransactionAsync();

            var result = await action();
            await transaction.CommitAsync();

            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(ConcurrencyMessage);
        }
    }
}
