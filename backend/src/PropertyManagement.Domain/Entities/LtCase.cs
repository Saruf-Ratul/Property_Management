using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// Landlord-Tenant case overlay on a Case.
/// </summary>
public class LtCase : TenantEntity
{
    public Guid CaseId { get; set; }
    public Case Case { get; set; } = null!;

    public string? PremisesAddressLine1 { get; set; }
    public string? PremisesAddressLine2 { get; set; }
    public string? PremisesCity { get; set; }
    public string? PremisesCounty { get; set; }
    public string? PremisesState { get; set; } = "NJ";
    public string? PremisesPostalCode { get; set; }

    public string? LandlordName { get; set; }
    public string? LandlordAddress { get; set; }

    public decimal? RentDue { get; set; }
    public decimal? LateFees { get; set; }
    public decimal? OtherCharges { get; set; }
    public decimal? TotalDue { get; set; }
    public DateTime? RentDueAsOf { get; set; }

    public bool IsRegisteredMultipleDwelling { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime? RegistrationDate { get; set; }

    public bool AttorneyReviewed { get; set; }
    public DateTime? AttorneyReviewedAtUtc { get; set; }
    public Guid? AttorneyReviewedById { get; set; }
    public UserProfile? AttorneyReviewedBy { get; set; }

    public ICollection<LtCaseFormData> FormData { get; set; } = new List<LtCaseFormData>();
    public ICollection<LtCaseDocument> Documents { get; set; } = new List<LtCaseDocument>();
}

public class LtCaseFormData : TenantEntity
{
    public Guid LtCaseId { get; set; }
    public LtCase LtCase { get; set; } = null!;

    public LtFormType FormType { get; set; }

    /// <summary>Form-field-name → value JSON, hand-edited by paralegal/attorney before generation.</summary>
    public string DataJson { get; set; } = "{}";

    public bool IsApproved { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public UserProfile? ApprovedBy { get; set; }
}

public class LtCaseDocument : TenantEntity
{
    public Guid LtCaseId { get; set; }
    public LtCase LtCase { get; set; } = null!;

    public LtFormType FormType { get; set; }
    public string FileName { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;
}
