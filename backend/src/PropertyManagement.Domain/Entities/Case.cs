using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

public class Case : TenantEntity
{
    public string CaseNumber { get; set; } = null!;
    public string Title { get; set; } = null!;
    public CaseType CaseType { get; set; } = CaseType.LandlordTenantEviction;

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public Guid CaseStageId { get; set; }
    public CaseStage CaseStage { get; set; } = null!;

    public Guid CaseStatusId { get; set; }
    public CaseStatus CaseStatus { get; set; } = null!;

    public Guid? AssignedAttorneyId { get; set; }
    public UserProfile? AssignedAttorney { get; set; }

    public Guid? AssignedParalegalId { get; set; }
    public UserProfile? AssignedParalegal { get; set; }

    /// <summary>Optional snapshot links to PMS data at intake.</summary>
    public Guid? PmsLeaseId { get; set; }
    public PmsLease? PmsLease { get; set; }
    public Guid? PmsTenantId { get; set; }
    public PmsTenant? PmsTenant { get; set; }
    public Guid? PmsPropertyId { get; set; }
    public PmsProperty? PmsProperty { get; set; }
    public Guid? PmsUnitId { get; set; }
    public PmsUnit? PmsUnit { get; set; }

    /// <summary>JSON snapshot of PMS state when case filing began. Once set, never auto-overwritten.</summary>
    public string? PmsSnapshotJson { get; set; }
    public DateTime? PmsSnapshotTakenAtUtc { get; set; }

    /// <summary>Outstanding balance / amount in controversy at intake.</summary>
    public decimal? AmountInControversy { get; set; }

    public DateTime? FiledOnUtc { get; set; }
    public DateTime? CourtDateUtc { get; set; }
    public string? CourtDocketNumber { get; set; }
    public string? CourtVenue { get; set; }
    public string? Outcome { get; set; }
    public string? Description { get; set; }

    public LawFirm LawFirm { get; set; } = null!;
    public LtCase? LtCase { get; set; }
    public ICollection<CaseDocument> Documents { get; set; } = new List<CaseDocument>();
    public ICollection<CaseComment> Comments { get; set; } = new List<CaseComment>();
    public ICollection<CasePayment> Payments { get; set; } = new List<CasePayment>();
    public ICollection<CaseActivity> Activities { get; set; } = new List<CaseActivity>();
    public ICollection<GeneratedDocument> GeneratedDocuments { get; set; } = new List<GeneratedDocument>();
}
