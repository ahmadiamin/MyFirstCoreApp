/*
    خلاصه‌ی داشبورد یک شرکت. شش Result Set به این ترتیب:
        ۱) KPIها (یک ردیف)
        ۲) چک‌های دریافتنیِ سررسیدشده یا نزدیک (تا @DueUntil)
        ۳) چک‌های پرداختنیِ سررسیدشده یا نزدیک
        ۴) بدهکاران برتر (طرف حساب‌های دارای مانده‌ی مثبت)
        ۵) بستانکاران برتر (مانده‌ی منفی؛ مقدار منفی برمی‌گردد)
        ۶) جریان روزانه‌ی سال مالی: فروش، خرید، دریافت، پرداخت (تقسیم به ماه شمسی در C# انجام می‌شود)

    مقدارهای enum از C# پاس داده می‌شوند تا عدد جادویی در SQL نباشد.

    فرمول باقی‌مانده‌ی فاکتور = مبلغ کل − پرداخت‌شده − تخفیف تسویه (مثل InvoiceBalance در C#).
    مانده‌ها فقط از اسناد قطعی و حذف‌نشده‌اند.
*/
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Summary
    @TenantId                 INT,
    @FromDate                 DATE,
    @ToDate                   DATE,
    @DueUntil                 DATE,
    @TopN                     INT = 5,

    @InvoiceTypeSale          INT,
    @InvoiceTypePurchase      INT,
    @InvoiceStatusPosted      INT,

    @AccountTypeBank          INT,
    @AccountTypeCash          INT,
    @AccountTypeReceivable    INT,
    @AccountTypePayable       INT,

    @ChequeDirectionReceived  INT,
    @ChequeDirectionIssued    INT,
    @ChequeStatusInProgress   INT,
    @ChequeStatusRePresented  INT,

    @PaymentTypeReceived      INT,
    @PaymentTypePaid          INT,
    @PaymentStatusPosted      INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @TopN < 1 SET @TopN = 5;
    IF @TopN > 50 SET @TopN = 50;

    -- ۱) KPIها ---------------------------------------------------------------
    SELECT
        -- فروش و خرید قطعیِ سال مالی
        (SELECT ISNULL(SUM(i.TotalAmount), 0) FROM dbo.Invoices AS i
          WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
            AND i.InvoiceType = @InvoiceTypeSale AND i.[Status] = @InvoiceStatusPosted
            AND i.[Date] >= @FromDate AND i.[Date] <= @ToDate)                       AS SalesTotal,

        (SELECT ISNULL(SUM(i.TotalAmount), 0) FROM dbo.Invoices AS i
          WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
            AND i.InvoiceType = @InvoiceTypePurchase AND i.[Status] = @InvoiceStatusPosted
            AND i.[Date] >= @FromDate AND i.[Date] <= @ToDate)                       AS PurchasesTotal,

        -- فاکتورهای باز (همه‌ی سال‌ها): تعداد و باقی‌مانده
        (SELECT COUNT(*) FROM dbo.Invoices AS i
          WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
            AND i.InvoiceType = @InvoiceTypeSale AND i.[Status] = @InvoiceStatusPosted
            AND i.TotalAmount - i.PaidAmount - i.SettlementDiscount > 0)             AS OpenSaleCount,

        (SELECT ISNULL(SUM(i.TotalAmount - i.PaidAmount - i.SettlementDiscount), 0) FROM dbo.Invoices AS i
          WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
            AND i.InvoiceType = @InvoiceTypeSale AND i.[Status] = @InvoiceStatusPosted
            AND i.TotalAmount - i.PaidAmount - i.SettlementDiscount > 0)             AS OpenSaleRemaining,

        (SELECT COUNT(*) FROM dbo.Invoices AS i
          WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
            AND i.InvoiceType = @InvoiceTypePurchase AND i.[Status] = @InvoiceStatusPosted
            AND i.TotalAmount - i.PaidAmount - i.SettlementDiscount > 0)             AS OpenPurchaseCount,

        (SELECT ISNULL(SUM(i.TotalAmount - i.PaidAmount - i.SettlementDiscount), 0) FROM dbo.Invoices AS i
          WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
            AND i.InvoiceType = @InvoiceTypePurchase AND i.[Status] = @InvoiceStatusPosted
            AND i.TotalAmount - i.PaidAmount - i.SettlementDiscount > 0)             AS OpenPurchaseRemaining,

        -- موجودی نقد و بانک (بدهکار − بستانکار)
        (SELECT ISNULL(SUM(l.Debit - l.Credit), 0)
           FROM dbo.TransactionLines AS l
           INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
           INNER JOIN dbo.Accounts AS a ON a.Id = l.AccountId
          WHERE l.TenantId = @TenantId AND t.TenantId = @TenantId
            AND l.IsDeleted = 0 AND t.IsDeleted = 0 AND t.IsPosted = 1
            AND a.AccountType IN (@AccountTypeBank, @AccountTypeCash))               AS CashAndBankBalance,

        -- مجموع دریافتنی‌ها (بدهکار − بستانکار) و پرداختنی‌ها (بستانکار − بدهکار)
        (SELECT ISNULL(SUM(l.Debit - l.Credit), 0)
           FROM dbo.TransactionLines AS l
           INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
           INNER JOIN dbo.Accounts AS a ON a.Id = l.AccountId
          WHERE l.TenantId = @TenantId AND t.TenantId = @TenantId
            AND l.IsDeleted = 0 AND t.IsDeleted = 0 AND t.IsPosted = 1
            AND a.AccountType = @AccountTypeReceivable)                              AS ReceivableBalance,

        (SELECT ISNULL(SUM(l.Credit - l.Debit), 0)
           FROM dbo.TransactionLines AS l
           INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
           INNER JOIN dbo.Accounts AS a ON a.Id = l.AccountId
          WHERE l.TenantId = @TenantId AND t.TenantId = @TenantId
            AND l.IsDeleted = 0 AND t.IsDeleted = 0 AND t.IsPosted = 1
            AND a.AccountType = @AccountTypePayable)                                 AS PayableBalance,

        -- چک‌های در انتظار وصول
        (SELECT COUNT(*) FROM dbo.Cheques AS c
          WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
            AND c.Direction = @ChequeDirectionReceived
            AND c.[Status] IN (@ChequeStatusInProgress, @ChequeStatusRePresented))   AS PendingReceivedChequeCount,

        (SELECT ISNULL(SUM(c.Amount), 0) FROM dbo.Cheques AS c
          WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
            AND c.Direction = @ChequeDirectionReceived
            AND c.[Status] IN (@ChequeStatusInProgress, @ChequeStatusRePresented))   AS PendingReceivedChequeAmount,

        (SELECT COUNT(*) FROM dbo.Cheques AS c
          WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
            AND c.Direction = @ChequeDirectionIssued
            AND c.[Status] IN (@ChequeStatusInProgress, @ChequeStatusRePresented))   AS PendingIssuedChequeCount,

        (SELECT ISNULL(SUM(c.Amount), 0) FROM dbo.Cheques AS c
          WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
            AND c.Direction = @ChequeDirectionIssued
            AND c.[Status] IN (@ChequeStatusInProgress, @ChequeStatusRePresented))   AS PendingIssuedChequeAmount;

    -- ۲) چک‌های دریافتنیِ سررسیدشده/نزدیک ------------------------------------------
    SELECT TOP (@TopN)
        c.Id AS Id, c.ChequeNumber AS ChequeNumber, ct.Name AS ContactName, c.Amount AS Amount, c.DueDate AS DueDate
    FROM dbo.Cheques AS c
    LEFT JOIN dbo.Contacts AS ct ON ct.Id = c.ContactId
    WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
      AND c.Direction = @ChequeDirectionReceived
      AND c.[Status] IN (@ChequeStatusInProgress, @ChequeStatusRePresented)
      AND c.DueDate <= @DueUntil
    ORDER BY c.DueDate, c.Id;

    -- ۳) چک‌های پرداختنیِ سررسیدشده/نزدیک ------------------------------------------
    SELECT TOP (@TopN)
        c.Id AS Id, c.ChequeNumber AS ChequeNumber, ct.Name AS ContactName, c.Amount AS Amount, c.DueDate AS DueDate
    FROM dbo.Cheques AS c
    LEFT JOIN dbo.Contacts AS ct ON ct.Id = c.ContactId
    WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
      AND c.Direction = @ChequeDirectionIssued
      AND c.[Status] IN (@ChequeStatusInProgress, @ChequeStatusRePresented)
      AND c.DueDate <= @DueUntil
    ORDER BY c.DueDate, c.Id;

    -- مانده‌ی هر طرف حساب (همه‌ی اسناد قطعی، همه‌ی سال‌ها) -------------------------
    SELECT l.ContactId AS ContactId, SUM(l.Debit - l.Credit) AS Balance
    INTO #Balances
    FROM dbo.TransactionLines AS l
    INNER JOIN dbo.Transactions AS t ON t.Id = l.TransactionId
    WHERE l.TenantId = @TenantId AND t.TenantId = @TenantId
      AND l.IsDeleted = 0 AND t.IsDeleted = 0 AND t.IsPosted = 1
      AND l.ContactId IS NOT NULL
    GROUP BY l.ContactId;

    -- ۴) بدهکاران برتر ------------------------------------------------------------
    SELECT TOP (@TopN) b.ContactId AS ContactId, c.Name AS Name, b.Balance AS Balance
    FROM #Balances AS b
    INNER JOIN dbo.Contacts AS c ON c.Id = b.ContactId
    WHERE b.Balance > 0
    ORDER BY b.Balance DESC, b.ContactId;

    -- ۵) بستانکاران برتر ----------------------------------------------------------
    SELECT TOP (@TopN) b.ContactId AS ContactId, c.Name AS Name, b.Balance AS Balance
    FROM #Balances AS b
    INNER JOIN dbo.Contacts AS c ON c.Id = b.ContactId
    WHERE b.Balance < 0
    ORDER BY b.Balance ASC, b.ContactId;

    -- ۶) جریان روزانه‌ی سال مالی --------------------------------------------------
    SELECT d.[Date] AS [Date],
           SUM(d.Sales) AS Sales, SUM(d.Purchases) AS Purchases,
           SUM(d.Received) AS Received, SUM(d.Paid) AS Paid
    FROM (
        SELECT i.[Date] AS [Date],
               CAST(CASE WHEN i.InvoiceType = @InvoiceTypeSale     THEN i.TotalAmount ELSE 0 END AS DECIMAL(19, 0)) AS Sales,
               CAST(CASE WHEN i.InvoiceType = @InvoiceTypePurchase THEN i.TotalAmount ELSE 0 END AS DECIMAL(19, 0)) AS Purchases,
               CAST(0 AS DECIMAL(19, 0)) AS Received,
               CAST(0 AS DECIMAL(19, 0)) AS Paid
        FROM dbo.Invoices AS i
        WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
          AND i.[Status] = @InvoiceStatusPosted
          AND i.InvoiceType IN (@InvoiceTypeSale, @InvoiceTypePurchase)
          AND i.[Date] >= @FromDate AND i.[Date] <= @ToDate

        UNION ALL

        SELECT p.[Date] AS [Date],
               CAST(0 AS DECIMAL(19, 0)) AS Sales,
               CAST(0 AS DECIMAL(19, 0)) AS Purchases,
               CAST(CASE WHEN p.PaymentType = @PaymentTypeReceived THEN p.Amount ELSE 0 END AS DECIMAL(19, 0)) AS Received,
               CAST(CASE WHEN p.PaymentType = @PaymentTypePaid     THEN p.Amount ELSE 0 END AS DECIMAL(19, 0)) AS Paid
        FROM dbo.Payments AS p
        WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
          AND p.[Status] = @PaymentStatusPosted
          AND p.[Date] >= @FromDate AND p.[Date] <= @ToDate
    ) AS d
    GROUP BY d.[Date]
    ORDER BY d.[Date];
END
