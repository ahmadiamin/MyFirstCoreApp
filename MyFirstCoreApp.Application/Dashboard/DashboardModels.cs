using MyFirstCoreApp.Application.Common.Utilities;

namespace MyFirstCoreApp.Application.Dashboard;

public sealed class DueChequeDto
{
    public int Id { get; set; }
    public string ChequeNumber { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }

    /// <summary>روز مانده تا سررسید؛ منفی = گذشته، 0 = امروز.</summary>
    public int DaysLeft { get; set; }

    public bool IsOverdue => DaysLeft < 0;
}

public sealed class ContactBalanceDto
{
    public int ContactId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>بدهکار − بستانکار. بدهکاران برتر مثبت و بستانکاران برتر منفی‌اند.</summary>
    public decimal Balance { get; set; }
}

public sealed class MonthBucket
{
    public string MonthName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;   // 1405/01
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }

    public decimal Sales { get; set; }
    public decimal Purchases { get; set; }
    public decimal Received { get; set; }
    public decimal Paid { get; set; }
}

public sealed class DashboardSummary
{
    // بازه‌ی سال مالی که sales/purchases و نمودار روی آن حساب شده
    public DateOnly FiscalFrom { get; set; }
    public DateOnly FiscalTo { get; set; }
    public DateOnly Today { get; set; }
    public DateOnly DueUntil { get; set; }

    public decimal SalesTotal { get; set; }
    public decimal PurchasesTotal { get; set; }

    public int OpenSaleCount { get; set; }
    public decimal OpenSaleRemaining { get; set; }
    public int OpenPurchaseCount { get; set; }
    public decimal OpenPurchaseRemaining { get; set; }

    public decimal CashAndBankBalance { get; set; }
    public decimal ReceivableBalance { get; set; }
    public decimal PayableBalance { get; set; }

    public int PendingReceivedChequeCount { get; set; }
    public decimal PendingReceivedChequeAmount { get; set; }
    public int PendingIssuedChequeCount { get; set; }
    public decimal PendingIssuedChequeAmount { get; set; }

    public List<DueChequeDto> DueReceivedCheques { get; set; } = new();
    public List<DueChequeDto> DueIssuedCheques { get; set; } = new();
    public List<ContactBalanceDto> TopDebtors { get; set; } = new();
    public List<ContactBalanceDto> TopCreditors { get; set; } = new();

    public List<MonthBucket> Months { get; set; } = new();

    /// <summary>بزرگ‌ترین مقدارِ ماهانه؛ برای مقیاس نمودار.</summary>
    public decimal MaxMonthlyValue => Months.Count == 0
        ? 0
        : Months.Max(m => Math.Max(Math.Max(m.Sales, m.Purchases), Math.Max(m.Received, m.Paid)));
}

/// <summary>یک ردیف جریان روزانه (خروجی پروسیجر).</summary>
public readonly record struct DailyFlow(DateOnly Date, decimal Sales, decimal Purchases, decimal Received, decimal Paid);

/// <summary>
/// تقسیم جریان روزانه به ماه‌های شمسی (SQL Server تقویم شمسی ندارد، پس در C# انجام می‌شود).
/// تابع خالص و بدون دیتابیس؛ قابل تست مستقیم.
/// </summary>
public static class DashboardMonthBuckets
{
    private static readonly string[] MonthNames =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };

    /// <summary>
    /// ماه‌های شمسی که بازه‌ی [from, to] را می‌پوشانند (سال مالی ۱۲ ماهه ← ۱۲ ماه؛
    /// سالی که وسط ماه شروع می‌شود ← ۱۳ ماه که اولی و آخری نیمه‌اند).
    /// </summary>
    public static List<MonthBucket> Build(DateOnly from, DateOnly to)
    {
        var buckets = new List<MonthBucket>();
        if (from == default || to < from) return buckets;

        var cursor = PersianDate.StartOfMonth(from);

        while (cursor <= to)
        {
            var end = PersianDate.EndOfMonth(cursor);
            var (year, month, _) = PersianDate.ToParts(cursor);

            buckets.Add(new MonthBucket
            {
                Start = cursor,
                End = end,
                MonthName = MonthNames[month - 1],
                Label = $"{year}/{month:00}"
            });

            cursor = end.AddDays(1);
        }

        return buckets;
    }

    /// <summary>جمع هر روز را به ماه شامل آن اضافه می‌کند؛ روزِ بیرون از همه‌ی ماه‌ها نادیده گرفته می‌شود.</summary>
    public static void Fill(List<MonthBucket> buckets, IEnumerable<DailyFlow> rows)
    {
        foreach (var row in rows)
        {
            var bucket = buckets.FirstOrDefault(b => row.Date >= b.Start && row.Date <= b.End);
            if (bucket == null) continue;

            bucket.Sales += row.Sales;
            bucket.Purchases += row.Purchases;
            bucket.Received += row.Received;
            bucket.Paid += row.Paid;
        }
    }
}
