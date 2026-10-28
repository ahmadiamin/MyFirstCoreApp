using System.Reflection;

namespace MyFirstCoreApp.Infrastructure.Persistence.Sql;

/// <summary>
/// اسکریپت‌های Stored Procedure گزارش‌ها و داشبورد که به‌صورت EmbeddedResource در اسمبلی هستند.
/// Migration (و تست‌های SQL Server) از همین‌جا می‌خوانند؛ پس فایل‌های .sql تنها منبع حقیقت‌اند.
///
/// هر اسکریپت با CREATE OR ALTER نوشته شده؛ برای تغییر یک پروسیجر در آینده، فایل .sql را
/// اصلاح کن و یک Migration خالی جدید بساز که Read(...) همان را دوباره اجرا کند.
/// </summary>
public static class ReportSqlScripts
{
    public static readonly string[] ProcedureNames =
    {
        "usp_Report_Ledger",
        "usp_Report_Detailed",
        "usp_Report_Contact",
        "usp_Report_Invoices",
        "usp_Report_ContactBalances",
        "usp_Dashboard_Summary"
    };

    public static string Read(string procedureName)
    {
        var assembly = typeof(ReportSqlScripts).Assembly;
        var suffix = $".{procedureName}.sql";

        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"اسکریپت '{procedureName}.sql' به‌عنوان EmbeddedResource پیدا نشد. " +
                "در فایل .csproj پروژه‌ی Infrastructure باید <EmbeddedResource Include=\"Persistence\\Sql\\**\\*.sql\" /> باشد.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}