namespace PropertyManagement.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Audit columns — populated automatically by AppDbContext.SaveChangesAsync
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Soft-delete columns — DbContext converts Remove() into a soft-delete and a global query filter hides them.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

public abstract class TenantEntity : Entity
{
    public Guid LawFirmId { get; set; }
}
