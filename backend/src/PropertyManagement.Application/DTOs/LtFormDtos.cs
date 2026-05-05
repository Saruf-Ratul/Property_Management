using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.DTOs;

// ────────────────────────────────────────────────────────────────────────────
// Existing (kept for backward compatibility with the /api/cases/{id}/lt-forms route)
// ────────────────────────────────────────────────────────────────────────────

public record LtFormDataDto(
    Guid Id,
    Guid LtCaseId,
    LtFormType FormType,
    string DataJson,
    bool IsApproved,
    DateTime? ApprovedAtUtc,
    string? ApprovedBy);

public record SaveLtFormDataRequest(LtFormType FormType, string DataJson);
public record ApproveLtFormRequest(LtFormType FormType);

public record GenerateLtPdfRequest(LtFormType FormType, bool ForceRegenerate);
public record GenerateLtPacketRequest(IReadOnlyList<LtFormType> Forms);

public record GeneratedDocumentDto(
    Guid Id,
    Guid CaseId,
    LtFormType? FormType,
    string FileName,
    int Version,
    bool IsMergedPacket,
    bool IsCurrent,
    long SizeBytes,
    string? GeneratedBy,
    DateTime GeneratedAtUtc);

/// <summary>Auto-fill payload returned from the backend after merging PMS snapshot + attorney settings.</summary>
public record LtFormAutofillResponse(LtFormType FormType, IReadOnlyDictionary<string, string?> Fields);

// ────────────────────────────────────────────────────────────────────────────
// LT Case list / summary (new /api/lt-cases route)
// ────────────────────────────────────────────────────────────────────────────

public record LtCaseSummaryDto(
    Guid Id,
    Guid CaseId,
    string CaseNumber,
    string CaseTitle,
    string ClientName,
    Guid ClientId,
    CaseStageCode StageCode,
    string StageName,
    CaseStatusCode StatusCode,
    string StatusName,
    LtFormPhase Phase,
    string PhaseName,
    bool AttorneyReviewed,
    int FormsApproved,
    int FormsTotal,
    int GeneratedFormCount,
    int GeneratedPacketCount,
    DateTime? LatestGeneratedAtUtc,
    decimal? TotalDue,
    DateTime CreatedAtUtc);

public record LtCaseDetailDto(
    Guid Id,
    Guid CaseId,
    string CaseNumber,
    string CaseTitle,
    string ClientName,
    Guid ClientId,
    LtFormPhase Phase,
    bool AttorneyReviewed,
    DateTime? AttorneyReviewedAtUtc,
    string? AttorneyReviewedByName,
    string? PremisesAddressLine1,
    string? PremisesCity,
    string? PremisesCounty,
    string? PremisesState,
    string? PremisesPostalCode,
    string? LandlordName,
    decimal? RentDue,
    decimal? LateFees,
    decimal? OtherCharges,
    decimal? TotalDue,
    DateTime? RentDueAsOf,
    bool IsRegisteredMultipleDwelling,
    string? RegistrationNumber);

// ────────────────────────────────────────────────────────────────────────────
// Structured form-data (the spec's "FormData model")
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// All sections of the LT case form bundle. Stored once per LT case and used
/// as the source of truth when generating any of the seven NJ forms.
/// Fields are intentionally nullable so the wizard can save partial data.
/// </summary>
public class LtFormDataSections
{
    public CaptionSection Caption { get; set; } = new();
    public AttorneySection Attorney { get; set; } = new();
    public PlaintiffSection Plaintiff { get; set; } = new();
    public DefendantSection Defendant { get; set; } = new();
    public PremisesSection Premises { get; set; } = new();
    public LeaseSection Lease { get; set; } = new();
    public RentOwedSection RentOwed { get; set; } = new();
    public AdditionalRentSection AdditionalRent { get; set; } = new();
    public FilingFeeSection FilingFee { get; set; } = new();
    public SubsidyRentControlSection Subsidy { get; set; } = new();
    public RequiredNoticesSection Notices { get; set; } = new();
    public RegistrationOwnershipSection Registration { get; set; } = new();
    public CertificationSection Certification { get; set; } = new();
    public WarrantSection Warrant { get; set; } = new();
}

public class CaptionSection
{
    public string? CourtName { get; set; }
    public string? CourtVenue { get; set; }
    public string? CountyName { get; set; }
    public string? DocketNumber { get; set; }
    public string? CaseNumber { get; set; }
    public DateTime? FilingDate { get; set; }
}

public class AttorneySection
{
    public string? FirmName { get; set; }
    public string? AttorneyName { get; set; }
    public string? BarNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? OfficeAddressLine1 { get; set; }
    public string? OfficeAddressLine2 { get; set; }
    public string? OfficeCity { get; set; }
    public string? OfficeState { get; set; }
    public string? OfficePostalCode { get; set; }
}

