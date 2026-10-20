using MyFirstCoreApp.Application.Dashboard;

namespace MyFirstCoreApp.Models.ViewModels
{
    public class DashboardViewModel
    {
        // ---------- اطلاعات شرکت ----------
        public string TenantName { get; set; } = string.Empty;
        public string? TenantLegalName { get; set; }
        public bool IsTenantOwner { get; set; }

        // ---------- آمار سیستم (فقط برای کاربرانِ دارای دسترسی) ----------
        public bool ShowSystemStats { get; set; }
        public int UserCount { get; set; }
        public int RoleCount { get; set; }
        public int PermissionCount { get; set; }
        public int MenuCount { get; set; }

        // ---------- شمارنده‌های حسابداری (حالا مقدار واقعی دارند) ----------
        public int ContactCount { get; set; }
        public int ProductCount { get; set; }
        public int InvoiceCount { get; set; }

        /// <summary>چک‌های در انتظار وصول (در جریان + ارائه‌مجدد).</summary>
        public int ChequeCount { get; set; }

        public decimal TotalSales { get; set; }
        public decimal TotalPurchases { get; set; }

        // ---------- ویجت‌های مالی (بر اساس دسترسی کاربر) ----------
        public bool ShowFinance { get; set; }      // Report.Ledger
        public bool ShowCheques { get; set; }      // Cheque.View
        public bool ShowInvoices { get; set; }     // Invoice.View

        /// <summary>null یعنی خلاصه‌ی مالی در دسترس نبود (مثلاً پروسیجر نصب نشده)؛ بقیه‌ی داشبورد نمایش داده می‌شود.</summary>
        public DashboardSummary? Summary { get; set; }

        public string FiscalRangeText { get; set; } = string.Empty;
        public string TodayText { get; set; } = string.Empty;
    }
}
