using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Inventory;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Inventory;

public sealed class CompanyAssetAssignmentConfiguration : IEntityTypeConfiguration<CompanyAssetAssignment>
{
    public void Configure(EntityTypeBuilder<CompanyAssetAssignment> builder)
    {
        builder.ToTable("CompanyAssetAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssignedByUserId).HasMaxLength(450);
        builder.Property(x => x.ReturnedByUserId).HasMaxLength(450);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.ReturnNotes).HasMaxLength(2000);
        builder.HasOne(x => x.UserTenant).WithMany().HasForeignKey(x => x.UserTenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.CompanyAssetId, x.AssignedDate });
        builder.HasIndex(x => x.CompanyAssetId).IsUnique().HasFilter("[ReturnedDate] IS NULL AND [IsDeleted] = 0");
    }
}
