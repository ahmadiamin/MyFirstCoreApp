/*
    گزارش فاکتورها با فیلتر و صفحه‌بندی سمت سرور.

    فیلترها همه اختیاری‌اند (NULL = بدون فیلتر):
        @InvoiceType : مقدار عددی enum نوع فاکتور
        @Status      : مقدار عددی enum وضعیت
        @ContactId   : طرف حساب
        @FromDate/@ToDate : بازه‌ی تاریخ فاکتور (دو سر شامل)
        @OnlyOpen    : 1 = فقط فاکتورهای دارای باقی‌مانده‌ی مثبت

    باقی‌مانده = مبلغ کل − پرداخت‌شده − تخفیف تسویه
    (همان فرمول InvoiceBalance در C#؛ اگر یکی عوض شد، دیگری هم باید عوض شود.)

    خروجی:
        ۱) یک ردیف جمع کل «همه‌ی ردیف‌های فیلترشده» (نه فقط صفحه‌ی جاری)
        ۲) ردیف‌های صفحه‌ی خواسته‌شده، جدیدترین اول
*/
CREATE OR ALTER PROCEDURE dbo.usp_Report_Invoices
    @TenantId    INT,
    @InvoiceType INT  = NULL,
    @Status      INT  = NULL,
    @ContactId   INT  = NULL,
    @FromDate    DATE = NULL,
    @ToDate      DATE = NULL,
    @OnlyOpen    BIT  = 0,
    @PageNumber  INT  = 1,
    @PageSize    INT  = 50
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 50;
    IF @PageSize > 10000 SET @PageSize = 10000;

    SELECT
        i.Id                  AS Id,
        i.Number              AS Number,
        i.[Date]              AS [Date],
        i.InvoiceType         AS InvoiceType,
        i.[Status]            AS [Status],
        i.ContactId           AS ContactId,
        c.Name                AS ContactName,
        i.[Description]       AS [Description],
        i.TotalAmount         AS TotalAmount,
        i.PaidAmount          AS PaidAmount,
        i.SettlementDiscount  AS SettlementDiscount,
        i.TotalAmount - i.PaidAmount - i.SettlementDiscount AS RemainingAmount
    INTO #Filtered
    FROM dbo.Invoices AS i
    LEFT JOIN dbo.Contacts AS c ON c.Id = i.ContactId
    WHERE i.TenantId = @TenantId
      AND i.IsDeleted = 0
      AND (@InvoiceType IS NULL OR i.InvoiceType = @InvoiceType)
      AND (@Status IS NULL OR i.[Status] = @Status)
      AND (@ContactId IS NULL OR i.ContactId = @ContactId)
      AND (@FromDate IS NULL OR i.[Date] >= @FromDate)
      AND (@ToDate IS NULL OR i.[Date] <= @ToDate)
      AND (@OnlyOpen = 0 OR i.TotalAmount - i.PaidAmount - i.SettlementDiscount > 0);

    -- ۱) جمع کل
    SELECT
        COUNT(*)                          AS [Count],
        ISNULL(SUM(TotalAmount), 0)       AS TotalAmount,
        ISNULL(SUM(PaidAmount), 0)        AS TotalPaid,
        ISNULL(SUM(SettlementDiscount), 0) AS TotalSettlementDiscount,
        ISNULL(SUM(RemainingAmount), 0)   AS TotalRemaining
    FROM #Filtered;

    -- ۲) صفحه‌ی جاری
    SELECT Id, Number, [Date], InvoiceType, [Status], ContactId, ContactName, [Description],
           TotalAmount, PaidAmount, SettlementDiscount, RemainingAmount
    FROM #Filtered
    ORDER BY [Date] DESC, Number DESC, Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
