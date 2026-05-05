using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.DTOs;

public record CaseListItemDto(
    Guid Id,
    string CaseNumber,
    string Title,
    CaseType CaseType,
    string ClientName,
    Guid ClientId,
    string StageName,
    CaseStageCode StageCode,
    string StatusName,
    CaseStatusCode StatusCode,
    string? AssignedAttorney,
    DateTime? FiledOnUtc,
    DateTime? CourtDateUtc,
    decimal? AmountInControversy,
    DateTime CreatedAtUtc);

public record CaseDetailDto(
    Guid Id,
    string CaseNumber,
    string Title,
    CaseType CaseType,
    Guid ClientId,
    string ClientName,
    Guid CaseStageId,
    string StageName,
    CaseStageCode StageCode,
    Guid CaseStatusId,
    string StatusName,
    CaseStatusCode StatusCode,
    Guid? AssignedAttorneyId,
    string? AssignedAttorney,
    Guid? AssignedParalegalId,
    string? AssignedParalegal,
    Guid? PmsLeaseId,
    Guid? PmsTenantId,
    Guid? PmsPropertyId,
    Guid? PmsUnitId,
    string? PmsSnapshotJson,
    DateTime? PmsSnapshotTakenAtUtc,
    decimal? AmountInControversy,
    DateTime? FiledOnUtc,
    DateTime? CourtDateUtc,
    string? CourtDocketNumber,
    string? CourtVenue,
    string? Outcome,
    string? Description,
    DateTime CreatedAtUtc,
    LtCaseDto? LtCase);

public record LtCaseDto(
    Guid Id,
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
    string? RegistrationNumber,
    bool AttorneyReviewed);

public record CreateCaseRequest(
    string Title,
    CaseType CaseType,
    Guid ClientId,
    Guid? AssignedAttorneyId,
    Guid? AssignedParalegalId,
    Guid? PmsLeaseId,
    Guid? PmsTenantId,
    decimal? AmountInControversy,
    string? Description);

/// <summary>
/// Create a case from a selected PMS lease. The server will look up the related tenant/unit/property
/// automatically and copy a snapshot of all PMS data onto the case.
/// </summary>
public record CreateCaseFromPmsRequest(
    Guid PmsLeaseId,
    Guid ClientId,
    string? Title,
    CaseType CaseType,
    Guid? AssignedAttorneyId,
    Guid? AssignedParalegalId,
    string? Description,
    /// <summary>Operator confirms the lease has been verified, tenant has been served notice, etc.</summary>
    bool ComplianceConfirmed);

public record UpdateCaseRequest(
    string Title,
    Guid? AssignedAttorneyId,
    Guid? AssignedParalegalId,
    string? CourtVenue,
    DateTime? CourtDateUtc,
    string? CourtDocketNumber,
    string? Outcome,
    string? Description,
    decimal? AmountInControversy);

public record ChangeCaseStageRequest(CaseStageCode StageCode, string? Note);
public record ChangeCaseStatusRequest(CaseStatusCode StatusCode, string? Note);
public record AssignCaseRequest(Guid? AttorneyId, Guid? ParalegalId, string? Note);
public record CloseCaseRequest(string? Outcome, string? Notes);

public record CaseSnapshotDto(
    Guid CaseId,
    DateTime? TakenAtUtc,
    /// <summary>Decoded snapshot — null if no snapshot has been taken yet.</summary>
    CaseSnapshotData? Data);

public record CaseSnapshotData(
    SnapshotProperty? Property,
    SnapshotUnit? Unit,
    SnapshotTenant? Tenant,
    SnapshotLease? Lease,
    IReadOnlyList<SnapshotLedger>? Ledger);

public record SnapshotProperty(string? Name, string? AddressLine1, string? City, string? State, string? PostalCode, string? County);
public record SnapshotUnit(string? UnitNumber, int? Bedrooms, int? Bathrooms, decimal? MarketRent);
public record SnapshotTenant(string? FirstName, string? LastName, string? Email, string? Phone);
public record SnapshotLease(DateTime? StartDate, DateTime? EndDate, decimal? MonthlyRent, decimal? SecurityDeposit, bool IsMonthToMonth, decimal? CurrentBalance);
public record SnapshotLedger(DateTime PostedDate, string Category, string? Description, decimal Amount, decimal Balance, bool IsCharge, bool IsPayment);

public record CaseCommentDto(
    Guid Id,
    Guid CaseId,
    string AuthorName,
    string Body,
    bool IsInternal,
    DateTime CreatedAtUtc);

public record CreateCaseCommentRequest(string Body, bool IsInternal);

public record CasePaymentDto(
    Guid Id,
    DateTime ReceivedOnUtc,
    decimal Amount,
    string? Method,
    string? Reference,
    string? Notes);

public record CreateCasePaymentRequest(DateTime ReceivedOnUtc, decimal Amount, string? Method, string? Reference, string? Notes);

public record CaseDocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentType DocumentType,
    string? Description,
    bool IsClientVisible,
    DateTime CreatedAtUtc);

public record CaseActivityDto(
    Guid Id,
    DateTime OccurredAtUtc,
    string ActivityType,
    string Summary,
    string? Details,
    string? ActorName);

public record CaseStageDto(Guid Id, CaseStageCode Code, string Name, int SortOrder, bool IsTerminal);
public record CaseStatusDto(Guid Id, CaseStatusCode Code, string Name, bool IsTerminal);
