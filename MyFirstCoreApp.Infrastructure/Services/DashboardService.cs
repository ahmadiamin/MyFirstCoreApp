using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Dashboard;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IFiscalYearService _fiscalYearService;
    private readonly TimeProvider _timeProvider;

    public DashboardService(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IFiscalYearService fiscalYearService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentTenant = currentTenant;
        _fiscalYearService = fiscalYearService;
        _timeProvider = timeProvider;
    }

    public async Task<DashboardSummary> GetSummaryAsync(
        int dueWindowDays = 7, int topN = 5, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("شرکت فعال مشخص نیست.");

        var today = _timeProvider.Today();
        var dueUntil = today.AddDays(Math.Max(dueWindowDays, 0));

        // بازه‌ی سال مالی جاری (یا سال شمسی جاری اگر سالی تعریف نشده)
        var fiscalYear = await _fiscalYearService.GetCurrentAsync(tenantId);
        var from = fiscalYear?.StartDate ?? PersianDate.StartOfYear(today);
        var to = fiscalYear?.EndDate ?? PersianDate.EndOfYear(today);

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId, DbType.Int32);
        parameters.Add("FromDate", from.ToDateTime(TimeOnly.MinValue), DbType.Date);
        parameters.Add("ToDate", to.ToDateTime(TimeOnly.MinValue), DbType.Date);
        parameters.Add("DueUntil", dueUntil.ToDateTime(TimeOnly.MinValue), DbType.Date);
        parameters.Add("TopN", Math.Clamp(topN, 1, 50), DbType.Int32);

        // مقدارهای enum از C# می‌آیند؛ SQL عدد جادویی ندارد
        parameters.Add("InvoiceTypeSale", (int)InvoiceType.Sale, DbType.Int32);
        parameters.Add("InvoiceTypePurchase", (int)InvoiceType.Purchase, DbType.Int32);
        parameters.Add("InvoiceStatusPosted", (int)InvoiceStatus.Posted, DbType.Int32);
        parameters.Add("AccountTypeBank", (int)AccountType.Bank, DbType.Int32);
        parameters.Add("AccountTypeCash", (int)AccountType.Cash, DbType.Int32);
        parameters.Add("AccountTypeReceivable", (int)AccountType.Receivable, DbType.Int32);
        parameters.Add("AccountTypePayable", (int)AccountType.Payable, DbType.Int32);
        parameters.Add("ChequeDirectionReceived", (int)ChequeDirection.Received, DbType.Int32);
        parameters.Add("ChequeDirectionIssued", (int)ChequeDirection.Issued, DbType.Int32);
        parameters.Add("ChequeStatusInProgress", (int)ChequeStatus.InProgress, DbType.Int32);
        parameters.Add("ChequeStatusRePresented", (int)ChequeStatus.RePresented, DbType.Int32);
        parameters.Add("PaymentTypeReceived", (int)PaymentType.Received, DbType.Int32);
        parameters.Add("PaymentTypePaid", (int)PaymentType.Paid, DbType.Int32);
        parameters.Add("PaymentStatusPosted", (int)PaymentStatus.Posted, DbType.Int32);

        var connection = _context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;

        if (wasClosed)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var command = new CommandDefinition(
                "dbo.usp_Dashboard_Summary",
                parameters,
                _context.Database.CurrentTransaction?.GetDbTransaction(),
                commandTimeout: 60,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken);

            using var multi = await connection.QueryMultipleAsync(command);

            var kpi = await multi.ReadSingleAsync<KpiRow>();
            var dueReceived = (await multi.ReadAsync<DueChequeRow>()).ToList();
            var dueIssued = (await multi.ReadAsync<DueChequeRow>()).ToList();
            var debtors = (await multi.ReadAsync<BalanceRow>()).ToList();
            var creditors = (await multi.ReadAsync<BalanceRow>()).ToList();
            var flow = (await multi.ReadAsync<FlowRow>()).ToList();

            var months = DashboardMonthBuckets.Build(from, to);
            DashboardMonthBuckets.Fill(months, flow.Select(f => new DailyFlow(
                DateOnly.FromDateTime(f.Date), f.Sales, f.Purchases, f.Received, f.Paid)));

            return new DashboardSummary
            {
                FiscalFrom = from,
                FiscalTo = to,
                Today = today,
                DueUntil = dueUntil,

                SalesTotal = kpi.SalesTotal,
                PurchasesTotal = kpi.PurchasesTotal,
                OpenSaleCount = kpi.OpenSaleCount,
                OpenSaleRemaining = kpi.OpenSaleRemaining,
                OpenPurchaseCount = kpi.OpenPurchaseCount,
                OpenPurchaseRemaining = kpi.OpenPurchaseRemaining,
                CashAndBankBalance = kpi.CashAndBankBalance,
                ReceivableBalance = kpi.ReceivableBalance,
                PayableBalance = kpi.PayableBalance,
                PendingReceivedChequeCount = kpi.PendingReceivedChequeCount,
                PendingReceivedChequeAmount = kpi.PendingReceivedChequeAmount,
                PendingIssuedChequeCount = kpi.PendingIssuedChequeCount,
                PendingIssuedChequeAmount = kpi.PendingIssuedChequeAmount,

                DueReceivedCheques = dueReceived.Select(c => ToDto(c, today)).ToList(),
                DueIssuedCheques = dueIssued.Select(c => ToDto(c, today)).ToList(),
                TopDebtors = debtors.Select(ToDto).ToList(),
                TopCreditors = creditors.Select(ToDto).ToList(),
                Months = months
            };
        }
        finally
        {
            if (wasClosed)
                await connection.CloseAsync();
        }
    }

    private static DueChequeDto ToDto(DueChequeRow row, DateOnly today)
    {
        var due = DateOnly.FromDateTime(row.DueDate);

        return new DueChequeDto
        {
            Id = row.Id,
            ChequeNumber = row.ChequeNumber,
            ContactName = row.ContactName,
            Amount = row.Amount,
            DueDate = due,
            DaysLeft = due.DayNumber - today.DayNumber
        };
    }

    private static ContactBalanceDto ToDto(BalanceRow row)
        => new() { ContactId = row.ContactId, Name = row.Name, Balance = row.Balance };

    // ردیف‌های خام (دقیقاً هم‌نام ستون‌های پروسیجر)
    private sealed class KpiRow
    {
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
    }

    private sealed class DueChequeRow
    {
        public int Id { get; set; }
        public string ChequeNumber { get; set; } = string.Empty;
        public string? ContactName { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }

    private sealed class BalanceRow
    {
        public int ContactId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    private sealed class FlowRow
    {
        public DateTime Date { get; set; }
        public decimal Sales { get; set; }
        public decimal Purchases { get; set; }
        public decimal Received { get; set; }
        public decimal Paid { get; set; }
    }
}
