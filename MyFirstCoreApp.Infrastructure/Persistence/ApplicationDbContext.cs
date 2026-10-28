using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Attachments;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Entities.Tenancy;

namespace MyFirstCoreApp.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly ICurrentTenant _currentTenant;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ICurrentTenant currentTenant)
            : base(options)
        {
            _currentTenant = currentTenant;
        }

        /// <summary>
        /// شرکت فعال. Query Filterها این پراپرتی را در هر کوئری می‌خوانند.
        /// اگر null باشد (شرکت انتخاب نشده)، هیچ رکورد شرکت‌داری برگردانده نمی‌شود.
        /// </summary>
        public int? CurrentTenantId => _currentTenant.TenantId;

        // Tenancy
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<UserTenant> UserTenants { get; set; }
        public DbSet<TenantSmsSetting> TenantSmsSettings { get; set; }
        public DbSet<SystemSmsSetting> SystemSmsSettings { get; set; }

        //Attachment
        public DbSet<Attachment> Attachments { get; set; }

        // Accounting
        public DbSet<AccountCategory> AccountCategories { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<TransactionGroup> TransactionGroups { get; set; }
        public DbSet<FiscalYear> FiscalYears { get; set; }

        // Transactions
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionLine> TransactionLines { get; set; }

        // Contacts
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<CustomerPortalShare> CustomerPortalShares { get; set; }

        // Inventory
        public DbSet<Unit> Units { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductAttribute> ProductAttributes { get; set; }
        
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<StockMovementItem> StockMovementItems { get; set; }

        // Invoicing
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<InvoiceHistory> InvoiceHistories { get; set; }
        public DbSet<InvoicePrintSetting> InvoicePrintSettings { get; set; }
        public DbSet<InvoicePoem> InvoicePoems { get; set; }

        // Banking
        public DbSet<Cheque> Cheques { get; set; }
        public DbSet<ChequeSmsReminderDispatch> ChequeSmsReminderDispatches { get; set; }
        public DbSet<ChequeHistory> ChequeHistories { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; }
        public DbSet<PaymentHistory> PaymentHistories { get; set; }

        // Identity (از قبلی‌ها)
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Menu> Menus { get; set; }
        public DbSet<UserProfileImage> UserProfileImages { get; set; }

        /// <summary>
        /// دسترسی به رکوردهای حذف‌شده‌ی نرم، فقط برای شرکت فعال.
        /// به‌جای IgnoreQueryFilters() خام استفاده شود، چون IgnoreQueryFilters
        /// فیلتر شرکت را هم برمی‌دارد و داده‌ی شرکت‌های دیگر را نشان می‌دهد.
        /// </summary>
        public IQueryable<TEntity> SetIncludingDeleted<TEntity>() where TEntity : TenantEntity
            => Set<TEntity>()
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == CurrentTenantId);

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            builder.Entity<RolePermission>()
                .HasKey(x => new
                {
                    x.RoleId,
                    x.PermissionId
                });

            builder.Entity<RolePermission>()
                .HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RolePermission>()
                .HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Permission>()
                .HasIndex(x => x.Name)
                .IsUnique();

            builder.Entity<Menu>()
                .HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Menu>()
                .HasIndex(x => x.SortOrder);

            builder.Entity<UserProfileImage>()
                .HasOne(x => x.User)
                .WithMany(x => x.ProfileImages)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserProfileImage>()
                .HasIndex(x => new { x.UserId, x.IsPrimary })
                .HasFilter("[IsPrimary] = 1")
                .IsUnique();

            // باید آخرین کار باشد تا روی همه‌ی کانفیگ‌ها غلبه کند
            ApplyGlobalQueryFilters(builder);
        }

        // ---------------------------------------------------------------
        // Global Query Filters
        // ---------------------------------------------------------------

        private void ApplyGlobalQueryFilters(ModelBuilder builder)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            var tenantFilter = typeof(ApplicationDbContext)
                .GetMethod(nameof(ApplyTenantFilter), flags)!;
            var softDeleteFilter = typeof(ApplicationDbContext)
                .GetMethod(nameof(ApplySoftDeleteFilter), flags)!;

            foreach (var entityType in builder.Model.GetEntityTypes().ToList())
            {
                // فیلتر فقط روی ریشه‌ی سلسله‌مراتب تعریف می‌شود
                if (entityType.BaseType != null || entityType.IsOwned())
                    continue;

                var clrType = entityType.ClrType;

                if (typeof(TenantEntity).IsAssignableFrom(clrType))
                {
                    tenantFilter.MakeGenericMethod(clrType).Invoke(this, new object[] { builder });
                }
                else if (typeof(BaseEntity).IsAssignableFrom(clrType))
                {
                    softDeleteFilter.MakeGenericMethod(clrType).Invoke(this, new object[] { builder });
                }
            }
        }

        // این‌ها باید داخل خود DbContext باشند تا EF بتواند CurrentTenantId را
        // برای هر نمونه‌ی Context جداگانه (و نه فقط اولین نمونه) ارزیابی کند.
        // (با reflection صدا زده می‌شوند، پس "unused" نیستند.)

        private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : TenantEntity
        {
            builder.Entity<TEntity>()
                .HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
        }

        private void ApplySoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : BaseEntity
        {
            builder.Entity<TEntity>()
                .HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
