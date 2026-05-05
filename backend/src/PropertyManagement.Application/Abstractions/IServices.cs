using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions;

public interface IAuthService
{
    Task<Result<AuthResponse>> LoginAsync(LoginRequest req, string? ip, string? ua, CancellationToken ct = default);
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, Guid lawFirmId, CancellationToken ct = default);
    Task<UserDto?> GetCurrentAsync(CancellationToken ct = default);
}

public interface IClientService
{
    Task<PagedResult<ClientDto>> ListAsync(PageRequest req, CancellationToken ct = default);
    Task<ClientDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<ClientDto>> CreateAsync(CreateClientRequest req, CancellationToken ct = default);
    Task<Result<ClientDto>> UpdateAsync(Guid id, UpdateClientRequest req, CancellationToken ct = default);
    /// <summary>
    /// Soft-deletes a client. Refuses if any cases or PMS integrations are still attached;
    /// the operator must remove or reassign those first to avoid orphaning history. The
    /// client row is logically deleted (IsDeleted=true) and stops appearing in list/detail
    /// queries due to the global soft-delete filter.
    /// </summary>
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IPropertyService
{
    Task<PagedResult<PropertyDto>> ListAsync(PageRequest req, PropertyFilter filter, CancellationToken ct = default);
    Task<PropertyDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PropertyDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UnitDto>> GetUnitsAsync(Guid propertyId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantDto>> GetTenantsAsync(Guid propertyId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaseDto>> GetLeasesAsync(Guid propertyId, CancellationToken ct = default);
    Task<PropertyLedgerSummaryDto> GetLedgerSummaryAsync(Guid propertyId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCountiesAsync(CancellationToken ct = default);
}

public interface ITenantService
{
    Task<PagedResult<TenantDto>> ListAsync(PageRequest req, TenantFilter filter, CancellationToken ct = default);
    Task<TenantDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LeaseDto>> GetLeasesAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerItemDto>> GetLedgerAsync(Guid leaseId, CancellationToken ct = default);
    Task<PagedResult<DelinquentTenantDto>> GetDelinquentAsync(PageRequest req, Guid? clientId, decimal minBalance, CancellationToken ct = default);
    Task<DelinquencyStatsDto> GetDelinquencyStatsAsync(Guid? clientId, CancellationToken ct = default);
}

public interface IPmsIntegrationService
{
    Task<PagedResult<PmsIntegrationDto>> ListAsync(PageRequest req, Guid? clientId, CancellationToken ct = default);
    Task<PmsIntegrationDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<PmsIntegrationDto>> CreateAsync(CreatePmsIntegrationRequest req, CancellationToken ct = default);
    Task<Result<PmsIntegrationDto>> UpdateAsync(Guid id, UpdatePmsIntegrationRequest req, CancellationToken ct = default);

    /// <summary>Test using stored credentials.</summary>
    Task<Result<PmsConnectionTestResult>> TestAsync(Guid id, CancellationToken ct = default);
    /// <summary>Test using ad-hoc credentials supplied in the request, without persisting anything.</summary>
    Task<Result<PmsConnectionTestResult>> TestAdHocAsync(PmsConnectionTestRequest req, CancellationToken ct = default);

    /// <summary>Run a sync. May enqueue a Hangfire job (RunInBackground=true) or run synchronously.</summary>
    Task<Result<PmsSyncResult>> TriggerSyncAsync(Guid id, PmsSyncRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<SyncLogDto>> GetSyncLogsAsync(Guid integrationId, int take, CancellationToken ct = default);
}

public class CaseListFilter
{
    public Guid? ClientId { get; set; }
    public CaseStageCode? Stage { get; set; }
    public CaseStatusCode? Status { get; set; }
    public Guid? AssignedAttorneyId { get; set; }
    public Guid? AssignedParalegalId { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    /// <summary>Convenience tab — Active / Filed / Closed / All. Maps to a stage/status combo.</summary>
    public CaseListTab Tab { get; set; } = CaseListTab.All;
}

public enum CaseListTab { All = 0, Active = 1, Filed = 2, Closed = 3 }

public interface ICaseService
{
    Task<PagedResult<CaseListItemDto>> ListAsync(PageRequest req, CaseListFilter filter, CancellationToken ct = default);
    Task<CaseDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<CaseSnapshotDto?> GetSnapshotAsync(Guid id, CancellationToken ct = default);

    Task<Result<CaseDetailDto>> CreateAsync(CreateCaseRequest req, CancellationToken ct = default);
    Task<Result<CaseDetailDto>> CreateFromPmsAsync(CreateCaseFromPmsRequest req, CancellationToken ct = default);
    Task<Result<CaseDetailDto>> UpdateAsync(Guid id, UpdateCaseRequest req, CancellationToken ct = default);

    Task<Result<CaseDetailDto>> ChangeStageAsync(Guid id, ChangeCaseStageRequest req, CancellationToken ct = default);
    Task<Result<CaseDetailDto>> ChangeStatusAsync(Guid id, ChangeCaseStatusRequest req, CancellationToken ct = default);
    Task<Result<CaseDetailDto>> AssignAsync(Guid id, AssignCaseRequest req, CancellationToken ct = default);
    Task<Result<CaseDetailDto>> CloseAsync(Guid id, CloseCaseRequest req, CancellationToken ct = default);

    Task<Result<CaseDetailDto>> SnapshotPmsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CaseCommentDto>> GetCommentsAsync(Guid caseId, CancellationToken ct = default);
    Task<Result<CaseCommentDto>> AddCommentAsync(Guid caseId, CreateCaseCommentRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<CasePaymentDto>> GetPaymentsAsync(Guid caseId, CancellationToken ct = default);
    Task<Result<CasePaymentDto>> AddPaymentAsync(Guid caseId, CreateCasePaymentRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<CaseDocumentDto>> GetDocumentsAsync(Guid caseId, CancellationToken ct = default);
    Task<Result<CaseDocumentDto>> UploadDocumentAsync(Guid caseId, string fileName, string contentType, long sizeBytes, Stream content, DocumentType type, string? description, bool isClientVisible, CancellationToken ct = default);

    Task<IReadOnlyList<CaseActivityDto>> GetActivityAsync(Guid caseId, CancellationToken ct = default);

    Task<IReadOnlyList<CaseStageDto>> GetStagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CaseStatusDto>> GetStatusesAsync(CancellationToken ct = default);

    /// <summary>List of firm-staff users that can be assigned (Lawyer/FirmAdmin/Paralegal).</summary>
    Task<IReadOnlyList<AssigneeDto>> GetAssigneesAsync(CancellationToken ct = default);
}

public record AssigneeDto(Guid Id, string FullName, string Email, string Role);

public interface ILtFormService
{
    Task<LtFormAutofillResponse> AutofillAsync(Guid caseId, LtFormType formType, CancellationToken ct = default);
    Task<IReadOnlyList<LtFormDataDto>> ListFormDataAsync(Guid caseId, CancellationToken ct = default);
    Task<Result<LtFormDataDto>> SaveFormDataAsync(Guid caseId, SaveLtFormDataRequest req, CancellationToken ct = default);
    Task<Result<LtFormDataDto>> ApproveAsync(Guid caseId, ApproveLtFormRequest req, CancellationToken ct = default);
    Task<Result<GeneratedDocumentDto>> GenerateAsync(Guid caseId, GenerateLtPdfRequest req, CancellationToken ct = default);
    Task<Result<GeneratedDocumentDto>> GeneratePacketAsync(Guid caseId, GenerateLtPacketRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<GeneratedDocumentDto>> GetGeneratedAsync(Guid caseId, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(Guid generatedDocumentId, CancellationToken ct = default);
}

/// <summary>
/// New LT-case-centric service backing the /api/lt-cases/* endpoints. Operates on LtCase ids
/// directly (vs the older route which keyed off Case ids and looked up the LtCase overlay).
/// </summary>
public interface ILtCaseService
{
    Task<PagedResult<LtCaseSummaryDto>> ListAsync(PageRequest req, LtFormPhase? phase, Guid? clientId, CancellationToken ct = default);
    Task<LtCaseDetailDto?> GetAsync(Guid ltCaseId, CancellationToken ct = default);
    Task<Result<LtCaseDetailDto>> CreateFromCaseAsync(Guid caseId, CancellationToken ct = default);

    Task<LtFormBundleDto?> GetFormBundleAsync(Guid ltCaseId, CancellationToken ct = default);
    Task<Result<LtFormBundleDto>> SaveFormBundleAsync(Guid ltCaseId, SaveLtFormBundleRequest req, CancellationToken ct = default);

    Task<LtValidationSummary> ValidateAsync(Guid ltCaseId, LtFormType formType, CancellationToken ct = default);

    Task<Result<GeneratedDocumentDto>> GenerateFormAsync(Guid ltCaseId, LtFormType formType, GenerateFormRequest req, CancellationToken ct = default);
    Task<Result<(byte[] Bytes, string FileName)>> PreviewFormAsync(Guid ltCaseId, LtFormType formType, GenerateFormRequest req, CancellationToken ct = default);
    Task<Result<GeneratedDocumentDto>> GeneratePacketAsync(Guid ltCaseId, GeneratePacketRequestNew req, CancellationToken ct = default);

    /// <summary>Approve / unapprove a single form. Only Lawyer/FirmAdmin should call this.</summary>
    Task<Result<LtFormApprovalDto>> SetFormApprovalAsync(Guid ltCaseId, LtFormType formType, bool isApproved, CancellationToken ct = default);

    /// <summary>Mark the LT case as reviewed by an attorney (precondition for final packet generation).</summary>
    Task<Result<LtCaseDetailDto>> MarkAttorneyReviewedAsync(Guid ltCaseId, bool reviewed, CancellationToken ct = default);

    Task<IReadOnlyList<GeneratedDocumentDto>> GetGeneratedAsync(Guid ltCaseId, CancellationToken ct = default);

    Task<IReadOnlyList<LtFormSchemaDto>> GetSchemasAsync(CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// Service backing the /api/client-portal/* endpoints. Every method is implicitly scoped to the
/// authenticated user's <see cref="ICurrentUser.ClientId"/> — implementations must reject
/// any attempt to read a record that doesn't belong to that client.
/// </summary>
public interface IClientPortalService
{
    Task<Result<ClientPortalDashboardDto>> GetDashboardAsync(CancellationToken ct = default);
    Task<Result<PagedResult<CaseListItemDto>>> ListCasesAsync(PageRequest req, CaseStageCode? stage, CaseStatusCode? status, CancellationToken ct = default);
    Task<Result<CaseDetailDto>> GetCaseAsync(Guid caseId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CaseActivityDto>>> GetCaseActivityAsync(Guid caseId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CaseCommentDto>>> GetCaseCommentsAsync(Guid caseId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CaseDocumentDto>>> GetCaseDocumentsAsync(Guid caseId, CancellationToken ct = default);

    Task<Result<CaseCommentDto>> AddCommentAsync(Guid caseId, ClientPortalCommentRequest req, CancellationToken ct = default);
    Task<Result<CaseDocumentDto>> UploadDocumentAsync(Guid caseId, string fileName, string contentType, long sizeBytes, Stream content, string? description, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ClientPortalNotificationDto>>> GetNotificationsAsync(int take, CancellationToken ct = default);
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> ListAsync(PageRequest req, AuditAction? action, DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>Single-record detail with full payload + old/new values.</summary>
    Task<AuditLogDetailDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Export a CSV stream of audit logs that match the filter (no paging — caps at 50k rows).</summary>
    Task<(byte[] Bytes, string FileName)> ExportCsvAsync(string? search, AuditAction? action, DateTime? from, DateTime? to, CancellationToken ct = default);
}
