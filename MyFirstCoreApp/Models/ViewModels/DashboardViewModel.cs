namespace MyFirstCoreApp.Models.ViewModels
{
    public class DashboardViewModel
    {
        // آمار کاربران و دسترسی‌ها
        public int UserCount { get; set; }
        public int RoleCount { get; set; }
        public int PermissionCount { get; set; }
        public int MenuCount { get; set; }

        // 🆕 اطلاعات Tenant فعلی
        public string TenantName { get; set; } = string.Empty;
        public string? TenantLegalName { get; set; }
        public bool IsTenantOwner { get; set; }

        // 🆕 آمار حسابداری (بعداً پر می‌شن)
        public int ContactCount { get; set; }
        public int ProductCount { get; set; }
        public int InvoiceCount { get; set; }
        public int ChequeCount { get; set; }

        // 🆕 مبالغ (نمایشی)
        public decimal TotalSales { get; set; }
        public decimal TotalPurchases { get; set; }
    }
}