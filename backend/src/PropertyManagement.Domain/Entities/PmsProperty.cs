using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

public class PmsProperty : TenantEntity
{
    public Guid IntegrationId { get; set; }
    public PmsIntegration Integration { get; set; } = null!;

    public string ExternalId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? County { get; set; }
    public int? UnitCount { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PmsUnit> Units { get; set; } = new List<PmsUnit>();
}
