/*
    گزارش معین یک حساب.

    @OpeningFromDate:
        NULL  ← مانده‌ی اول دوره = جمع همه‌ی خطوط قبل از @FromDate (حساب‌های دائمی: بانک، دریافتنی، ...)
        تاریخ ← فقط خطوطِ از این تاریخ تا قبل از @FromDate (حساب‌های موقت درآمد/هزینه؛
                 تاریخ = ابتدای سال مالی، چون این حساب‌ها در شروع سال مالی صفر می‌شوند)
        سرویس (ReportOpeningPolicy) این مقدار را تعیین می‌کند؛ SQL به نوع حساب وابسته نیست.

    خروجی:
        ۱) یک ردیف خلاصه: OpeningBalance, TotalDebit, TotalCredit, LineCount
        ۲) خطوط به ترتیب تاریخ/شماره سند/ردیف، با مانده‌ی تجمعی (RunningBalance)

    فقط اسناد قطعی (IsPosted = 1) و حذف‌نشده؛ فقط شرکت داده‌شده.
*/
CREATE OR ALTER PROCEDURE dbo.usp_Report_Ledger
    @TenantId        INT,
    @AccountId       INT,
    @FromDate        DATE,
    @ToDate          DATE,
    @OpeningFromDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Opening DECIMAL(19, 0);

    SELECT @Opening = ISNULL(SUM(l.Debit - l.Credit), 0)
    FROM dbo.TransactionLines AS l
    INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
    WHERE l.TenantId = @TenantId
      AND t.TenantId = @TenantId
      AND l.AccountId = @AccountId
      AND l.IsDeleted = 0
      AND t.IsDeleted = 0
      AND t.IsPosted = 1
      AND t.[Date] < @FromDate
      AND (@OpeningFromDate IS NULL OR t.[Date] >= @OpeningFromDate);

    SELECT
        t.Id                                         AS TransactionId,
        t.Number                                     AS TransactionNumber,
        t.[Date]                                     AS [Date],
        COALESCE(l.Description, t.Description)       AS [Description],
        t.Reference                                  AS Reference,
        l.Debit                                      AS Debit,
        l.Credit                                     AS Credit,
        @Opening + SUM(l.Debit - l.Credit) OVER (
            ORDER BY t.[Date], t.Number, l.LineNumber, l.Id
            ROWS UNBOUNDED PRECEDING)                AS RunningBalance,
        l.LineNumber                                 AS LineNumber,
        l.Id                                         AS LineId
    INTO #Lines
    FROM dbo.TransactionLines AS l
    INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
    WHERE l.TenantId = @TenantId
      AND t.TenantId = @TenantId
      AND l.AccountId = @AccountId
      AND l.IsDeleted = 0
      AND t.IsDeleted = 0
      AND t.IsPosted = 1
      AND t.[Date] >= @FromDate
      AND t.[Date] <= @ToDate;

    -- ۱) خلاصه
    SELECT
        @Opening                  AS OpeningBalance,
        ISNULL(SUM(Debit), 0)     AS TotalDebit,
        ISNULL(SUM(Credit), 0)    AS TotalCredit,
        COUNT(*)                  AS LineCount
    FROM #Lines;

    -- ۲) خطوط
    SELECT TransactionId, TransactionNumber, [Date], [Description], Reference, Debit, Credit, RunningBalance
    FROM #Lines
    ORDER BY [Date], TransactionNumber, LineNumber, LineId;
END
