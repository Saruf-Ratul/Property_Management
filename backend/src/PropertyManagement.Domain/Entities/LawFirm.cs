using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

public class LawFirm : Entity
{
    public string Name { get; set; } = null!;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? BarNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserProfile> Users { get; set; } = new List<UserProfile>();
    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<Case> Cases { get; set; } = new List<Case>();
    public AttorneySetting? AttorneySetting { get; set; }
}
