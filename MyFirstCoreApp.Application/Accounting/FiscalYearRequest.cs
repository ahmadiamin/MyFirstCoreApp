namespace MyFirstCoreApp.Application.Accounting;

public sealed class FiscalYearRequest
{
    /// <summary>اختیاری؛ خالی یعنی «سال مالی {سال شمسی شروع}».</summary>
    public string? Name { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
