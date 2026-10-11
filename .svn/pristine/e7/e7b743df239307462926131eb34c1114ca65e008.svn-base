using MyFirstCoreApp.Domain.Common.Interfaces;

namespace MyFirstCoreApp.Domain.Common;

public abstract class BaseEntity : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}   