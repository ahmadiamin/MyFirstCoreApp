using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class ProductCodeGenerator : IProductCodeGenerator
{
    private readonly ApplicationDbContext _context;

    public ProductCodeGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<string> GenerateNextCodeAsync(int tenantId)
    {
        return _context.ExecuteInTransactionAsync(
            IsolationLevel.Serializable,
            async () =>
            {
                // آخرین کد ثبت‌شده برای این شرکت
                // کالاهای حذف‌شده نیز در نظر گرفته می‌شوند.
                var lastCode = await _context.Products
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(x =>
                        x.TenantId == tenantId &&
                        x.Code != null &&
                        x.Code != "")
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Select(x => x.Code)
                    .FirstOrDefaultAsync();

                var candidate = GetNextCode(lastCode);

                // جلوگیری از انتخاب کد تکراری
                while (await _context.Products
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.TenantId == tenantId &&
                        x.Code == candidate))
                {
                    candidate = GetNextCode(candidate);
                }

                return candidate;
            });
    }

    private static string GetNextCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "1";

        code = code.Trim();

        // پیدا کردن تعداد ارقام انتهایی کد
        var digitCount = 0;

        for (var i = code.Length - 1; i >= 0; i--)
        {
            if (code[i] < '0' || code[i] > '9')
                break;

            digitCount++;
        }

        // اگر کد انتهای عددی نداشته باشد، عدد 1 اضافه می‌کنیم.
        if (digitCount == 0)
        {
            if (code.Length >= 50)
                throw new InvalidOperationException(
                    "امکان تولید کد جدید بر اساس آخرین کد کالا وجود ندارد.");

            return code + "1";
        }

        var prefix = code[..^digitCount];
        var numberText = code[^digitCount..];

        if (!long.TryParse(
                numberText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number))
        {
            throw new InvalidOperationException(
                "قسمت عددی آخرین کد کالا معتبر نیست.");
        }

        if (number == long.MaxValue)
        {
            throw new InvalidOperationException(
                "شماره کد کالا به حداکثر مقدار مجاز رسیده است.");
        }

        number++;

        var nextNumber = number.ToString(
            $"D{digitCount}",
            CultureInfo.InvariantCulture);

        var result = prefix + nextNumber;

        if (result.Length > 50)
        {
            throw new InvalidOperationException(
                "طول کد تولیدشده بیشتر از ۵۰ کاراکتر است.");
        }

        return result;
    }
}