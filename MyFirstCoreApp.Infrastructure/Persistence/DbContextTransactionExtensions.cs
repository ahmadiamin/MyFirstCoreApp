using Microsoft.EntityFrameworkCore;
using System.Data;

namespace MyFirstCoreApp.Infrastructure.Persistence;

public static class DbContextTransactionExtensions
{
    public static async Task<T> ExecuteInTransactionAsync<T>(
        this DbContext context,
        IsolationLevel isolationLevel,
        Func<Task<T>> action)
    {
        // تراکنش بیرونی وجود دارد؛ همان را استفاده کن.
        if (context.Database.CurrentTransaction != null)
            return await action();

        await using var transaction =
            await context.Database.BeginTransactionAsync(isolationLevel);

        try
        {
            var result = await action();

            await transaction.CommitAsync();

            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public static async Task ExecuteInTransactionAsync(
        this DbContext context,
        IsolationLevel isolationLevel,
        Func<Task> action)
    {
        // تراکنش بیرونی وجود دارد؛ همان را استفاده کن.
        if (context.Database.CurrentTransaction != null)
        {
            await action();
            return;
        }

        await using var transaction =
            await context.Database.BeginTransactionAsync(isolationLevel);

        try
        {
            await action();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}