public class PlaintiffSection
{
    public string? Name { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsCorporate { get; set; }
}

public class DefendantSection
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AdditionalOccupants { get; set; }
}

public class PremisesSection
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? County { get; set; }
    public string? State { get; set; } = "NJ";
    public string? PostalCode { get; set; }
    public string? UnitNumber { get; set; }
}

public class LeaseSection
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsMonthToMonth { get; set; }
    public bool IsWritten { get; set; } = true;
    public decimal? MonthlyRent { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public DateTime? RentDueDay { get; set; }
}

public class RentOwedSection
{
    public DateTime? AsOfDate { get; set; }
    public decimal? PriorBalance { get; set; }
    public decimal? CurrentMonthRent { get; set; }
    public decimal? Total { get; set; }
}

public class AdditionalRentSection
{
    public decimal? LateFees { get; set; }
    public decimal? AttorneyFees { get; set; }
    public decimal? OtherCharges { get; set; }
    public string? OtherChargesDescription { get; set; }
}

public class FilingFeeSection
{
    public decimal? AmountClaimed { get; set; }
    public decimal? FilingFee { get; set; } = 50m;
    public bool ApplyForFeeWaiver { get; set; }
}

public class SubsidyRentControlSection
{
    public bool IsRentControlled { get; set; }
    public bool IsSubsidized { get; set; }
    public string? SubsidyProgram { get; set; }
}

public class RequiredNoticesSection
{
    public bool NoticeToCeaseServed { get; set; }
    public DateTime? NoticeToCeaseDate { get; set; }
    public bool NoticeToQuitServed { get; set; }
    public DateTime? NoticeToQuitDate { get; set; }
    public string? ServiceMethod { get; set; }
}

public class RegistrationOwnershipSection
{
    public bool IsRegisteredMultipleDwelling { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public bool IsOwnerOccupied { get; set; }
    public int? UnitCountInBuilding { get; set; }
}

public class CertificationSection
{
    public string? CertifierName { get; set; }
    public string? CertifierTitle { get; set; }
    public DateTime? CertificationDate { get; set; }
    public bool AttorneyReviewed { get; set; }
}

public class WarrantSection
{
    public DateTime? JudgmentDate { get; set; }
    public string? JudgmentDocketNumber { get; set; }
    public DateTime? RequestedExecutionDate { get; set; }
    public bool TenantStillInPossession { get; set; }
    public bool PaymentReceivedSinceJudgment { get; set; }
    public decimal? AmountPaidSinceJudgment { get; set; }
}

// ────────────────────────────────────────────────────────────────────────────
// Form data API contracts
// ────────────────────────────────────────────────────────────────────────────

/// <summary>The bundle returned by GET /api/lt-cases/{id}/form-data.</summary>
public record LtFormBundleDto(
    Guid LtCaseId,
    LtFormDataSections Sections,
    /// <summary>Per-form approval flags (Lawyer/FirmAdmin must approve each form before final packet generation).</summary>
    IReadOnlyDictionary<LtFormType, LtFormApprovalDto> Approvals,
    DateTime? UpdatedAtUtc);

public record LtFormApprovalDto(bool IsApproved, DateTime? ApprovedAtUtc, string? ApprovedBy);

public record SaveLtFormBundleRequest(LtFormDataSections Sections);

public record GenerateFormRequest(
    /// <summary>Optional one-off field overrides applied on top of the saved bundle for this generation.</summary>
    IReadOnlyDictionary<string, string?>? Overrides,
    /// <summary>If true, return PDF bytes inline without persisting a GeneratedDocument row.</summary>
    bool Preview = false);

public record GeneratePacketRequestNew(
    IReadOnlyList<LtFormType>? Forms,
    bool RequireApproval = true);

// ────────────────────────────────────────────────────────────────────────────
// Form schema metadata (front-end uses to render the structured wizard)
// ────────────────────────────────────────────────────────────────────────────

public record LtFormSchemaDto(
    LtFormType FormType,
    string DisplayName,
    LtFormPhase Phase,
    bool IsPublicCourtForm,
    /// <summary>Sections relevant to this form (others can be hidden in the UI).</summary>
    IReadOnlyList<string> RelevantSections);

// ────────────────────────────────────────────────────────────────────────────
// Validation summary
// ────────────────────────────────────────────────────────────────────────────

public record LtValidationIssue(string Severity, string Section, string Field, string Message);

public record LtValidationSummary(
    bool IsValid,
    IReadOnlyList<LtValidationIssue> Issues,
    IReadOnlyList<RedactionFindingDto> RedactionFindings);

public record RedactionFindingDto(string FieldName, string Pattern, string Sample);
