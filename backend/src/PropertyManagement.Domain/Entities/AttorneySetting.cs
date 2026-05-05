using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities;

public class AttorneySetting : Entity
{
    public Guid LawFirmId { get; set; }
    public LawFirm LawFirm { get; set; } = null!;

    public string? FirmDisplayName { get; set; }
    public string? AttorneyName { get; set; }
    public string? BarNumber { get; set; }
    public string? AttorneyEmail { get; set; }
    public string? AttorneyPhone { get; set; }
    public string? OfficeAddressLine1 { get; set; }
    public string? OfficeAddressLine2 { get; set; }
    public string? OfficeCity { get; set; }
    public string? OfficeState { get; set; }
    public string? OfficePostalCode { get; set; }
    public string? SignatureImagePath { get; set; }
    public string? DefaultCourtVenue { get; set; }
}
