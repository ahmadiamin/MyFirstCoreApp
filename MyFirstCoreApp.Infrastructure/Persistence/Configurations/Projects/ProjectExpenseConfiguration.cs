using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Projects;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Projects;

public class ProjectExpenseConfiguration : IEntityTypeConfiguration<ProjectExpense>
{
    public void Configure(EntityTypeBuilder<ProjectExpense> b)
    {
        b.ToTable("ProjectExpenses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Reference).HasMaxLength(100);
        b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ExpenseAccount).WithMany().HasForeignKey(x => x.ExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne(x => x.ProjectExpense).HasForeignKey(x => x.ProjectExpenseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProjectExpensePaymentConfiguration : IEntityTypeConfiguration<ProjectExpensePayment>
{
    public void Configure(EntityTypeBuilder<ProjectExpensePayment> b)
    {
        b.ToTable("ProjectExpensePayments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TenantId, x.ProjectExpenseId });
    }
}
