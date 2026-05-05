using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

public class PmsTenant : TenantEntity
{
    public Guid IntegrationId { get; set; }
    public PmsIntegration Integration { get; set; } = null!;

    public string ExternalId { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PmsLease> Leases { get; set; } = new List<PmsLease>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
