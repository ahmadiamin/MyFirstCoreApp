using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Common.Interfaces;

namespace MyFirstCoreApp.Infrastructure.Persistence.Interceptors;

/// <summary>
/// قبل از هر SaveChanges سه کار را در «یک نقطه» انجام می‌دهد:
///  1) فیلدهای Audit (CreatedAt/By و UpdatedAt/By)
///  2) تعیین و کنترل TenantId (نوشتن در شرکتِ دیگر ممنوع است)
///  3) تبدیل حذف واقعی به حذف نرم (برای موجودیت‌های ISoftDeletable)
///
/// قواعد TenantId:
///  - رکورد جدید با TenantId = 0  ←  از شرکت فعال پر می‌شود؛ اگر شرکت فعال نباشد خطا می‌دهد.
///  - اگر شرکت فعال مشخص است و TenantId رکورد با آن فرق دارد  ←  خطا (Fail-closed).
///  - اگر شرکت فعال مشخص نیست (Seed، عملیات سیستمی) و TenantId صریح داده شده  ←  مجاز.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public AuditSaveChangesInterceptor(ICurrentUser currentUser, ICurrentTenant currentTenant)
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyRules(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;
        var currentTenantId = _currentTenant.TenantId;

        // ToList: چون در حلقه وضعیت Entryها (حذف نرم) تغییر می‌کند
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.Entity is TenantEntity tenantEntity)
                EnforceTenant(entry, tenantEntity, currentTenantId);

            switch (entry.State)
            {
                case EntityState.Added when entry.Entity is IAuditableEntity created:
                    created.CreatedAt = now;
                    created.CreatedBy = userId;
                    break;

                case EntityState.Modified when entry.Entity is IAuditableEntity:
                    // جلوگیری از بازنویسی فیلدهای ایجاد (مثلاً وقتی Update روی Entity جداافتاده صدا زده می‌شود)
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditableEntity.UpdatedBy)).CurrentValue = userId;
                    break;

                case EntityState.Deleted when entry.Entity is ISoftDeletable:
                    SoftDelete(entry, now, userId);
                    break;
            }
        }
    }

    private static void EnforceTenant(EntityEntry entry, TenantEntity entity, int? currentTenantId)
    {
        if (entry.State == EntityState.Added && entity.TenantId == 0)
        {
            if (currentTenantId is null)
                throw new InvalidOperationException(
                    "شرکت فعال مشخص نیست؛ ذخیره‌ی رکورد بدون شرکت مجاز نیست.");

            entity.TenantId = currentTenantId.Value;
            return;
        }

        if (currentTenantId.HasValue && entity.TenantId != currentTenantId.Value)
        {
            throw new InvalidOperationException(
                $"تلاش برای ذخیره‌ی رکورد شرکت دیگر ({entity.GetType().Name}، TenantId={entity.TenantId}).");
        }
    }

    private static void SoftDelete(EntityEntry entry, DateTime now, string? userId)
    {
        // به‌جای DELETE، فقط یک UPDATE روی ستون‌های حذف نرم زده می‌شود
        // (در نتیجه Cascade دیتابیس هم فعال نمی‌شود).
        entry.State = EntityState.Unchanged;
        entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
        entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;
        entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = userId;
    }
}
