using System.Text.Json;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class CaseService : ICaseService
{
    private readonly AppDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _user;

    public CaseService(AppDbContext db, IDocumentStorage storage, IAuditService audit, ICurrentUser user)
    {
        _db = db; _storage = storage; _audit = audit; _user = user;
    }

    public async Task<PagedResult<CaseListItemDto>> ListAsync(PageRequest req, CaseListFilter filter, CancellationToken ct = default)
    {
        var q = _db.Cases.AsNoTracking().AsQueryable();

        // Client portal users only see their own client's cases.
        if (_user.IsInRole(Domain.Common.Roles.ClientAdmin) || _user.IsInRole(Domain.Common.Roles.ClientUser))
        {
            if (_user.ClientId is null) return PagedResult<CaseListItemDto>.Empty(req.Page, req.PageSize);
            q = q.Where(c => c.ClientId == _user.ClientId.Value);
        }
        if (filter.ClientId.HasValue) q = q.Where(c => c.ClientId == filter.ClientId.Value);
        if (filter.Stage.HasValue) q = q.Where(c => c.CaseStage.Code == filter.Stage.Value);
        if (filter.Status.HasValue) q = q.Where(c => c.CaseStatus.Code == filter.Status.Value);
        if (filter.AssignedAttorneyId.HasValue) q = q.Where(c => c.AssignedAttorneyId == filter.AssignedAttorneyId.Value);
        if (filter.AssignedParalegalId.HasValue) q = q.Where(c => c.AssignedParalegalId == filter.AssignedParalegalId.Value);
        if (filter.CreatedFrom.HasValue) q = q.Where(c => c.CreatedAtUtc >= filter.CreatedFrom.Value);
        if (filter.CreatedTo.HasValue) q = q.Where(c => c.CreatedAtUtc <= filter.CreatedTo.Value);

        // Tab is a convenience filter on top of explicit stage/status (the explicit ones take precedence).
        switch (filter.Tab)
        {
            case CaseListTab.Active:
                q = q.Where(c => c.CaseStatus.Code != CaseStatusCode.Closed && c.CaseStatus.Code != CaseStatusCode.Cancelled);
                break;
            case CaseListTab.Filed:
                q = q.Where(c => c.FiledOnUtc != null
                                 && c.CaseStatus.Code != CaseStatusCode.Closed
                                 && c.CaseStatus.Code != CaseStatusCode.Cancelled);
                break;
            case CaseListTab.Closed:
                q = q.Where(c => c.CaseStatus.Code == CaseStatusCode.Closed || c.CaseStatus.Code == CaseStatusCode.Cancelled);
                break;
            case CaseListTab.All:
            default:
                break;
        }

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(c =>
                EF.Functions.Like(c.Title, $"%{s}%") ||
                EF.Functions.Like(c.CaseNumber, $"%{s}%") ||
                EF.Functions.Like(c.CourtDocketNumber!, $"%{s}%"));
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

        return new PagedResult<CaseListItemDto> { Items = items, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public async Task<CaseDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Cases.AsNoTracking()
            .Include(x => x.CaseStage)
            .Include(x => x.CaseStatus)
            .Include(x => x.Client)
            .Include(x => x.AssignedAttorney)
            .Include(x => x.AssignedParalegal)
            .Include(x => x.LtCase)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        if ((_user.IsInRole(Domain.Common.Roles.ClientAdmin) || _user.IsInRole(Domain.Common.Roles.ClientUser))
            && _user.ClientId != c.ClientId)
            return null;

        return new CaseDetailDto(
            c.Id, c.CaseNumber, c.Title, c.CaseType, c.ClientId, c.Client.Name,
            c.CaseStageId, c.CaseStage.Name, c.CaseStage.Code,
            c.CaseStatusId, c.CaseStatus.Name, c.CaseStatus.Code,
            c.AssignedAttorneyId,
            c.AssignedAttorney != null ? c.AssignedAttorney.FirstName + " " + c.AssignedAttorney.LastName : null,
            c.AssignedParalegalId,
            c.AssignedParalegal != null ? c.AssignedParalegal.FirstName + " " + c.AssignedParalegal.LastName : null,
            c.PmsLeaseId, c.PmsTenantId, c.PmsPropertyId, c.PmsUnitId,
            c.PmsSnapshotJson, c.PmsSnapshotTakenAtUtc,
            c.AmountInControversy, c.FiledOnUtc, c.CourtDateUtc, c.CourtDocketNumber, c.CourtVenue,
            c.Outcome, c.Description, c.CreatedAtUtc,
            c.LtCase is null ? null : new LtCaseDto(
                c.LtCase.Id, c.LtCase.PremisesAddressLine1, c.LtCase.PremisesCity, c.LtCase.PremisesCounty,
                c.LtCase.PremisesState, c.LtCase.PremisesPostalCode,
                c.LtCase.LandlordName, c.LtCase.RentDue, c.LtCase.LateFees, c.LtCase.OtherCharges, c.LtCase.TotalDue,
                c.LtCase.RentDueAsOf, c.LtCase.IsRegisteredMultipleDwelling, c.LtCase.RegistrationNumber, c.LtCase.AttorneyReviewed));
    }

    public async Task<Result<CaseDetailDto>> CreateAsync(CreateCaseRequest req, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(x => x.Id == req.ClientId, ct);
        if (client is null) return Result<CaseDetailDto>.Failure("Client not found");

        var intakeStage = await _db.CaseStages.FirstAsync(x => x.Code == CaseStageCode.Intake, ct);
        var openStatus = await _db.CaseStatuses.FirstAsync(x => x.Code == CaseStatusCode.Open, ct);

        var seq = await _db.Cases.IgnoreQueryFilters().CountAsync(c => c.LawFirmId == _user.LawFirmId, ct) + 1;
        var caseNumber = $"LT-{DateTime.UtcNow:yyyyMM}-{seq:D5}";

        var c = new Case
        {
            CaseNumber = caseNumber,
            Title = req.Title,
            CaseType = req.CaseType,
            ClientId = req.ClientId,
            CaseStageId = intakeStage.Id,
            CaseStatusId = openStatus.Id,
            AssignedAttorneyId = req.AssignedAttorneyId,
            AssignedParalegalId = req.AssignedParalegalId,
            PmsLeaseId = req.PmsLeaseId,
            PmsTenantId = req.PmsTenantId,
            AmountInControversy = req.AmountInControversy,
            Description = req.Description
        };

        // If created from PMS, hydrate property/unit and create LT shell
        if (req.PmsLeaseId.HasValue)
        {
            var lease = await _db.PmsLeases
                .Include(l => l.Tenant)
                .Include(l => l.Unit).ThenInclude(u => u.Property)
                .FirstOrDefaultAsync(l => l.Id == req.PmsLeaseId.Value, ct);
            if (lease is not null)
            {
                c.PmsTenantId = lease.TenantId;
                c.PmsUnitId = lease.UnitId;
                c.PmsPropertyId = lease.Unit.PropertyId;
                c.AmountInControversy ??= lease.CurrentBalance;
            }
        }

        _db.Cases.Add(c);
        // create LT overlay
        c.LtCase = new LtCase { CaseId = c.Id };
        await _db.SaveChangesAsync(ct);

        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = c.Id,
            ActorUserId = _user.UserId,
            ActivityType = "CaseCreated",
            Summary = $"Case {c.CaseNumber} created"
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.CreateCase, nameof(Case), c.Id.ToString(), $"Created case {c.CaseNumber}", new { c.CaseNumber, c.ClientId, c.Title }, ct);

        var dto = await GetAsync(c.Id, ct);
        return Result<CaseDetailDto>.Success(dto!);
    }

    public async Task<Result<CaseDetailDto>> UpdateAsync(Guid id, UpdateCaseRequest req, CancellationToken ct = default)
    {
        var c = await _db.Cases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CaseDetailDto>.Failure("Case not found");

        c.Title = req.Title;
        c.AssignedAttorneyId = req.AssignedAttorneyId;
        c.AssignedParalegalId = req.AssignedParalegalId;
        c.CourtVenue = req.CourtVenue;
        c.CourtDateUtc = req.CourtDateUtc;
        c.CourtDocketNumber = req.CourtDocketNumber;
        c.Outcome = req.Outcome;
        c.Description = req.Description;
        c.AmountInControversy = req.AmountInControversy;
        await _db.SaveChangesAsync(ct);

        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = c.Id, ActorUserId = _user.UserId,
            ActivityType = "CaseUpdated", Summary = $"Case {c.CaseNumber} updated"
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.UpdateCase, nameof(Case), c.Id.ToString(), $"Updated case {c.CaseNumber}", null, ct);
        var dto = (await GetAsync(c.Id, ct))!;
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<Result<CaseDetailDto>> CreateFromPmsAsync(CreateCaseFromPmsRequest req, CancellationToken ct = default)
    {
        var lease = await _db.PmsLeases
            .Include(l => l.Tenant)
            .Include(l => l.Unit).ThenInclude(u => u.Property)
            .Include(l => l.LedgerItems)
            .Include(l => l.Integration).ThenInclude(i => i.Client)
            .FirstOrDefaultAsync(l => l.Id == req.PmsLeaseId, ct);
        if (lease is null) return Result<CaseDetailDto>.Failure("PMS lease not found");

        // Sanity check: the PMS lease must belong to the requested client.
        if (lease.Integration.ClientId != req.ClientId)
            return Result<CaseDetailDto>.Failure("PMS lease does not belong to the selected client.");

        var inferredTitle = string.IsNullOrWhiteSpace(req.Title)
            ? $"{lease.Tenant.FirstName} {lease.Tenant.LastName} — {lease.Unit.Property.Name} #{lease.Unit.UnitNumber} — Non-payment"
            : req.Title!;

        var createReq = new CreateCaseRequest(
            inferredTitle,
            req.CaseType,
            req.ClientId,
            req.AssignedAttorneyId,
            req.AssignedParalegalId,
            req.PmsLeaseId,
            lease.TenantId,
            lease.CurrentBalance,
            req.Description ?? $"Outstanding balance: {lease.CurrentBalance:C}.");

        var created = await CreateAsync(createReq, ct);
        if (!created.IsSuccess || created.Value is null) return created;

        // Always snapshot at intake when created from PMS.
        var snap = await SnapshotPmsAsync(created.Value.Id, ct);
        if (!snap.IsSuccess) return created;

        // Record compliance confirmation in the activity timeline.
        if (req.ComplianceConfirmed)
        {
            _db.CaseActivities.Add(new CaseActivity
            {
                CaseId = created.Value.Id, ActorUserId = _user.UserId,
                ActivityType = "ComplianceConfirmed",
                Summary = "Operator confirmed pre-filing compliance checklist",
            });
            await _db.SaveChangesAsync(ct);
        }

        var dto = (await GetAsync(created.Value.Id, ct))!;
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<Result<CaseDetailDto>> ChangeStageAsync(Guid id, ChangeCaseStageRequest req, CancellationToken ct = default)
    {
        var c = await _db.Cases.Include(x => x.CaseStage).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CaseDetailDto>.Failure("Case not found");
        var stage = await _db.CaseStages.FirstOrDefaultAsync(x => x.Code == req.StageCode, ct);
        if (stage is null) return Result<CaseDetailDto>.Failure("Invalid stage");

        var fromStage = c.CaseStage.Name;
        c.CaseStageId = stage.Id;

        if (req.StageCode == CaseStageCode.Filed && c.FiledOnUtc is null)
            c.FiledOnUtc = DateTime.UtcNow;
        if (req.StageCode == CaseStageCode.Closed)
        {
            var closed = await _db.CaseStatuses.FirstAsync(x => x.Code == CaseStatusCode.Closed, ct);
            c.CaseStatusId = closed.Id;
        }
        await _db.SaveChangesAsync(ct);

        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = c.Id, ActorUserId = _user.UserId,
            ActivityType = "StageChanged",
            Summary = $"Stage changed from {fromStage} to {stage.Name}",
            Details = req.Note
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.ChangeStatus, nameof(Case), c.Id.ToString(),
            $"Stage changed from {fromStage} to {stage.Name}", new { req.StageCode, req.Note }, ct);

        var dto = (await GetAsync(c.Id, ct))!;
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<Result<CaseDetailDto>> ChangeStatusAsync(Guid id, ChangeCaseStatusRequest req, CancellationToken ct = default)
    {
        var c = await _db.Cases.Include(x => x.CaseStatus).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CaseDetailDto>.Failure("Case not found");
        var status = await _db.CaseStatuses.FirstOrDefaultAsync(x => x.Code == req.StatusCode, ct);
        if (status is null) return Result<CaseDetailDto>.Failure("Invalid status");

        var fromStatus = c.CaseStatus.Name;
        c.CaseStatusId = status.Id;
        await _db.SaveChangesAsync(ct);

        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = c.Id, ActorUserId = _user.UserId,
            ActivityType = "StatusChanged",
            Summary = $"Status changed from {fromStatus} to {status.Name}",
            Details = req.Note,
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.ChangeStatus, nameof(Case), c.Id.ToString(),
            $"Status changed from {fromStatus} to {status.Name}", new { req.StatusCode, req.Note }, ct);

        var dto = (await GetAsync(c.Id, ct))!;
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<Result<CaseDetailDto>> AssignAsync(Guid id, AssignCaseRequest req, CancellationToken ct = default)
    {
        var c = await _db.Cases.Include(x => x.AssignedAttorney).Include(x => x.AssignedParalegal).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CaseDetailDto>.Failure("Case not found");

        var changes = new List<string>();
        if (c.AssignedAttorneyId != req.AttorneyId)
        {
            var fromName = c.AssignedAttorney is null ? "Unassigned" : $"{c.AssignedAttorney.FirstName} {c.AssignedAttorney.LastName}";
            string toName = "Unassigned";
            if (req.AttorneyId.HasValue)
            {
                var attorney = await _db.UserProfiles.FirstOrDefaultAsync(u => u.Id == req.AttorneyId.Value, ct);
                if (attorney is null) return Result<CaseDetailDto>.Failure("Attorney not found");
                toName = attorney.FullName;
            }
            c.AssignedAttorneyId = req.AttorneyId;
            changes.Add($"Attorney: {fromName} → {toName}");
        }
        if (c.AssignedParalegalId != req.ParalegalId)
        {
            var fromName = c.AssignedParalegal is null ? "Unassigned" : $"{c.AssignedParalegal.FirstName} {c.AssignedParalegal.LastName}";
            string toName = "Unassigned";
            if (req.ParalegalId.HasValue)
            {
                var paralegal = await _db.UserProfiles.FirstOrDefaultAsync(u => u.Id == req.ParalegalId.Value, ct);
                if (paralegal is null) return Result<CaseDetailDto>.Failure("Paralegal not found");
                toName = paralegal.FullName;
            }
            c.AssignedParalegalId = req.ParalegalId;
            changes.Add($"Paralegal: {fromName} → {toName}");
        }

        if (changes.Count == 0)
        {
            var unchanged = (await GetAsync(c.Id, ct))!;
            return Result<CaseDetailDto>.Success(unchanged);
        }

        await _db.SaveChangesAsync(ct);

        var summary = string.Join(" · ", changes);
        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = c.Id, ActorUserId = _user.UserId,
            ActivityType = "Assigned",
            Summary = summary,
            Details = req.Note,
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.UpdateCase, nameof(Case), c.Id.ToString(),
            $"Assignment updated: {summary}", new { req.AttorneyId, req.ParalegalId, req.Note }, ct);

        var dto = (await GetAsync(c.Id, ct))!;
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<Result<CaseDetailDto>> CloseAsync(Guid id, CloseCaseRequest req, CancellationToken ct = default)
    {
        var c = await _db.Cases.Include(x => x.CaseStage).Include(x => x.CaseStatus).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CaseDetailDto>.Failure("Case not found");

        var closedStage = await _db.CaseStages.FirstAsync(x => x.Code == CaseStageCode.Closed, ct);
        var closedStatus = await _db.CaseStatuses.FirstAsync(x => x.Code == CaseStatusCode.Closed, ct);

        c.CaseStageId = closedStage.Id;
        c.CaseStatusId = closedStatus.Id;
        if (!string.IsNullOrWhiteSpace(req.Outcome))
            c.Outcome = req.Outcome;
        await _db.SaveChangesAsync(ct);

        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = c.Id, ActorUserId = _user.UserId,
            ActivityType = "Closed",
            Summary = string.IsNullOrWhiteSpace(req.Outcome) ? "Case closed" : $"Case closed — {req.Outcome}",
            Details = req.Notes,
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.CloseCase, nameof(Case), c.Id.ToString(),
            $"Closed case {c.CaseNumber}", new { req.Outcome, req.Notes }, ct);

        var dto = (await GetAsync(c.Id, ct))!;
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<CaseSnapshotDto?> GetSnapshotAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Cases.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.PmsSnapshotJson, x.PmsSnapshotTakenAtUtc })
            .FirstOrDefaultAsync(ct);
        if (c is null) return null;
        if (string.IsNullOrWhiteSpace(c.PmsSnapshotJson))
            return new CaseSnapshotDto(c.Id, c.PmsSnapshotTakenAtUtc, null);

        try
        {
            using var doc = JsonDocument.Parse(c.PmsSnapshotJson);
            var root = doc.RootElement;
            var data = new CaseSnapshotData(
                Property: ParseProperty(root),
                Unit: ParseUnit(root),
                Tenant: ParseTenant(root),
                Lease: ParseLease(root),
                Ledger: ParseLedger(root));
            return new CaseSnapshotDto(c.Id, c.PmsSnapshotTakenAtUtc, data);
        }
        catch
        {
            return new CaseSnapshotDto(c.Id, c.PmsSnapshotTakenAtUtc, null);
        }
    }

    public async Task<IReadOnlyList<AssigneeDto>> GetAssigneesAsync(CancellationToken ct = default)
    {
        // Firm staff: anyone whose Identity user is in Lawyer / FirmAdmin / Paralegal role.
        var firmRoles = new[] { Domain.Common.Roles.Lawyer, Domain.Common.Roles.FirmAdmin, Domain.Common.Roles.Paralegal };

        var profiles = await (
            from p in _db.UserProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.IdentityUserId equals u.Id
            join ur in _db.UserRoles.AsNoTracking() on u.Id equals ur.UserId
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where p.IsActive && firmRoles.Contains(r.Name!)
            select new { p.Id, p.FirstName, p.LastName, p.Email, RoleName = r.Name! })
            .ToListAsync(ct);

        return profiles
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .Select(x => new AssigneeDto(x.Id, $"{x.FirstName} {x.LastName}".Trim(), x.Email, x.RoleName))
            .OrderBy(x => x.FullName)
            .ToList();
    }

    // ─── JSON snapshot decoders ─────────────────────────────────────────────
    private static SnapshotProperty? ParseProperty(JsonElement root)
    {
        if (!root.TryGetProperty("property", out var p) || p.ValueKind != JsonValueKind.Object) return null;
        return new SnapshotProperty(
            Get(p, "Name"), Get(p, "AddressLine1"), Get(p, "City"),
            Get(p, "State"), Get(p, "PostalCode"), Get(p, "County"));
    }
    private static SnapshotUnit? ParseUnit(JsonElement root)
    {
        if (!root.TryGetProperty("unit", out var u) || u.ValueKind != JsonValueKind.Object) return null;
        return new SnapshotUnit(
            Get(u, "UnitNumber"), GetInt(u, "Bedrooms"), GetInt(u, "Bathrooms"), GetDecimal(u, "MarketRent"));
    }
    private static SnapshotTenant? ParseTenant(JsonElement root)
    {
        if (!root.TryGetProperty("tenant", out var t) || t.ValueKind != JsonValueKind.Object) return null;
        return new SnapshotTenant(Get(t, "FirstName"), Get(t, "LastName"), Get(t, "Email"), Get(t, "Phone"));
    }
    private static SnapshotLease? ParseLease(JsonElement root)
    {
        if (!root.TryGetProperty("lease", out var l) || l.ValueKind != JsonValueKind.Object) return null;
        return new SnapshotLease(
            GetDate(l, "StartDate"), GetDate(l, "EndDate"),
            GetDecimal(l, "MonthlyRent"), GetDecimal(l, "SecurityDeposit"),
            l.TryGetProperty("IsMonthToMonth", out var mtm) && mtm.ValueKind == JsonValueKind.True,
            GetDecimal(l, "CurrentBalance"));
    }
    private static IReadOnlyList<SnapshotLedger>? ParseLedger(JsonElement root)
    {
        if (!root.TryGetProperty("ledger", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var list = new List<SnapshotLedger>();
        foreach (var li in arr.EnumerateArray())
        {
            list.Add(new SnapshotLedger(
                PostedDate: GetDate(li, "PostedDate") ?? DateTime.MinValue,
                Category: Get(li, "Category") ?? "",
                Description: Get(li, "Description"),
                Amount: GetDecimal(li, "Amount") ?? 0m,
                Balance: GetDecimal(li, "Balance") ?? 0m,
                IsCharge: li.TryGetProperty("IsCharge", out var c) && c.ValueKind == JsonValueKind.True,
                IsPayment: li.TryGetProperty("IsPayment", out var pmt) && pmt.ValueKind == JsonValueKind.True));
        }
        return list;
    }
    private static string? Get(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int? GetInt(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    private static decimal? GetDecimal(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
    private static DateTime? GetDate(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var v) || v.ValueKind != JsonValueKind.String) return null;
        return DateTime.TryParse(v.GetString(), out var dt) ? dt : null;
    }

    public async Task<Result<CaseDetailDto>> SnapshotPmsAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Cases
            .Include(x => x.LtCase)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<CaseDetailDto>.Failure("Case not found");

        var lease = c.PmsLeaseId.HasValue
            ? await _db.PmsLeases
                .Include(l => l.Tenant)
                .Include(l => l.Unit).ThenInclude(u => u.Property)
                .Include(l => l.LedgerItems)
                .FirstOrDefaultAsync(l => l.Id == c.PmsLeaseId.Value, ct)
            : null;

        var snapshot = new
        {
            takenAt = DateTime.UtcNow,
            lease = lease is null ? null : new
            {
                lease.Id, lease.ExternalId, lease.StartDate, lease.EndDate, lease.MonthlyRent,
                lease.SecurityDeposit, lease.IsMonthToMonth, lease.CurrentBalance
            },
            tenant = lease?.Tenant is null ? null : new
            {
                lease.Tenant.Id, lease.Tenant.FirstName, lease.Tenant.LastName, lease.Tenant.Email, lease.Tenant.Phone
            },
            unit = lease?.Unit is null ? null : new
            {
                lease.Unit.Id, lease.Unit.UnitNumber, lease.Unit.Bedrooms, lease.Unit.Bathrooms, lease.Unit.MarketRent
            },
            property = lease?.Unit.Property is null ? null : new
            {
                lease.Unit.Property.Id, lease.Unit.Property.Name, lease.Unit.Property.AddressLine1,
                lease.Unit.Property.City, lease.Unit.Property.State, lease.Unit.Property.PostalCode,
                lease.Unit.Property.County
            },
            ledger = lease?.LedgerItems.OrderByDescending(li => li.PostedDate).Take(50)
                .Select(li => new { li.PostedDate, li.Category, li.Description, li.Amount, li.Balance, li.IsCharge, li.IsPayment })
        };
        c.PmsSnapshotJson = JsonSerializer.Serialize(snapshot);
        c.PmsSnapshotTakenAtUtc = DateTime.UtcNow;

        if (c.LtCase is not null && lease is not null)
        {
            c.LtCase.PremisesAddressLine1 = lease.Unit.Property.AddressLine1;
            c.LtCase.PremisesCity = lease.Unit.Property.City;
            c.LtCase.PremisesCounty = lease.Unit.Property.County;
            c.LtCase.PremisesState = lease.Unit.Property.State ?? "NJ";
            c.LtCase.PremisesPostalCode = lease.Unit.Property.PostalCode;
            c.LtCase.RentDue = lease.CurrentBalance;
            c.LtCase.TotalDue = lease.CurrentBalance;
            c.LtCase.RentDueAsOf = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);

        _db.CaseActivities.Add(new CaseActivity
        {
            CaseId = c.Id, ActorUserId = _user.UserId,
            ActivityType = "PmsSnapshot",
            Summary = "PMS data snapshot captured for case"
        });
        await _db.SaveChangesAsync(ct);

        var dto = (await GetAsync(c.Id, ct))!;
        return Result<CaseDetailDto>.Success(dto);
    }

    public async Task<IReadOnlyList<CaseCommentDto>> GetCommentsAsync(Guid caseId, CancellationToken ct = default)
    {
        var isClientUser = _user.IsInRole(Domain.Common.Roles.ClientUser) || _user.IsInRole(Domain.Common.Roles.ClientAdmin);
        return await _db.CaseComments.AsNoTracking()
            .Where(x => x.CaseId == caseId && (!isClientUser || !x.IsInternal))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CaseCommentDto(x.Id, x.CaseId, x.Author.FirstName + " " + x.Author.LastName, x.Body, x.IsInternal, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<Result<CaseCommentDto>> AddCommentAsync(Guid caseId, CreateCaseCommentRequest req, CancellationToken ct = default)
    {
        var c = await _db.Cases.FindAsync(new object[] { caseId }, ct);
        if (c is null) return Result<CaseCommentDto>.Failure("Case not found");
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(x => x.IdentityUserId == _user.UserId.ToString(), ct);
        if (profile is null) return Result<CaseCommentDto>.Failure("User profile not found");

        var isClientUser = _user.IsInRole(Domain.Common.Roles.ClientUser) || _user.IsInRole(Domain.Common.Roles.ClientAdmin);
        var comment = new CaseComment
        {
            CaseId = caseId,
            AuthorUserId = profile.Id,
            Body = req.Body,
            IsInternal = !isClientUser && req.IsInternal
        };
        _db.CaseComments.Add(comment);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.CommentAdded, nameof(CaseComment), comment.Id.ToString(),
            $"Comment added to case {c.CaseNumber}{(comment.IsInternal ? " (internal)" : "")}",
            payload: new { caseId, isInternal = comment.IsInternal, length = comment.Body.Length },
            ct);
        return Result<CaseCommentDto>.Success(new CaseCommentDto(comment.Id, caseId, profile.FullName, comment.Body, comment.IsInternal, comment.CreatedAtUtc));
    }

    public async Task<IReadOnlyList<CasePaymentDto>> GetPaymentsAsync(Guid caseId, CancellationToken ct = default) =>
        await _db.CasePayments.AsNoTracking().Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.ReceivedOnUtc)
            .Select(x => new CasePaymentDto(x.Id, x.ReceivedOnUtc, x.Amount, x.Method, x.Reference, x.Notes))
            .ToListAsync(ct);

    public async Task<Result<CasePaymentDto>> AddPaymentAsync(Guid caseId, CreateCasePaymentRequest req, CancellationToken ct = default)
    {
        var c = await _db.Cases.FindAsync(new object[] { caseId }, ct);
        if (c is null) return Result<CasePaymentDto>.Failure("Case not found");
        var p = new CasePayment
        {
            CaseId = caseId, ReceivedOnUtc = req.ReceivedOnUtc, Amount = req.Amount,
            Method = req.Method, Reference = req.Reference, Notes = req.Notes
        };
        _db.CasePayments.Add(p);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.PaymentRecorded, nameof(CasePayment), p.Id.ToString(),
            $"Payment of {p.Amount:C} recorded on case {c.CaseNumber}",
            payload: new { caseId, amount = p.Amount, method = p.Method, reference = p.Reference },
            ct);
        return Result<CasePaymentDto>.Success(new CasePaymentDto(p.Id, p.ReceivedOnUtc, p.Amount, p.Method, p.Reference, p.Notes));
    }

    public async Task<IReadOnlyList<CaseDocumentDto>> GetDocumentsAsync(Guid caseId, CancellationToken ct = default)
    {
        var isClientUser = _user.IsInRole(Domain.Common.Roles.ClientUser) || _user.IsInRole(Domain.Common.Roles.ClientAdmin);
        return await _db.CaseDocuments.AsNoTracking()
            .Where(x => x.CaseId == caseId && (!isClientUser || x.IsClientVisible))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CaseDocumentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.DocumentType, x.Description, x.IsClientVisible, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<Result<CaseDocumentDto>> UploadDocumentAsync(Guid caseId, string fileName, string contentType, long sizeBytes, Stream content, DocumentType type, string? description, bool isClientVisible, CancellationToken ct = default)
    {
        var c = await _db.Cases.FindAsync(new object[] { caseId }, ct);
        if (c is null) return Result<CaseDocumentDto>.Failure("Case not found");
        var path = await _storage.SaveAsync($"cases/{caseId:N}", fileName, content, ct);
        var doc = new CaseDocument
        {
            CaseId = caseId, FileName = fileName, ContentType = contentType, SizeBytes = sizeBytes,
            StoragePath = path, DocumentType = type, Description = description, IsClientVisible = isClientVisible
        };
        _db.CaseDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.UploadDocument, nameof(CaseDocument), doc.Id.ToString(), $"Uploaded {fileName} to case {c.CaseNumber}", null, ct);
        return Result<CaseDocumentDto>.Success(new CaseDocumentDto(doc.Id, doc.FileName, doc.ContentType, doc.SizeBytes, doc.DocumentType, doc.Description, doc.IsClientVisible, doc.CreatedAtUtc));
    }

    public async Task<IReadOnlyList<CaseActivityDto>> GetActivityAsync(Guid caseId, CancellationToken ct = default) =>
        await _db.CaseActivities.AsNoTracking().Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new CaseActivityDto(x.Id, x.OccurredAtUtc, x.ActivityType, x.Summary, x.Details,
                x.Actor != null ? x.Actor.FirstName + " " + x.Actor.LastName : null))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CaseStageDto>> GetStagesAsync(CancellationToken ct = default) =>
        await _db.CaseStages.AsNoTracking().OrderBy(x => x.SortOrder)
            .Select(x => new CaseStageDto(x.Id, x.Code, x.Name, x.SortOrder, x.IsTerminal))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CaseStatusDto>> GetStatusesAsync(CancellationToken ct = default) =>
        await _db.CaseStatuses.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new CaseStatusDto(x.Id, x.Code, x.Name, x.IsTerminal))
            .ToListAsync(ct);
}
