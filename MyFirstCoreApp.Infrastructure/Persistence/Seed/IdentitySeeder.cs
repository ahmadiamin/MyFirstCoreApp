
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Infrastructure.Persistence.Seed
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(
            IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var configuration = scope.ServiceProvider
                .GetRequiredService<IConfiguration>();

            await context.Database.MigrateAsync();

            await SeedRolesAsync(roleManager);

            await SeedPermissionsAsync(context);

            await SeedRolePermissionsAsync(
                context,
                roleManager);

            await SeedMenusAsync(context);

            await SeedAdminUserAsync(
                userManager,
                roleManager,
                configuration);

        }

        private static async Task SeedRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            string[] roles =
            {
            "Admin",
            "Manager",
            "Operator",
            "User"
        };

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole(roleName));

                    if (!result.Succeeded)
                    {
                        throw new Exception(
                            $"خطا در ایجاد Role '{roleName}': " +
                            string.Join(", ",
                                result.Errors.Select(x => x.Description)));
                    }
                }
            }
        }

        private static async Task SeedPermissionsAsync(
            ApplicationDbContext context)
        {
            var permissions = new[]
            {
                // Users
                new Permission
                {
                    Name = "User.View",
                    DisplayName = "مشاهده کاربران",
                    GroupName = "Users",
                    IsActive = true
                },
                new Permission
                {
                    Name = "User.Create",
                    DisplayName = "ایجاد کاربر",
                    GroupName = "Users",
                    IsActive = true
                },
                new Permission
                {
                    Name = "User.Edit",
                    DisplayName = "ویرایش کاربر",
                    GroupName = "Users",
                    IsActive = true
                },
                new Permission
                {
                    Name = "User.Delete",
                    DisplayName = "حذف کاربر",
                    GroupName = "Users",
                    IsActive = true
                },

                // Roles
                new Permission
                {
                    Name = "Role.View",
                    DisplayName = "مشاهده نقش‌ها",
                    GroupName = "Roles",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Role.Create",
                    DisplayName = "ایجاد نقش",
                    GroupName = "Roles",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Role.Edit",
                    DisplayName = "ویرایش نقش‌ها",
                    GroupName = "Roles",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Role.Delete",
                    DisplayName = "حذف نقش‌ها",
                    GroupName = "Roles",
                    IsActive = true
                },

                // Permissions
                new Permission
                {
                    Name = "Permission.View",
                    DisplayName = "مشاهده دسترسی‌ها",
                    GroupName = "Permissions",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Permission.Create",
                    DisplayName = "ایجاد دسترسی",
                    GroupName = "Permissions",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Permission.Edit",
                    DisplayName = "ویرایش دسترسی‌ها",
                    GroupName = "Permissions",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Permission.Delete",
                    DisplayName = "حذف دسترسی‌ها",
                    GroupName = "Permissions",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Menu.View",
                    DisplayName = "مشاهده منوها",
                    GroupName = "منوها",
                    IsActive = true
                },

                new Permission
                {
                    Name = "Menu.Create",
                    DisplayName = "ایجاد منو",
                    GroupName = "منوها",
                    IsActive = true
                },

                new Permission
                {
                    Name = "Menu.Edit",
                    DisplayName = "ویرایش منو",
                    GroupName = "منوها",
                    IsActive = true
                },

                new Permission
                {
                    Name = "Menu.Delete",
                    DisplayName = "حذف منو",
                    GroupName = "منوها",
                    IsActive = true
                },

                // Tenancy
                new Permission
                {
                    Name = "Tenancy.View",
                    DisplayName = "مشاهده شرکت‌ها",
                    GroupName = "Tenancy",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Tenancy.Create",
                    DisplayName = "ثبت شرکت",
                    GroupName = "Tenancy",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Tenancy.Edit",
                    DisplayName = "ویرایش شرکت",
                    GroupName = "Tenancy",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Tenancy.Delete",
                    DisplayName = "حذف شرکت",
                    GroupName = "Tenancy",
                    IsActive = true
                },
                new Permission
                {
                    Name = "Tenancy.Select",
                    DisplayName = "انتخاب شرکت",
                    GroupName = "Tenancy",
                    IsActive = true
                },

                // AccountCategory
                new Permission { Name = "AccountCategory.View",   DisplayName = "مشاهده دسته‌بندی حساب‌ها", GroupName = "Accounting", IsActive = true },
                new Permission { Name = "AccountCategory.Create", DisplayName = "ایجاد دسته‌بندی حساب",   GroupName = "Accounting", IsActive = true },
                new Permission { Name = "AccountCategory.Edit",   DisplayName = "ویرایش دسته‌بندی حساب",   GroupName = "Accounting", IsActive = true },
                new Permission { Name = "AccountCategory.Delete", DisplayName = "حذف دسته‌بندی حساب",      GroupName = "Accounting", IsActive = true },

                // Account
                new Permission { Name = "Account.View",   DisplayName = "مشاهده حساب‌ها", GroupName = "Accounting", IsActive = true },
                new Permission { Name = "Account.Create", DisplayName = "ایجاد حساب",     GroupName = "Accounting", IsActive = true },
                new Permission { Name = "Account.Edit",   DisplayName = "ویرایش حساب",    GroupName = "Accounting", IsActive = true },
                new Permission { Name = "Account.Delete", DisplayName = "حذف حساب",       GroupName = "Accounting", IsActive = true },

                // Report
                new Permission { Name = "Report.Ledger", DisplayName = "گزارش معین", GroupName = "Reports", IsActive = true },

                // TransactionGroup
                new Permission { Name = "TransactionGroup.View",   DisplayName = "مشاهده گروه تراکنش‌ها", GroupName = "Accounting", IsActive = true },
                new Permission { Name = "TransactionGroup.Create", DisplayName = "ایجاد گروه تراکنش",     GroupName = "Accounting", IsActive = true },
                new Permission { Name = "TransactionGroup.Edit",   DisplayName = "ویرایش گروه تراکنش",    GroupName = "Accounting", IsActive = true },
                new Permission { Name = "TransactionGroup.Delete", DisplayName = "حذف گروه تراکنش",       GroupName = "Accounting", IsActive = true },

                // Contact
                new Permission { Name = "Contact.View",   DisplayName = "مشاهده طرف حساب‌ها", GroupName = "Contacts", IsActive = true },
                new Permission { Name = "Contact.Create", DisplayName = "ایجاد طرف حساب",     GroupName = "Contacts", IsActive = true },
                new Permission { Name = "Contact.Edit",   DisplayName = "ویرایش طرف حساب",    GroupName = "Contacts", IsActive = true },
                new Permission { Name = "Contact.Delete", DisplayName = "حذف طرف حساب",       GroupName = "Contacts", IsActive = true },

                // Unit
                new Permission { Name = "Unit.View",   DisplayName = "مشاهده واحدها", GroupName = "Inventory", IsActive = true },
                new Permission { Name = "Unit.Create", DisplayName = "ایجاد واحد",     GroupName = "Inventory", IsActive = true },
                new Permission { Name = "Unit.Edit",   DisplayName = "ویرایش واحد",    GroupName = "Inventory", IsActive = true },
                new Permission { Name = "Unit.Delete", DisplayName = "حذف واحد",       GroupName = "Inventory", IsActive = true },

                // ProductCategory
                new Permission { Name = "ProductCategory.View",   DisplayName = "مشاهده دسته‌بندی کالا", GroupName = "Inventory", IsActive = true },
                new Permission { Name = "ProductCategory.Create", DisplayName = "ایجاد دسته‌بندی کالا",  GroupName = "Inventory", IsActive = true },
                new Permission { Name = "ProductCategory.Edit",   DisplayName = "ویرایش دسته‌بندی کالا",  GroupName = "Inventory", IsActive = true },
                new Permission { Name = "ProductCategory.Delete", DisplayName = "حذف دسته‌بندی کالا",     GroupName = "Inventory", IsActive = true },

                // Product
                new Permission { Name = "Product.View",   DisplayName = "مشاهده کالاها", GroupName = "Inventory", IsActive = true },
                new Permission { Name = "Product.Create", DisplayName = "ایجاد کالا",     GroupName = "Inventory", IsActive = true },
                new Permission { Name = "Product.Edit",   DisplayName = "ویرایش کالا",    GroupName = "Inventory", IsActive = true },
                new Permission { Name = "Product.Delete", DisplayName = "حذف کالا",       GroupName = "Inventory", IsActive = true },

                // Transaction (اسناد حسابداری)
                new Permission { Name = "Transaction.View",   DisplayName = "مشاهده اسناد", GroupName = "Accounting", IsActive = true },
                new Permission { Name = "Transaction.Create", DisplayName = "ثبت سند",     GroupName = "Accounting", IsActive = true },
                new Permission { Name = "Transaction.Edit",   DisplayName = "ویرایش سند",   GroupName = "Accounting", IsActive = true },
                new Permission { Name = "Transaction.Delete", DisplayName = "حذف سند",      GroupName = "Accounting", IsActive = true },
                new Permission { Name = "Transaction.Post", DisplayName = "قطعی کردن سند", GroupName = "Accounting", IsActive = true },

                new Permission { Name = "Invoice.View",   DisplayName = "مشاهده فاکتورها", GroupName = "Invoicing", IsActive = true },
                new Permission { Name = "Invoice.Create", DisplayName = "ثبت فاکتور",     GroupName = "Invoicing", IsActive = true },
                new Permission { Name = "Invoice.Edit",   DisplayName = "ویرایش فاکتور",   GroupName = "Invoicing", IsActive = true },
                new Permission { Name = "Invoice.Delete", DisplayName = "حذف فاکتور",      GroupName = "Invoicing", IsActive = true },
                new Permission { Name = "Invoice.Post",   DisplayName = "قطعی کردن فاکتور", GroupName = "Invoicing", IsActive = true },
            
                // Cheque
                new Permission { Name = "Cheque.View",   DisplayName = "مشاهده چک‌ها",  GroupName = "Banking", IsActive = true },
                new Permission { Name = "Cheque.Create", DisplayName = "ثبت چک",      GroupName = "Banking", IsActive = true },
                new Permission { Name = "Cheque.Edit",   DisplayName = "ویرایش چک",    GroupName = "Banking", IsActive = true },
                new Permission { Name = "Cheque.Delete", DisplayName = "حذف چک",       GroupName = "Banking", IsActive = true },
            
                // Payment
                new Permission { Name = "Payment.View",   DisplayName = "مشاهده پرداخت‌ها", GroupName = "Banking", IsActive = true },
                new Permission { Name = "Payment.Create", DisplayName = "ثبت پرداخت",      GroupName = "Banking", IsActive = true },
                new Permission { Name = "Payment.Edit",   DisplayName = "ویرایش پرداخت",    GroupName = "Banking", IsActive = true },
                new Permission { Name = "Payment.Delete", DisplayName = "حذف پرداخت",       GroupName = "Banking", IsActive = true },
                new Permission { Name = "Payment.Post",   DisplayName = "قطعی پرداخت",      GroupName = "Banking", IsActive = true },
            };

            foreach (var permission in permissions)
            {
                var exists = await context.Permissions
                    .AnyAsync(x => x.Name == permission.Name);

                if (!exists)
                {
                    context.Permissions.Add(permission);
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedRolePermissionsAsync(
            ApplicationDbContext context,
            RoleManager<IdentityRole> roleManager)
        {
            var adminRole = await roleManager.FindByNameAsync("Admin");

            if (adminRole == null)
                return;

            var allPermissionIds = await context.Permissions
                .Select(x => x.Id)
                .ToListAsync();

            var existingPermissionIds = await context.RolePermissions
                .Where(x => x.RoleId == adminRole.Id)
                .Select(x => x.PermissionId)
                .ToListAsync();

            var permissionsToAdd = allPermissionIds
                .Except(existingPermissionIds)
                .ToList();

            foreach (var permissionId in permissionsToAdd)
            {
                context.RolePermissions.Add(
                    new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionId = permissionId
                    });
            }

            if (permissionsToAdd.Count > 0)
            {
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedAdminUserAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration)
        {
            var email = configuration["SeedAdmin:Email"];
            var password = configuration["SeedAdmin:Password"];
            var fullName = configuration["SeedAdmin:FullName"];

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fullName))
            {
                throw new Exception(
                    "اطلاعات SeedAdmin در User Secrets تنظیم نشده است.");
            }

            var adminRole = await roleManager.FindByNameAsync("Admin");

            if (adminRole == null)
                throw new Exception(
                    "Role 'Admin' پیدا نشد.");

            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    FullName = fullName,
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(
                    user,
                    password);

                if (!result.Succeeded)
                {
                    throw new Exception(
                        "خطا در ایجاد Admin User: " +
                        string.Join(", ",
                            result.Errors.Select(x => x.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(
                    user,
                    "Admin"))
            {
                var result = await userManager.AddToRoleAsync(
                    user,
                    "Admin");

                if (!result.Succeeded)
                {
                    throw new Exception(
                        "خطا در اختصاص Role Admin: " +
                        string.Join(", ",
                            result.Errors.Select(x => x.Description)));
                }
            }
        }
        private static async Task SeedMenusAsync(ApplicationDbContext context)
        {
            // ============================================
            // ۱. منوهای والد
            // ============================================
            var parents = new[]
            {
        new Menu { Title = "کاربران و دسترسی‌ها", Url = null, Icon = "fa-solid fa-users", Permission = null, SortOrder = 1, IsActive = true },
        new Menu { Title = "اطلاعات پایه", Url = null, Icon = "fa-solid fa-list", Permission = null, SortOrder = 2, IsActive = true },
        new Menu { Title = "حسابداری", Url = null, Icon = "fa-solid fa-calculator", Permission = null, SortOrder = 3, IsActive = true },
        new Menu { Title = "طرف حساب‌ها", Url = null, Icon = "fa-solid fa-address-book", Permission = null, SortOrder = 4, IsActive = true },
        new Menu { Title = "انبار", Url = null, Icon = "fa-solid fa-cubes", Permission = null, SortOrder = 5, IsActive = true },
        new Menu { Title = "گزارشات", Url = null, Icon = "fa-solid fa-chart-bar", Permission = null, SortOrder = 6, IsActive = true },
        new Menu { Title = "فاکتورها", Url = null, Icon = "fa-solid fa-file-invoice", Permission = null, SortOrder = 7, IsActive = true },
        new Menu { Title = "امور بانکی", Url = null, Icon = "fa-solid fa-building-columns", Permission = null, SortOrder = 8, IsActive = true }
    };

            foreach (var parent in parents)
            {
                var exists = await context.Menus.AnyAsync(x => x.Title == parent.Title && x.ParentId == null);
                if (!exists) context.Menus.Add(parent);
            }
            await context.SaveChangesAsync();

            // ============================================
            // ۲. پیدا کردن Parent ها
            // ============================================
            var usersParent = await context.Menus.FirstAsync(x => x.Title == "کاربران و دسترسی‌ها" && x.ParentId == null);
            var baseParent = await context.Menus.FirstAsync(x => x.Title == "اطلاعات پایه" && x.ParentId == null);
            var accountingParent = await context.Menus.FirstAsync(x => x.Title == "حسابداری" && x.ParentId == null);
            var contactsParent = await context.Menus.FirstAsync(x => x.Title == "طرف حساب‌ها" && x.ParentId == null);
            var inventoryParent = await context.Menus.FirstAsync(x => x.Title == "انبار" && x.ParentId == null);
            var reportsParent = await context.Menus.FirstAsync(x => x.Title == "گزارشات" && x.ParentId == null);
            var invoicingParent = await context.Menus.FirstAsync(x => x.Title == "فاکتورها" && x.ParentId == null);
            var bankingParent = await context.Menus.FirstAsync(x => x.Title == "امور بانکی" && x.ParentId == null);

            // ============================================
            // ۳. زیرمنوها
            // ============================================
            var children = new[]
            {
                // کاربران و دسترسی‌ها
                new Menu { Title = "کاربران", Url = "/Admin/Users", Icon = "fa-solid fa-user", Permission = "User.View", ParentId = usersParent.Id, SortOrder = 1, IsActive = true },
                new Menu { Title = "نقش‌ها", Url = "/Admin/Roles", Icon = "fa-solid fa-user-tag", Permission = "Role.View", ParentId = usersParent.Id, SortOrder = 2, IsActive = true },
                new Menu { Title = "دسترسی‌ها", Url = "/Admin/Permissions", Icon = "fa-solid fa-key", Permission = "Permission.View", ParentId = usersParent.Id, SortOrder = 3, IsActive = true },
                new Menu { Title = "منوها", Url = "/Admin/Menus", Icon = "fa-solid fa-bars", Permission = "Menu.View", ParentId = usersParent.Id, SortOrder = 4, IsActive = true },

                // اطلاعات پایه
                new Menu { Title = "شرکت‌ها", Url = "/Tenancy/Tenant", Icon = "fa-solid fa-building", Permission = "Tenancy.View", ParentId = baseParent.Id, SortOrder = 1, IsActive = true },

                // حسابداری
                new Menu { Title = "دسته‌بندی حساب‌ها", Url = "/Accounting/AccountCategories", Icon = "fa-solid fa-folder", Permission = "AccountCategory.View", ParentId = accountingParent.Id, SortOrder = 1, IsActive = true },
                new Menu { Title = "حساب‌ها", Url = "/Accounting/Accounts", Icon = "fa-solid fa-building-columns", Permission = "Account.View", ParentId = accountingParent.Id, SortOrder = 2, IsActive = true },
                new Menu { Title = "گروه تراکنش‌ها", Url = "/Accounting/TransactionGroups", Icon = "fa-solid fa-tags", Permission = "TransactionGroup.View", ParentId = accountingParent.Id, SortOrder = 3, IsActive = true },
                new Menu { Title = "اسناد حسابداری", Url = "/Accounting/Transactions", Icon = "fa-solid fa-file-lines", Permission = "Transaction.View", ParentId = accountingParent.Id, SortOrder = 4, IsActive = true },

                // طرف حساب‌ها
                new Menu { Title = "لیست طرف حساب‌ها", Url = "/Contacts/Contacts", Icon = "fa-solid fa-address-book", Permission = "Contact.View", ParentId = contactsParent.Id, SortOrder = 1, IsActive = true },

                // انبار
                new Menu { Title = "واحدها", Url = "/Inventory/Units", Icon = "fa-solid fa-balance-scale", Permission = "Unit.View", ParentId = inventoryParent.Id, SortOrder = 1, IsActive = true },
                new Menu { Title = "دسته‌بندی کالا", Url = "/Inventory/ProductCategories", Icon = "fa-solid fa-folder-open", Permission = "ProductCategory.View", ParentId = inventoryParent.Id, SortOrder = 2, IsActive = true },
                new Menu { Title = "کالاها", Url = "/Inventory/Products", Icon = "fa-solid fa-cube", Permission = "Product.View", ParentId = inventoryParent.Id, SortOrder = 3, IsActive = true },

                // گزارشات
                new Menu { Title = "گزارش معین", Url = "/Accounting/Reports/Ledger", Icon = "fa-solid fa-book", Permission = "Report.Ledger", ParentId = reportsParent.Id, SortOrder = 1, IsActive = true },
                new Menu { Title = "گزارش تفصیلی", Url = "/Accounting/Reports/Detailed",    Icon = "fa-solid fa-list-ul",    Permission = "Report.Ledger", ParentId = reportsParent.Id,  SortOrder = 2,    IsActive = true},
                new Menu{    Title = "گزارش طرف حساب",    Url = "/Accounting/Reports/Contact",    Icon = "fa-solid fa-address-book",    Permission = "Report.Ledger",    ParentId = reportsParent.Id,    SortOrder = 3,    IsActive = true},

                // فاکتورها
                new Menu { Title = "پیش‌فاکتورها", Url = "/Invoicing/Invoices?invoiceType=1", Icon = "fa-solid fa-file-pen", Permission = "Invoice.View", ParentId = invoicingParent.Id, SortOrder = 1, IsActive = true },
                new Menu { Title = "فاکتورهای فروش", Url = "/Invoicing/Invoices?invoiceType=2", Icon = "fa-solid fa-cart-shopping", Permission = "Invoice.View", ParentId = invoicingParent.Id, SortOrder = 2, IsActive = true },
                new Menu { Title = "فاکتورهای خرید", Url = "/Invoicing/Invoices?invoiceType=3", Icon = "fa-solid fa-truck", Permission = "Invoice.View", ParentId = invoicingParent.Id, SortOrder = 3, IsActive = true },

                // 🆕 امور بانکی
                new Menu { Title = "چک‌های دریافتنی", Url = "/Banking/Cheques?direction=1", Icon = "fa-solid fa-money-check-dollar", Permission = "Cheque.View", ParentId = bankingParent.Id, SortOrder = 1, IsActive = true },
                new Menu { Title = "چک‌های پرداختنی", Url = "/Banking/Cheques?direction=2", Icon = "fa-solid fa-money-check", Permission = "Cheque.View", ParentId = bankingParent.Id, SortOrder = 2, IsActive = true },
                new Menu { Title = "دریافت‌ها", Url = "/Banking/Payments?paymentType=1", Icon = "fa-solid fa-hand-holding-dollar", Permission = "Payment.View", ParentId = bankingParent.Id, SortOrder = 3, IsActive = true },
                new Menu { Title = "پرداخت‌ها", Url = "/Banking/Payments?paymentType=2", Icon = "fa-solid fa-credit-card", Permission = "Payment.View", ParentId = bankingParent.Id, SortOrder = 4, IsActive = true }
            };

            foreach (var child in children)
            {
                var exists = await context.Menus.AnyAsync(x => x.Url == child.Url);
                if (!exists) context.Menus.Add(child);
            }

            await context.SaveChangesAsync();
        }
    }
}