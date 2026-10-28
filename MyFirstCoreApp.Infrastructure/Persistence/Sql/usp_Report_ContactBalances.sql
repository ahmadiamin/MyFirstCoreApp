/*
    گزارش بدهکاران / بستانکاران (مانده‌ی طرف حساب‌ها).

    @Mode: 1 = بدهکاران (مانده‌ی بدهکار − بستانکار > 0؛ آن‌ها به ما بدهکارند)
           2 = بستانکاران (بستانکار − بدهکار > 0؛ ما به آن‌ها بدهکاریم)
    ستون Amount همیشه «مقدار مثبتِ مانده» در جهتِ همان حالت است.

    مانده از «اسناد قطعی و حذف‌نشده تا @AsOfDate» می‌آید (همه‌ی خطوطِ دارای طرف حساب، روی هر حسابی).

    ستون‌های فاکتور باز (سن بدهی) و چک‌های در انتظار «وضعیت فعلی» هستند، نه تاریخچه‌ی @AsOfDate
    (تخصیص پرداخت‌ها تاریخ‌دار نگه‌داری نمی‌شود). برای @AsOfDate = امروز دقیق‌اند.

    @InvoiceType و @ChequeDirection را C# تعیین می‌کند (بدهکاران: فروش/دریافتنی؛ بستانکاران: خرید/پرداختنی)
    تا عدد جادویی در SQL نباشد.

    سن بدهی: DATEDIFF(روز، تاریخ فاکتور، @AsOfDate) ← 0–30، 31–60، 61–90، بیش از 90.
    باقی‌مانده‌ی فاکتور = مبلغ کل − پرداخت‌شده − تخفیف تسویه.

    @SortBy: 1 = بیشترین مانده، 2 = نام، 3 = قدیمی‌ترین آخرین فعالیت
    @PageSize: 0 = همه‌ی ردیف‌ها (برای خروجی Excel)

    خروجی: ۱) یک ردیف جمع کلِ همه‌ی ردیف‌های فیلترشده  ۲) صفحه‌ی خواسته‌شده
*/
CREATE OR ALTER PROCEDURE dbo.usp_Report_ContactBalances
    @TenantId                INT,
    @AsOfDate                DATE,
    @Mode                    INT,
    @MinAmount               DECIMAL(19, 0) = 0,
    @Search                  NVARCHAR(100) = NULL,
    @InvoiceType             INT,
    @InvoiceStatusPosted     INT,
    @ChequeDirection         INT,
    @ChequeStatusInProgress  INT,
    @ChequeStatusRePresented INT,
    @SortBy                  INT = 1,
    @PageNumber              INT = 1,
    @PageSize                INT = 50
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 0 SET @PageSize = 50;
    IF @PageSize > 20000 SET @PageSize = 20000;
    IF @MinAmount < 0 SET @MinAmount = 0;

    DECLARE @Offset INT = CASE WHEN @PageSize = 0 THEN 0 ELSE (@PageNumber - 1) * @PageSize END;
    DECLARE @Fetch  INT = CASE WHEN @PageSize = 0 THEN 2147483647 ELSE @PageSize END;

    -- الگوی جستجو (نویسه‌های ویژه‌ی LIKE خنثی می‌شوند)
    DECLARE @Pattern NVARCHAR(220) = NULL;
    IF @Search IS NOT NULL AND LTRIM(RTRIM(@Search)) <> N''
        SET @Pattern = N'%' + REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(@Search)), N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%';

    -- مانده‌ی دفتر هر طرف حساب ----------------------------------------------------
    SELECT
        l.ContactId     AS ContactId,
        SUM(l.Debit)    AS TotalDebit,
        SUM(l.Credit)   AS TotalCredit,
        MAX(t.[Date])   AS LastDate
    INTO #Bal
    FROM dbo.TransactionLines AS l
    INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
    WHERE l.TenantId = @TenantId
      AND t.TenantId = @TenantId
      AND l.IsDeleted = 0
      AND t.IsDeleted = 0
      AND t.IsPosted = 1
      AND l.ContactId IS NOT NULL
      AND t.[Date] <= @AsOfDate
    GROUP BY l.ContactId;

    -- فاکتورهای بازِ همین نوع (سن بدهی) --------------------------------------------
    SELECT
        x.ContactId                                                    AS ContactId,
        COUNT(*)                                                       AS OpenCount,
        SUM(x.Remaining)                                               AS OpenRemaining,
        SUM(CASE WHEN x.AgeDays <= 30                  THEN x.Remaining ELSE 0 END) AS Age0To30,
        SUM(CASE WHEN x.AgeDays BETWEEN 31 AND 60      THEN x.Remaining ELSE 0 END) AS Age31To60,
        SUM(CASE WHEN x.AgeDays BETWEEN 61 AND 90      THEN x.Remaining ELSE 0 END) AS Age61To90,
        SUM(CASE WHEN x.AgeDays > 90                   THEN x.Remaining ELSE 0 END) AS Age90Plus
    INTO #Inv
    FROM (
        SELECT
            i.ContactId AS ContactId,
            i.TotalAmount - i.PaidAmount - i.SettlementDiscount AS Remaining,
            DATEDIFF(DAY, i.[Date], @AsOfDate) AS AgeDays
        FROM dbo.Invoices AS i
        WHERE i.TenantId = @TenantId
          AND i.IsDeleted = 0
          AND i.InvoiceType = @InvoiceType
          AND i.[Status] = @InvoiceStatusPosted
          AND i.ContactId IS NOT NULL
          AND i.[Date] <= @AsOfDate
          AND i.TotalAmount - i.PaidAmount - i.SettlementDiscount > 0
    ) AS x
    GROUP BY x.ContactId;

    -- چک‌های در انتظار وصول (مبلغ‌شان در مانده‌ی دفتر قبلاً کم شده است) -------------
    SELECT c.ContactId AS ContactId, SUM(c.Amount) AS PendingCheques
    INTO #Chq
    FROM dbo.Cheques AS c
    WHERE c.TenantId = @TenantId
      AND c.IsDeleted = 0
      AND c.Direction = @ChequeDirection
      AND c.[Status] IN (@ChequeStatusInProgress, @ChequeStatusRePresented)
      AND c.ContactId IS NOT NULL
      AND c.IssueDate <= @AsOfDate
    GROUP BY c.ContactId;

    -- ترکیب و فیلتر -----------------------------------------------------------------
    SELECT
        b.ContactId                                                   AS ContactId,
        ct.Name                                                       AS ContactName,
        ct.Code                                                       AS ContactCode,
        ct.Mobile                                                     AS Mobile,
        b.TotalDebit                                                  AS TotalDebit,
        b.TotalCredit                                                 AS TotalCredit,
        CASE WHEN @Mode = 1 THEN b.TotalDebit - b.TotalCredit
             ELSE b.TotalCredit - b.TotalDebit END                    AS Amount,
        b.LastDate                                                    AS LastTransactionDate,
        ISNULL(v.OpenCount, 0)                                        AS OpenInvoiceCount,
        ISNULL(v.OpenRemaining, 0)                                    AS OpenInvoiceRemaining,
        ISNULL(v.Age0To30, 0)                                         AS Age0To30,
        ISNULL(v.Age31To60, 0)                                        AS Age31To60,
        ISNULL(v.Age61To90, 0)                                        AS Age61To90,
        ISNULL(v.Age90Plus, 0)                                        AS Age90Plus,
        ISNULL(q.PendingCheques, 0)                                   AS PendingChequeAmount
    INTO #Result
    FROM #Bal AS b
    LEFT JOIN dbo.Contacts AS ct ON ct.Id = b.ContactId
    LEFT JOIN #Inv AS v ON v.ContactId = b.ContactId
    LEFT JOIN #Chq AS q ON q.ContactId = b.ContactId
    WHERE (CASE WHEN @Mode = 1 THEN b.TotalDebit - b.TotalCredit ELSE b.TotalCredit - b.TotalDebit END) > 0
      AND (CASE WHEN @Mode = 1 THEN b.TotalDebit - b.TotalCredit ELSE b.TotalCredit - b.TotalDebit END) >= @MinAmount
      AND (@Pattern IS NULL OR ct.Name LIKE @Pattern OR ct.Code LIKE @Pattern OR ct.Mobile LIKE @Pattern);

    -- ۱) جمع کل (همه‌ی ردیف‌های فیلترشده، نه فقط صفحه‌ی جاری) -------------------------
    SELECT
        COUNT(*)                              AS [Count],
        ISNULL(SUM(Amount), 0)                AS TotalAmount,
        ISNULL(SUM(TotalDebit), 0)            AS TotalDebit,
        ISNULL(SUM(TotalCredit), 0)           AS TotalCredit,
        ISNULL(SUM(OpenInvoiceRemaining), 0)  AS TotalOpenInvoiceRemaining,
        ISNULL(SUM(Age0To30), 0)              AS TotalAge0To30,
        ISNULL(SUM(Age31To60), 0)             AS TotalAge31To60,
        ISNULL(SUM(Age61To90), 0)             AS TotalAge61To90,
        ISNULL(SUM(Age90Plus), 0)             AS TotalAge90Plus,
        ISNULL(SUM(PendingChequeAmount), 0)   AS TotalPendingCheque
    FROM #Result;

    -- ۲) صفحه‌ی خواسته‌شده ------------------------------------------------------------
    SELECT
        ContactId, ContactName, ContactCode, Mobile, TotalDebit, TotalCredit, Amount, LastTransactionDate,
        OpenInvoiceCount, OpenInvoiceRemaining, Age0To30, Age31To60, Age61To90, Age90Plus, PendingChequeAmount
    FROM #Result
    ORDER BY
        CASE WHEN @SortBy = 1 THEN Amount END DESC,
        CASE WHEN @SortBy = 2 THEN ContactName END ASC,
        CASE WHEN @SortBy = 3 THEN LastTransactionDate END ASC,
        ContactId
    OFFSET @Offset ROWS
    FETCH NEXT @Fetch ROWS ONLY;
END
