using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// A property-management company client of the law firm.
/// </summary>
public class Client : TenantEntity
{
    public string Name { get; set; } = null!;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;

    public LawFirm LawFirm { get; set; } = null!;
    public ICollection<PmsIntegration> PmsIntegrations { get; set; } = new List<PmsIntegration>();
    public ICollection<UserProfile> Users { get; set; } = new List<UserProfile>();
    public ICollection<Case> Cases { get; set; } = new List<Case>();
}
