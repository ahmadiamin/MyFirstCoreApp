using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Projects;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Projects;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Code)
            .HasMaxLength(50);

        builder.Property(x => x.Address)
            .HasMaxLength(1000);

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.HoldReason)
            .HasMaxLength(1000);

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Contact)
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectManager)
            .WithMany()
            .HasForeignKey(x => x.ProjectManagerUserTenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Members)
            .WithOne(x => x.Project)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Stages)
            .WithOne(x => x.Project)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasFilter("[Code] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.Status });

        builder.HasIndex(x => new { x.TenantId, x.ContactId });

        builder.HasIndex(x => new { x.TenantId, x.ProjectManagerUserTenantId });

        builder.HasIndex(x => new { x.TenantId, x.StartDate });
    }
}