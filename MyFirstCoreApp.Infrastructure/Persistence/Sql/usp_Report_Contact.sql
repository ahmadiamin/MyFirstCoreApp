/*
    صورت‌حساب یک طرف حساب (همه‌ی خطوطی که به او وصل است، روی هر حسابی).

    مانده‌ی اول دوره همیشه تجمعی است (مانده‌ی طرف حساب با تغییر سال مالی صفر نمی‌شود).
    علامت مانده: بدهکار − بستانکار؛ مثبت = طرف حساب به ما بدهکار است.
*/
CREATE OR ALTER PROCEDURE dbo.usp_Report_Contact
    @TenantId   INT,
    @ContactId  INT,
    @FromDate   DATE,
    @ToDate     DATE,
    @OnlyPosted BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Opening DECIMAL(19, 0);

    SELECT @Opening = ISNULL(SUM(l.Debit - l.Credit), 0)
    FROM dbo.TransactionLines AS l
    INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
    WHERE l.TenantId = @TenantId
      AND t.TenantId = @TenantId
      AND l.ContactId = @ContactId
      AND l.IsDeleted = 0
      AND t.IsDeleted = 0
      AND (@OnlyPosted = 0 OR t.IsPosted = 1)
      AND t.[Date] < @FromDate;

    SELECT
        t.Id                                         AS TransactionId,
        t.Number                                     AS TransactionNumber,
        t.[Date]                                     AS [Date],
        COALESCE(l.Description, t.Description)       AS [Description],
        t.Reference                                  AS Reference,
        t.ReferenceType                              AS ReferenceType,
        t.ReferenceId                                AS ReferenceId,
        a.Name                                       AS AccountName,
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
    LEFT JOIN dbo.Accounts AS a ON a.Id = l.AccountId
    WHERE l.TenantId = @TenantId
      AND t.TenantId = @TenantId
      AND l.ContactId = @ContactId
      AND l.IsDeleted = 0
      AND t.IsDeleted = 0
      AND (@OnlyPosted = 0 OR t.IsPosted = 1)
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
    SELECT TransactionId, TransactionNumber, [Date], [Description], Reference,
           ReferenceType, ReferenceId, AccountName, Debit, Credit, RunningBalance
    FROM #Lines
    ORDER BY [Date], TransactionNumber, LineNumber, LineId;
END
