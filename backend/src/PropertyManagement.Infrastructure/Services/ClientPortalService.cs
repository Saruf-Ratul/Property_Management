using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

/// <summary>
/// Backs the /api/client-portal/* endpoints. Every read/write is implicitly scoped to the
/// authenticated user's ClientId. The controller already restricts the route to
/// ClientAdmin / ClientUser, but we double-check at the service level too — defense in depth.
/// </summary>
public class ClientPortalService : IClientPortalService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDocumentStorage _storage;
    private readonly IAuditService _audit;

    public ClientPortalService(AppDbContext db, ICurrentUser user, IDocumentStorage storage, IAuditService audit)
    {
        _db = db; _user = user; _storage = storage; _audit = audit;
    }

    private Guid? Cid => _user.ClientId;

    private bool IsClientAdmin => _user.IsInRole(Roles.ClientAdmin);
    private bool IsClientUser  => _user.IsInRole(Roles.ClientUser);
    private bool IsClient      => IsClientAdmin || IsClientUser;

    /// <summary>Returns Failure if the caller is not a portal user, or has no ClientId on their JWT.</summary>
    private Result<T> RequirePortalUser<T>()
    {
        if (!IsClient) return Result<T>.Failure("Client portal access requires ClientAdmin or ClientUser role.");
        if (Cid is null) return Result<T>.Failure("Your account is not linked to a client. Contact your firm.");
        return Result<T>.Success(default!);
    }

    /// <summary>Returns the case if it belongs to the caller's client, else a Failure.</summary>
    private async Task<Result<Case>> AuthorizedCaseAsync(Guid caseId, CancellationToken ct)
    {
        var gate = RequirePortalUser<Case>();
        if (!gate.IsSuccess) return gate;
        var c = await _db.Cases
            .Include(x => x.Client).Include(x => x.CaseStage).Include(x => x.CaseStatus)
            .FirstOrDefaultAsync(x => x.Id == caseId, ct);
        if (c is null || c.ClientId != Cid)
            return Result<Case>.Failure("Case not found or access denied.");
        return Result<Case>.Success(c);
    }

    // ─── Dashboard ──────────────────────────────────────────────────────────
    public async Task<Result<ClientPortalDashboardDto>> GetDashboardAsync(CancellationToken ct = default)
    {
        var gate = RequirePortalUser<ClientPortalDashboardDto>();
        if (!gate.IsSuccess) return gate;

        var clientId = Cid!.Value;
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null) return Result<ClientPortalDashboardDto>.Failure("Client not found.");

        var caseQ = _db.Cases.AsNoTracking().Where(c => c.ClientId == clientId);

        var total = await caseQ.CountAsync(ct);
        var closed = await caseQ.CountAsync(c => c.CaseStatus.Code == CaseStatusCode.Closed
                                              || c.CaseStatus.Code == CaseStatusCode.Cancelled, ct);
        var active = total - closed;

        var inFiling = await caseQ.CountAsync(c =>
            c.CaseStage.Code == CaseStageCode.Intake ||
            c.CaseStage.Code == CaseStageCode.Draft ||
            c.CaseStage.Code == CaseStageCode.FormReview ||
            c.CaseStage.Code == CaseStageCode.ReadyToFile, ct);
        var inTrial = await caseQ.CountAsync(c =>
            c.CaseStage.Code == CaseStageCode.Filed ||
            c.CaseStage.Code == CaseStageCode.CourtDateScheduled ||
            c.CaseStage.Code == CaseStageCode.Judgment ||
            c.CaseStage.Code == CaseStageCode.Settlement, ct);
        var awaitingWarrant = await caseQ.CountAsync(c => c.CaseStage.Code == CaseStageCode.WarrantRequested, ct);

        var amountInControversy = await caseQ
            .Where(c => c.CaseStatus.Code == CaseStatusCode.Open)
            .SumAsync(c => (decimal?)c.AmountInControversy, ct) ?? 0m;

        var docCount = await _db.CaseDocuments.AsNoTracking()
            .Where(d => d.Case.ClientId == clientId && d.IsClientVisible).CountAsync(ct);

        var nowUtc = DateTime.UtcNow;
        var upcoming = await caseQ
            .Where(c => c.CourtDateUtc != null && c.CourtDateUtc >= nowUtc
                     && c.CaseStatus.Code != CaseStatusCode.Closed
                     && c.CaseStatus.Code != CaseStatusCode.Cancelled)
            .OrderBy(c => c.CourtDateUtc)
            .Take(10)
            .Select(c => new UpcomingCourtDateDto(
                c.Id, c.CaseNumber, c.Title, c.CourtDateUtc!.Value,
                c.CourtVenue, c.CourtDocketNumber))
            .ToListAsync(ct);

        var notifications = await GetNotificationsCoreAsync(20, ct);

        return Result<ClientPortalDashboardDto>.Success(new ClientPortalDashboardDto(
            ClientId: client.Id,
            ClientName: client.Name,
            TotalCases: total,
            ActiveCases: active,
            ClosedCases: closed,
            CasesInFiling: inFiling,
            CasesInTrialOrJudgment: inTrial,
            CasesAwaitingWarrant: awaitingWarrant,
            TotalAmountInControversy: amountInControversy,
            DocumentsAvailableCount: docCount,
            UnreadNotificationCount: notifications.Count(n => n.IsHighlighted),
            NextCourtDateUtc: upcoming.FirstOrDefault()?.CourtDateUtc,
            UpcomingCourtDates: upcoming,
            RecentActivity: notifications));
    }

    // ─── Cases list / detail ────────────────────────────────────────────────
    public async Task<Result<PagedResult<CaseListItemDto>>> ListCasesAsync(
        PageRequest req, CaseStageCode? stage, CaseStatusCode? status, CancellationToken ct = default)
    {
        var gate = RequirePortalUser<PagedResult<CaseListItemDto>>();
        if (!gate.IsSuccess) return gate;

        var q = _db.Cases.AsNoTracking().Where(c => c.ClientId == Cid!.Value);
        if (stage.HasValue) q = q.Where(c => c.CaseStage.Code == stage.Value);
        if (status.HasValue) q = q.Where(c => c.CaseStatus.Code == status.Value);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(c => EF.Functions.Like(c.Title, $"%{s}%") || EF.Functions.Like(c.CaseNumber, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAtUtc)
            .Skip(req.Skip).Take(req.Take)
            .Select(c => new CaseListItemDto(
                c.Id, c.CaseNumber, c.Title, c.CaseType,
                c.Client.Name, c.ClientId,
                c.CaseStage.Name, c.CaseStage.Code,
                c.CaseStatus.Name, c.CaseStatus.Code,
                c.AssignedAttorney != null ? c.AssignedAttorney.FirstName + " " + c.AssignedAttorney.LastName : null,
                c.FiledOnUtc, c.CourtDateUtc, c.AmountInControversy, c.CreatedAtUtc))
            .ToListAsync(ct);

        return Result<PagedResult<CaseListItemDto>>.Success(new PagedResult<CaseListItemDto>
        {
            Items = items, Page = req.Page, PageSize = req.Take, TotalCount = total
        });
    }

    public async Task<Result<CaseDetailDto>> GetCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var auth = await AuthorizedCaseAsync(caseId, ct);
        if (!auth.IsSuccess) return Result<CaseDetailDto>.Failure(auth.Error!);

        var c = await _db.Cases.AsNoTracking()
            .Include(x => x.CaseStage)
            .Include(x => x.CaseStatus)
            .Include(x => x.Client)
            .Include(x => x.AssignedAttorney)
            .Include(x => x.AssignedParalegal)
            .Include(x => x.LtCase)
            .FirstAsync(x => x.Id == caseId, ct);

        var dto = new CaseDetailDto(
            c.Id, c.CaseNumber, c.Title, c.CaseType, c.ClientId, c.Client.Name,
            c.CaseStageId, c.CaseStage.Name, c.CaseStage.Code,
            c.CaseStatusId, c.CaseStatus.Name, c.CaseStatus.Code,
            c.AssignedAttorneyId,
            c.AssignedAttorney != null ? c.AssignedAttorney.FirstName + " " + c.AssignedAttorney.LastName : null,
            c.AssignedParalegalId,
            c.AssignedParalegal != null ? c.AssignedParalegal.FirstName + " " + c.AssignedParalegal.LastName : null,
            // PMS snapshot ids and json are firm-internal — null them out for the client.
            null, null, null, null, null, null,
            c.AmountInControversy, c.FiledOnUtc, c.CourtDateUtc, c.CourtDocketNumber, c.CourtVenue,
            c.Outcome, c.Description, c.CreatedAtUtc,
            c.LtCase is null ? null : new LtCaseDto(
                c.LtCase.Id, c.LtCase.PremisesAddressLine1, c.LtCase.PremisesCity, c.LtCase.PremisesCounty,
                c.LtCase.PremisesState, c.LtCase.PremisesPostalCode,
                c.LtCase.LandlordName, c.LtCase.RentDue, c.LtCase.LateFees, c.LtCase.OtherCharges, c.LtCase.TotalDue,
                c.LtCase.RentDueAsOf, c.LtCase.IsRegisteredMultipleDwelling, c.LtCase.RegistrationNumber, c.LtCase.AttorneyReviewed));
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<CaseActivityDto>>> GetCaseActivityAsync(Guid caseId, CancellationToken ct = default)
    {
        var auth = await AuthorizedCaseAsync(caseId, ct);
        if (!auth.IsSuccess) return Result<IReadOnlyList<CaseActivityDto>>.Failure(auth.Error!);

        var items = await _db.CaseActivities.AsNoTracking()
            .Where(a => a.CaseId == caseId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Select(a => new CaseActivityDto(a.Id, a.OccurredAtUtc, a.ActivityType, a.Summary, a.Details,
                a.Actor != null ? a.Actor.FirstName + " " + a.Actor.LastName : null))
            .ToListAsync(ct);
        return Result<IReadOnlyList<CaseActivityDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<CaseCommentDto>>> GetCaseCommentsAsync(Guid caseId, CancellationToken ct = default)
    {
        var auth = await AuthorizedCaseAsync(caseId, ct);
        if (!auth.IsSuccess) return Result<IReadOnlyList<CaseCommentDto>>.Failure(auth.Error!);

        var items = await _db.CaseComments.AsNoTracking()
            .Where(x => x.CaseId == caseId && !x.IsInternal)        // hide internal comments from clients
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CaseCommentDto(x.Id, x.CaseId, x.Author.FirstName + " " + x.Author.LastName,
                x.Body, x.IsInternal, x.CreatedAtUtc))
            .ToListAsync(ct);
        return Result<IReadOnlyList<CaseCommentDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<CaseDocumentDto>>> GetCaseDocumentsAsync(Guid caseId, CancellationToken ct = default)
    {
        var auth = await AuthorizedCaseAsync(caseId, ct);
        if (!auth.IsSuccess) return Result<IReadOnlyList<CaseDocumentDto>>.Failure(auth.Error!);

        var items = await _db.CaseDocuments.AsNoTracking()
            .Where(d => d.CaseId == caseId && d.IsClientVisible)    // only client-visible docs
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new CaseDocumentDto(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.DocumentType,
                d.Description, d.IsClientVisible, d.CreatedAtUtc))
            .ToListAsync(ct);
        return Result<IReadOnlyList<CaseDocumentDto>>.Success(items);
    }

    // ─── Writes (ClientAdmin only) ──────────────────────────────────────────
    public async Task<Result<CaseCommentDto>> AddCommentAsync(Guid caseId, ClientPortalCommentRequest req, CancellationToken ct = default)
    {
        if (!IsClientAdmin)
            return Result<CaseCommentDto>.Failure("Only ClientAdmin can post comments through the portal.");
        if (string.IsNullOrWhiteSpace(req.Body))
            return Result<CaseCommentDto>.Failure("Comment body is required.");

        var auth = await AuthorizedCaseAsync(caseId, ct);
        if (!auth.IsSuccess) return Result<CaseCommentDto>.Failure(auth.Error!);

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(x => x.IdentityUserId == _user.UserId.ToString(), ct);
        if (profile is null) return Result<CaseCommentDto>.Failure("User profile not found.");

        var comment = new CaseComment
        {
            CaseId = caseId,
            AuthorUserId = profile.Id,
            Body = req.Body.Trim(),
            IsInternal = false,    // portal comments are always client-visible
        };
        _db.CaseComments.Add(comment);
        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = caseId, ActorUserId = profile.Id,
            ActivityType = "ClientComment",
            Summary = $"{profile.FullName} posted a comment from the client portal",
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.UpdateCase, nameof(CaseComment), comment.Id.ToString(),
            $"Portal comment on case {auth.Value!.CaseNumber}", null, ct);

        return Result<CaseCommentDto>.Success(new CaseCommentDto(
            comment.Id, caseId, profile.FullName, comment.Body, false, comment.CreatedAtUtc));
    }

    public async Task<Result<CaseDocumentDto>> UploadDocumentAsync(Guid caseId, string fileName, string contentType,
        long sizeBytes, Stream content, string? description, CancellationToken ct = default)
    {
        if (!IsClientAdmin)
            return Result<CaseDocumentDto>.Failure("Only ClientAdmin can upload documents through the portal.");

        var auth = await AuthorizedCaseAsync(caseId, ct);
        if (!auth.IsSuccess) return Result<CaseDocumentDto>.Failure(auth.Error!);

        var path = await _storage.SaveAsync($"cases/{caseId:N}/portal", fileName, content, ct);
        var doc = new CaseDocument
        {
            CaseId = caseId, FileName = fileName, ContentType = contentType, SizeBytes = sizeBytes,
            StoragePath = path,
            DocumentType = DocumentType.Other,
            Description = string.IsNullOrWhiteSpace(description)
                ? "Uploaded by client via portal"
                : $"[Client] {description}",
            IsClientVisible = true,
        };
        _db.CaseDocuments.Add(doc);
        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = caseId, ActorUserId = _user.UserId,
            ActivityType = "ClientDocumentUploaded",
            Summary = $"Client uploaded {fileName}",
            Details = description,
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.UploadDocument, nameof(CaseDocument), doc.Id.ToString(),
            $"Portal upload {fileName} to case {auth.Value!.CaseNumber}", null, ct);

        return Result<CaseDocumentDto>.Success(new CaseDocumentDto(
            doc.Id, doc.FileName, doc.ContentType, doc.SizeBytes, doc.DocumentType,
            doc.Description, doc.IsClientVisible, doc.CreatedAtUtc));
    }

    // ─── Notifications ──────────────────────────────────────────────────────
    public async Task<Result<IReadOnlyList<ClientPortalNotificationDto>>> GetNotificationsAsync(int take, CancellationToken ct = default)
    {
        var gate = RequirePortalUser<IReadOnlyList<ClientPortalNotificationDto>>();
        if (!gate.IsSuccess) return gate;

        var items = await GetNotificationsCoreAsync(Math.Clamp(take, 1, 200), ct);
        return Result<IReadOnlyList<ClientPortalNotificationDto>>.Success(items);
    }

    private async Task<List<ClientPortalNotificationDto>> GetNotificationsCoreAsync(int take, CancellationToken ct)
    {
        // Activity types that are *meaningful to clients*. Internal ones (e.g. "PmsSnapshot") are excluded.
        var clientFacingTypes = new[]
        {
            "CaseCreated", "StageChanged", "StatusChanged", "Assigned", "Closed",
            "ClientComment", "ClientDocumentUploaded",
            "Payment",
        };

        // Activity types we want to highlight (badge "new" / "important")
        var highlightedTypes = new[] { "StageChanged", "StatusChanged", "Closed" };

        var clientId = Cid!.Value;
        var rows = await _db.CaseActivities.AsNoTracking()
            .Where(a => a.Case.ClientId == clientId && clientFacingTypes.Contains(a.ActivityType))
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .Select(a => new
            {
                a.Id, a.CaseId,
                a.Case.CaseNumber, a.Case.Title,
                a.OccurredAtUtc, a.ActivityType, a.Summary
            })
            .ToListAsync(ct);

        return rows.Select(a => new ClientPortalNotificationDto(
            a.Id, a.CaseId, a.CaseNumber, a.Title,
            a.OccurredAtUtc, a.ActivityType, a.Summary,
            Severity: a.ActivityType is "Closed" or "StageChanged" or "StatusChanged" ? "info" : "default",
            IsHighlighted: highlightedTypes.Contains(a.ActivityType))).ToList();
    }
}
