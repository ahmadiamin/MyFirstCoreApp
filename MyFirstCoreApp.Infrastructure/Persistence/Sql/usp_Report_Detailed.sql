/*
    گزارش تفصیلی یک حساب (با فیلتر اختیاری طرف حساب).

    تفاوت با نسخه‌ی LINQ قبلی: فیلتر طرف حساب هم روی «مانده‌ی اول دوره» اعمال می‌شود
    (قبلاً فقط روی خطوط اعمال می‌شد و مانده‌ی تجمعی با خطوط نمایش‌داده‌شده نمی‌خواند).

    @OpeningFromDate: مثل usp_Report_Ledger (NULL = تجمعی، تاریخ = ابتدای سال مالی برای حساب‌های موقت).
    @OnlyPosted: 1 = فقط اسناد قطعی؛ 0 = پیش‌نویس‌ها هم.
*/
CREATE OR ALTER PROCEDURE dbo.usp_Report_Detailed
    @TenantId        INT,
    @AccountId       INT,
    @ContactId       INT  = NULL,
    @FromDate        DATE,
    @ToDate          DATE,
    @OpeningFromDate DATE = NULL,
    @OnlyPosted      BIT  = 1
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
      AND (@ContactId IS NULL OR l.ContactId = @ContactId)
      AND l.IsDeleted = 0
      AND t.IsDeleted = 0
      AND (@OnlyPosted = 0 OR t.IsPosted = 1)
      AND t.[Date] < @FromDate
      AND (@OpeningFromDate IS NULL OR t.[Date] >= @OpeningFromDate);

    SELECT
        t.Id                                         AS TransactionId,
        t.Number                                     AS TransactionNumber,
        t.[Date]                                     AS [Date],
        COALESCE(l.Description, t.Description)       AS [Description],
        c.Name                                       AS ContactName,
        t.Reference                                  AS Reference,
        t.ReferenceType                              AS ReferenceType,
        t.ReferenceId                                AS ReferenceId,
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
    LEFT JOIN dbo.Contacts AS c ON c.Id = l.ContactId
    WHERE l.TenantId = @TenantId
      AND t.TenantId = @TenantId
      AND l.AccountId = @AccountId
      AND (@ContactId IS NULL OR l.ContactId = @ContactId)
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
    SELECT TransactionId, TransactionNumber, [Date], [Description], ContactName, Reference,
           ReferenceType, ReferenceId, Debit, Credit, RunningBalance
    FROM #Lines
    ORDER BY [Date], TransactionNumber, LineNumber, LineId;
END
