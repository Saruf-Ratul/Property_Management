using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

public class PmsUnit : TenantEntity
{
    public Guid PropertyId { get; set; }
    public PmsProperty Property { get; set; } = null!;

    public string ExternalId { get; set; } = null!;
    public string UnitNumber { get; set; } = null!;
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public decimal? SquareFeet { get; set; }
    public decimal? MarketRent { get; set; }
    public bool IsOccupied { get; set; }

    public ICollection<PmsLease> Leases { get; set; } = new List<PmsLease>();
}
