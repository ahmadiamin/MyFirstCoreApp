using System.Data;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCoreApp.Infrastructure.Services;

internal static class DbContextTransactionExtensions
{
    private const string ConcurrencyMessage =
        "اطلاعات هم‌زمان توسط کاربر دیگری تغییر کرد. لطفاً صفحه را تازه کنید و دوباره تلاش کنید.";

    public static Task ExecuteInTransactionAsync(
        this DbContext context,
        Func<Task> action)
    {
        return context.ExecuteInTransactionAsync(
            IsolationLevel.ReadCommitted,
            action);
    }

    public static async Task ExecuteInTransactionAsync(this DbContext context, IsolationLevel isolationLevel, Func<Task> action)
    {
        await context.ExecuteInTransactionAsync<object?>(isolationLevel,
            async () =>
            {
                await action();
                return null;
            });
    }

    public static Task<T> ExecuteInTransactionAsync<T>(
        this DbContext context,
        Func<Task<T>> action)
    {
        return context.ExecuteInTransactionAsync(
            IsolationLevel.ReadCommitted,
            action);
    }

    public static async Task<T> ExecuteInTransactionAsync<T>(this DbContext context, IsolationLevel isolationLevel, Func<Task<T>> action)
    {
        // تراکنش بیرونی وجود دارد؛ از همان استفاده می‌کنیم.
        if (context.Database.CurrentTransaction != null)
            return await action();

        await using var transaction = await context.Database.BeginTransactionAsync(isolationLevel);

        try
        {
            var result = await action();
            await transaction.CommitAsync();
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException(ConcurrencyMessage);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}