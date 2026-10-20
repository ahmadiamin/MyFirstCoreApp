using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCoreApp.Infrastructure.Services;

internal static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// تداخل روی ایندکس یکتا (2601) یا کلید یکتا (2627) در SQL Server.
    /// برای شماره‌گذاری خودکار: اگر دو کاربر هم‌زمان شماره‌ی یکسان بگیرند، یکی دوباره تلاش می‌کند.
    /// </summary>
    public static bool IsDuplicateKey(this DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
