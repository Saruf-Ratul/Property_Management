using System.Security.Cryptography;
using System.Text.Json;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PropertyManagement.Infrastructure.Services;

/// <summary>
/// Implements /api/lt-cases/* — operates on LT-case ids directly.
/// Stores the structured form bundle as a JSON blob on the synthetic
/// "Master" form-data row (FormType=VerifiedComplaint, since that's the most comprehensive form).
/// </summary>
public class LtCaseService : ILtCaseService
{
    /// <summary>
    /// We piggyback the structured bundle onto an existing FormType slot (VerifiedComplaint) so we
    /// don't need a schema migration. The bundle itself is the source of truth; per-form approval
    /// is tracked by the LtCase and per-form generation versions live in GeneratedDocument.
    /// </summary>
    private const LtFormType BundleSlot = LtFormType.VerifiedComplaint;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;
    private readonly IPdfFormFillerService _filler;
    private readonly IPdfFieldMappingService _mapper;
    private readonly IRedactionValidator _redaction;
    private readonly IDocumentStorage _storage;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _user;
    private readonly ILogger<LtCaseService> _log;

    public LtCaseService(AppDbContext db, IPdfFormFillerService filler, IPdfFieldMappingService mapper,
        IRedactionValidator redaction, IDocumentStorage storage, IAuditService audit,
        ICurrentUser user, ILogger<LtCaseService> log)
    {
        _db = db; _filler = filler; _mapper = mapper; _redaction = redaction;
        _storage = storage; _audit = audit; _user = user; _log = log;
    }

    // ─── Schema ─────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<LtFormSchemaDto>> GetSchemasAsync(CancellationToken ct = default)
        => Task.FromResult(_mapper.AllSchemas);

    // ─── List / detail ──────────────────────────────────────────────────────
    public async Task<PagedResult<LtCaseSummaryDto>> ListAsync(PageRequest req, LtFormPhase? phase, Guid? clientId, CancellationToken ct = default)
    {
        var q = _db.LtCases.AsNoTracking()
            .Include(x => x.Case).ThenInclude(c => c.CaseStage)
            .Include(x => x.Case).ThenInclude(c => c.CaseStatus)
            .Include(x => x.Case).ThenInclude(c => c.Client)
            .AsQueryable();

        if (clientId.HasValue) q = q.Where(x => x.Case.ClientId == clientId.Value);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(x =>
                EF.Functions.Like(x.Case.Title, $"%{s}%") ||
                EF.Functions.Like(x.Case.CaseNumber, $"%{s}%"));
        }

        // Pull a small slice and aggregate the form/doc stats client-side so the InMemory provider is happy.
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

        var caseIds = rows.Select(x => x.CaseId).ToList();
        var ltIds = rows.Select(x => x.Id).ToList();

        var formApprovals = await _db.LtCaseFormData.AsNoTracking()
            .Where(f => ltIds.Contains(f.LtCaseId))
            .Select(f => new { f.LtCaseId, f.FormType, f.IsApproved })
            .ToListAsync(ct);

        var generated = await _db.GeneratedDocuments.AsNoTracking()
            .Where(g => caseIds.Contains(g.CaseId))
            .Select(g => new { g.CaseId, g.IsMergedPacket, g.GeneratedAtUtc })
            .ToListAsync(ct);

        var summaries = rows.Select(x =>
        {
            var stage = x.Case.CaseStage.Code;
            var resolvedPhase = ResolvePhase(stage);
            var approvals = formApprovals.Where(f => f.LtCaseId == x.Id).ToList();
            var thisGen = generated.Where(g => g.CaseId == x.CaseId).ToList();
            return new LtCaseSummaryDto(
                x.Id, x.CaseId, x.Case.CaseNumber, x.Case.Title,
                x.Case.Client.Name, x.Case.ClientId,
                stage, x.Case.CaseStage.Name,
                x.Case.CaseStatus.Code, x.Case.CaseStatus.Name,
                resolvedPhase, PhaseName(resolvedPhase),
                x.AttorneyReviewed,
                approvals.Count(a => a.IsApproved),
                7, // 7 NJ forms total
                thisGen.Count(g => !g.IsMergedPacket),
                thisGen.Count(g => g.IsMergedPacket),
                thisGen.Count > 0 ? thisGen.Max(g => g.GeneratedAtUtc) : (DateTime?)null,
                x.TotalDue,
                x.CreatedAtUtc);
        });

        if (phase.HasValue) summaries = summaries.Where(s => s.Phase == phase.Value);

        var list = summaries.ToList();
        var total = list.Count;
        var paged = list.Skip(req.Skip).Take(req.Take).ToList();
        return new PagedResult<LtCaseSummaryDto> { Items = paged, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public async Task<LtCaseDetailDto?> GetAsync(Guid ltCaseId, CancellationToken ct = default)
    {
        var x = await _db.LtCases.AsNoTracking()
            .Include(l => l.Case).ThenInclude(c => c.CaseStage)
            .Include(l => l.Case).ThenInclude(c => c.Client)
            .Include(l => l.AttorneyReviewedBy)
            .FirstOrDefaultAsync(l => l.Id == ltCaseId, ct);
        if (x is null) return null;

        return new LtCaseDetailDto(
            x.Id, x.CaseId, x.Case.CaseNumber, x.Case.Title,
            x.Case.Client.Name, x.Case.ClientId,
            ResolvePhase(x.Case.CaseStage.Code),
            x.AttorneyReviewed, x.AttorneyReviewedAtUtc,
            x.AttorneyReviewedBy is null ? null : $"{x.AttorneyReviewedBy.FirstName} {x.AttorneyReviewedBy.LastName}",
            x.PremisesAddressLine1, x.PremisesCity, x.PremisesCounty,
            x.PremisesState, x.PremisesPostalCode,
            x.LandlordName, x.RentDue, x.LateFees, x.OtherCharges, x.TotalDue,
            x.RentDueAsOf, x.IsRegisteredMultipleDwelling, x.RegistrationNumber);
    }

    public async Task<Result<LtCaseDetailDto>> CreateFromCaseAsync(Guid caseId, CancellationToken ct = default)
    {
        var c = await _db.Cases.Include(x => x.LtCase).FirstOrDefaultAsync(x => x.Id == caseId, ct);
        if (c is null) return Result<LtCaseDetailDto>.Failure("Case not found");

        if (c.LtCase is null)
        {
            c.LtCase = new LtCase { CaseId = c.Id };
            await _db.SaveChangesAsync(ct);
        }

        var dto = await GetAsync(c.LtCase.Id, ct);
        return dto is null
            ? Result<LtCaseDetailDto>.Failure("LtCase not found after creation.")
            : Result<LtCaseDetailDto>.Success(dto);
    }

    // ─── Form bundle (structured master data) ───────────────────────────────
    public async Task<LtFormBundleDto?> GetFormBundleAsync(Guid ltCaseId, CancellationToken ct = default)
    {
        var lt = await _db.LtCases.AsNoTracking()
            .Include(x => x.Case)
            .FirstOrDefaultAsync(x => x.Id == ltCaseId, ct);
        if (lt is null) return null;

        // Read the master row.
        var master = await _db.LtCaseFormData.AsNoTracking()
            .FirstOrDefaultAsync(f => f.LtCaseId == ltCaseId && f.FormType == BundleSlot, ct);

        var sections = master is null || string.IsNullOrWhiteSpace(master.DataJson)
            ? await BuildAutoFilledSectionsAsync(lt, ct)
            : (DeserializeSections(master.DataJson) ?? await BuildAutoFilledSectionsAsync(lt, ct));

        // Per-form approvals.
        var perForm = await _db.LtCaseFormData.AsNoTracking()
            .Where(f => f.LtCaseId == ltCaseId)
            .Include(f => f.ApprovedBy)
            .ToListAsync(ct);

        var approvals = Enum.GetValues<LtFormType>()
            .ToDictionary(
                t => t,
                t =>
                {
                    var row = perForm.FirstOrDefault(p => p.FormType == t);
                    return new LtFormApprovalDto(
                        row?.IsApproved ?? false,
                        row?.ApprovedAtUtc,
                        row?.ApprovedBy is null ? null : $"{row.ApprovedBy.FirstName} {row.ApprovedBy.LastName}");
                });

        return new LtFormBundleDto(
            ltCaseId, sections, approvals,
            master?.UpdatedAtUtc ?? master?.CreatedAtUtc);
    }

    public async Task<Result<LtFormBundleDto>> SaveFormBundleAsync(Guid ltCaseId, SaveLtFormBundleRequest req, CancellationToken ct = default)
    {
        var lt = await _db.LtCases.Include(x => x.Case).FirstOrDefaultAsync(x => x.Id == ltCaseId, ct);
        if (lt is null) return Result<LtFormBundleDto>.Failure("LT case not found");

        // Mirror selected fields to LtCase columns so list/detail queries stay cheap.
        lt.PremisesAddressLine1   = req.Sections.Premises.AddressLine1;
        lt.PremisesAddressLine2   = req.Sections.Premises.AddressLine2;
        lt.PremisesCity           = req.Sections.Premises.City;
        lt.PremisesCounty         = req.Sections.Premises.County;
        lt.PremisesState          = req.Sections.Premises.State;
        lt.PremisesPostalCode     = req.Sections.Premises.PostalCode;
        lt.LandlordName           = req.Sections.Plaintiff.Name;
        lt.RentDue                = req.Sections.RentOwed.Total;
        lt.LateFees               = req.Sections.AdditionalRent.LateFees;
        lt.OtherCharges           = req.Sections.AdditionalRent.OtherCharges;
        lt.TotalDue               = req.Sections.RentOwed.Total
                                     + (req.Sections.AdditionalRent.LateFees ?? 0)
                                     + (req.Sections.AdditionalRent.AttorneyFees ?? 0)
                                     + (req.Sections.AdditionalRent.OtherCharges ?? 0);
        lt.RentDueAsOf            = req.Sections.RentOwed.AsOfDate;
        lt.IsRegisteredMultipleDwelling = req.Sections.Registration.IsRegisteredMultipleDwelling;
        lt.RegistrationNumber     = req.Sections.Registration.RegistrationNumber;
        lt.RegistrationDate       = req.Sections.Registration.RegistrationDate;

        var json = JsonSerializer.Serialize(req.Sections, Json);

        var master = await _db.LtCaseFormData.FirstOrDefaultAsync(
            f => f.LtCaseId == ltCaseId && f.FormType == BundleSlot, ct);
        if (master is null)
        {
            master = new LtCaseFormData { LtCaseId = ltCaseId, FormType = BundleSlot, DataJson = json };
            _db.LtCaseFormData.Add(master);
        }
        else
        {
            master.DataJson = json;
            // Saving the bundle invalidates per-form approvals.
            master.IsApproved = false;
            master.ApprovedAtUtc = null;
            master.ApprovedById = null;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.UpdateCase, nameof(LtCase), ltCaseId.ToString(),
            "Saved LT form bundle", null, ct);

        var dto = await GetFormBundleAsync(ltCaseId, ct);
        return Result<LtFormBundleDto>.Success(dto!);
    }

    // ─── Validation summary ─────────────────────────────────────────────────
    public async Task<LtValidationSummary> ValidateAsync(Guid ltCaseId, LtFormType formType, CancellationToken ct = default)
    {
        var bundle = await GetFormBundleAsync(ltCaseId, ct)
                     ?? throw new KeyNotFoundException("LT case not found");

        var fields = _mapper.BuildFields(formType, bundle.Sections);
        var schema = _mapper.GetSchema(formType);

        var issues = new List<LtValidationIssue>();
        void Require(string section, string field, bool ok, string msg) {
            if (!ok) issues.Add(new LtValidationIssue("error", section, field, msg));
        }
        void Warn(string section, string field, bool ok, string msg) {
            if (!ok) issues.Add(new LtValidationIssue("warn", section, field, msg));
        }

        var s = bundle.Sections;
        Require("Plaintiff", "Name", !string.IsNullOrWhiteSpace(s.Plaintiff.Name), "Plaintiff/landlord name is required.");
        Require("Defendant", "Name", !string.IsNullOrWhiteSpace(s.Defendant.LastName), "Defendant/tenant last name is required.");
        Require("Premises", "Address", !string.IsNullOrWhiteSpace(s.Premises.AddressLine1), "Premises address is required.");
        Require("Caption", "Court", !string.IsNullOrWhiteSpace(s.Caption.CourtVenue ?? s.Premises.County),
            "Court venue or county is required for the caption.");

        if (formType == LtFormType.VerifiedComplaint || formType == LtFormType.LandlordCaseInformationStatement)
        {
            Require("RentOwed", "Total", (s.RentOwed.Total ?? 0) > 0, "Total rent owed must be greater than 0.");
            Warn("Notices", "NoticeToCease", s.Notices.NoticeToCeaseServed,
                "Notice to cease has not been recorded as served — many NJ courts require this.");
        }
        if (formType == LtFormType.RequestForResidentialWarrantOfRemoval)
        {
            Require("Warrant", "JudgmentDate", s.Warrant.JudgmentDate is not null, "Judgment date is required.");
            Require("Warrant", "TenantStillInPossession", s.Warrant.TenantStillInPossession,
                "Confirm tenant is still in possession before requesting a warrant.");
        }
        if (formType == LtFormType.CertificationOfLeaseAndRegistration)
        {
            Require("Registration", "RegistrationNumber",
                !s.Registration.IsRegisteredMultipleDwelling || !string.IsNullOrWhiteSpace(s.Registration.RegistrationNumber),
                "Registration number is required when 'Registered multiple dwelling' is checked.");
        }

        // Redaction findings — only block public forms.
        var redactionFindings = schema.IsPublicCourtForm
            ? _redaction.Validate(fields)
            : Array.Empty<RedactionFinding>();
        var redactDtos = redactionFindings
            .Select(f => new RedactionFindingDto(f.FieldName, f.Pattern, f.Sample))
            .ToList();
        if (schema.IsPublicCourtForm && redactionFindings.Count > 0)
        {
            issues.Add(new LtValidationIssue("error", "Redaction", "(multiple)",
                $"Redaction blocked: detected {redactionFindings.Count} sensitive identifier(s) on a public form."));
        }

        return new LtValidationSummary(
            IsValid: !issues.Any(i => i.Severity == "error"),
            Issues: issues,
            RedactionFindings: redactDtos);
    }

    // ─── Generate / preview / packet ────────────────────────────────────────
    public async Task<Result<GeneratedDocumentDto>> GenerateFormAsync(Guid ltCaseId, LtFormType formType,
        GenerateFormRequest req, CancellationToken ct = default)
    {
        var (caseId, fields, validation, schema) = await PrepareGenerationAsync(ltCaseId, formType, req, ct);
        if (validation is { IsValid: false } v)
            return Result<GeneratedDocumentDto>.Failure(string.Join(" · ", v.Issues.Where(i => i.Severity == "error").Select(i => i.Message)));

        var pdfBytes = await _filler.FillAsync(formType, fields, ct);
        var caseNumber = await _db.Cases.AsNoTracking().Where(c => c.Id == caseId).Select(c => c.CaseNumber).FirstAsync(ct);
        var sha = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();
        var fileName = $"{formType}_{caseNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        var existing = await _db.GeneratedDocuments
            .Where(g => g.CaseId == caseId && g.FormType == formType)
            .OrderByDescending(g => g.Version).ToListAsync(ct);
        foreach (var e in existing) e.IsCurrent = false;
        var nextVersion = (existing.FirstOrDefault()?.Version ?? 0) + 1;

        using var ms = new MemoryStream(pdfBytes);
        var path = await _storage.SaveAsync($"cases/{caseId:N}/generated", fileName, ms, ct);

        var doc = new GeneratedDocument
        {
            CaseId = caseId, FormType = formType, FileName = fileName, StoragePath = path,
            Version = nextVersion, IsCurrent = true, IsMergedPacket = false,
            Sha256 = sha, SizeBytes = pdfBytes.LongLength, GeneratedBy = _user.Email
        };
        _db.GeneratedDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.GeneratePdf, nameof(GeneratedDocument), doc.Id.ToString(),
            $"Generated {formType} v{nextVersion} for case {caseNumber}", null, ct);

        return Result<GeneratedDocumentDto>.Success(ToGenDto(doc));
    }

    public async Task<Result<(byte[] Bytes, string FileName)>> PreviewFormAsync(Guid ltCaseId, LtFormType formType,
        GenerateFormRequest req, CancellationToken ct = default)
    {
        var (caseId, fields, validation, schema) = await PrepareGenerationAsync(ltCaseId, formType, req, ct);
        if (validation is { IsValid: false } v)
            return Result<(byte[], string)>.Failure(string.Join(" · ", v.Issues.Where(i => i.Severity == "error").Select(i => i.Message)));

        var pdfBytes = await _filler.PreviewAsync(formType, fields, ct);
        var caseNumber = await _db.Cases.AsNoTracking().Where(c => c.Id == caseId).Select(c => c.CaseNumber).FirstAsync(ct);
        var fileName = $"PREVIEW_{formType}_{caseNumber}.pdf";

        await _audit.LogAsync(AuditAction.GeneratePdf, nameof(LtCase), ltCaseId.ToString(),
            $"Previewed {formType} for case {caseNumber}", null, ct);

        return Result<(byte[], string)>.Success((pdfBytes, fileName));
    }

    public async Task<Result<GeneratedDocumentDto>> GeneratePacketAsync(Guid ltCaseId, GeneratePacketRequestNew req, CancellationToken ct = default)
    {
        var lt = await _db.LtCases.Include(x => x.Case).FirstOrDefaultAsync(x => x.Id == ltCaseId, ct);
        if (lt is null) return Result<GeneratedDocumentDto>.Failure("LT case not found");

        var bundle = await GetFormBundleAsync(ltCaseId, ct)
                     ?? throw new InvalidOperationException("Bundle missing");

        var forms = req.Forms is { Count: > 0 }
            ? req.Forms
            : new[] {
                LtFormType.VerifiedComplaint, LtFormType.Summons,
                LtFormType.CertificationByLandlord, LtFormType.CertificationByAttorney,
                LtFormType.CertificationOfLeaseAndRegistration,
                LtFormType.LandlordCaseInformationStatement
            };

        if (req.RequireApproval)
        {
            var anyUnapproved = forms.Any(f => !bundle.Approvals.TryGetValue(f, out var a) || !a.IsApproved);
            if (anyUnapproved)
                return Result<GeneratedDocumentDto>.Failure(
                    "Attorney review required: at least one selected form has not been approved.");
            if (!lt.AttorneyReviewed)
                return Result<GeneratedDocumentDto>.Failure(
                    "Attorney must mark the LT case as reviewed before final packet generation.");
        }

        var pdfs = new List<byte[]>();
        var blockedReasons = new List<string>();
        foreach (var ft in forms)
        {
            var validation = await ValidateAsync(ltCaseId, ft, ct);
            if (!validation.IsValid)
            {
                blockedReasons.Add($"{ft}: {string.Join(", ", validation.Issues.Where(i => i.Severity == "error").Select(i => i.Message))}");
                continue;
            }
            var fields = _mapper.BuildFields(ft, bundle.Sections);
            pdfs.Add(await _filler.FillAsync(ft, fields, ct));
        }
        if (blockedReasons.Count > 0)
            return Result<GeneratedDocumentDto>.Failure("Packet blocked: " + string.Join(" | ", blockedReasons));

        var merged = await _filler.MergeAsync(pdfs, ct);
        var caseNumber = lt.Case.CaseNumber;
        var sha = Convert.ToHexString(SHA256.HashData(merged)).ToLowerInvariant();
        var fileName = $"FilingPacket_{caseNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        var existing = await _db.GeneratedDocuments
            .Where(g => g.CaseId == lt.CaseId && g.IsMergedPacket)
            .OrderByDescending(g => g.Version).ToListAsync(ct);
        foreach (var e in existing) e.IsCurrent = false;
        var nextVersion = (existing.FirstOrDefault()?.Version ?? 0) + 1;

        using var ms = new MemoryStream(merged);
        var path = await _storage.SaveAsync($"cases/{lt.CaseId:N}/packets", fileName, ms, ct);

        var doc = new GeneratedDocument
        {
            CaseId = lt.CaseId, FormType = null, FileName = fileName, StoragePath = path,
            Version = nextVersion, IsCurrent = true, IsMergedPacket = true,
            Sha256 = sha, SizeBytes = merged.LongLength, GeneratedBy = _user.Email
        };
        _db.GeneratedDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.GeneratePdf, nameof(GeneratedDocument), doc.Id.ToString(),
            $"Generated filing packet v{nextVersion} for case {caseNumber}",
            new { forms = forms.Select(f => f.ToString()) }, ct);

        return Result<GeneratedDocumentDto>.Success(ToGenDto(doc));
    }

    public async Task<Result<LtFormApprovalDto>> SetFormApprovalAsync(Guid ltCaseId, LtFormType formType, bool isApproved, CancellationToken ct = default)
    {
        if (!_user.IsInRole(Domain.Common.Roles.Lawyer) && !_user.IsInRole(Domain.Common.Roles.FirmAdmin))
            return Result<LtFormApprovalDto>.Failure("Only Lawyers / FirmAdmins can approve forms.");

        var lt = await _db.LtCases.FirstOrDefaultAsync(x => x.Id == ltCaseId, ct);
        if (lt is null) return Result<LtFormApprovalDto>.Failure("LT case not found");

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(x => x.IdentityUserId == _user.UserId.ToString(), ct);

        // Find or create the per-form row WITHOUT touching DataJson.
        var row = await _db.LtCaseFormData.FirstOrDefaultAsync(f => f.LtCaseId == ltCaseId && f.FormType == formType, ct);
        if (row is null)
        {
            row = new LtCaseFormData { LtCaseId = ltCaseId, FormType = formType, DataJson = "{}" };
            _db.LtCaseFormData.Add(row);
        }
        row.IsApproved = isApproved;
        row.ApprovedAtUtc = isApproved ? DateTime.UtcNow : null;
        row.ApprovedById = isApproved ? profile?.Id : null;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.UpdateCase, nameof(LtCaseFormData), row.Id.ToString(),
            $"{(isApproved ? "Approved" : "Un-approved")} {formType} for LT case {ltCaseId}", null, ct);

        return Result<LtFormApprovalDto>.Success(new LtFormApprovalDto(
            row.IsApproved, row.ApprovedAtUtc,
            profile is null ? null : $"{profile.FirstName} {profile.LastName}"));
    }

    public async Task<Result<LtCaseDetailDto>> MarkAttorneyReviewedAsync(Guid ltCaseId, bool reviewed, CancellationToken ct = default)
    {
        if (!_user.IsInRole(Domain.Common.Roles.Lawyer) && !_user.IsInRole(Domain.Common.Roles.FirmAdmin))
            return Result<LtCaseDetailDto>.Failure("Only Lawyers / FirmAdmins can mark a case as reviewed.");

        var lt = await _db.LtCases.FirstOrDefaultAsync(x => x.Id == ltCaseId, ct);
        if (lt is null) return Result<LtCaseDetailDto>.Failure("LT case not found");

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(x => x.IdentityUserId == _user.UserId.ToString(), ct);
        lt.AttorneyReviewed = reviewed;
        lt.AttorneyReviewedAtUtc = reviewed ? DateTime.UtcNow : null;
        lt.AttorneyReviewedById = reviewed ? profile?.Id : null;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.UpdateCase, nameof(LtCase), lt.Id.ToString(),
            $"Attorney review {(reviewed ? "marked complete" : "cleared")}", null, ct);

        var dto = (await GetAsync(ltCaseId, ct))!;
        return Result<LtCaseDetailDto>.Success(dto);
    }

    public async Task<IReadOnlyList<GeneratedDocumentDto>> GetGeneratedAsync(Guid ltCaseId, CancellationToken ct = default)
    {
        var caseId = await _db.LtCases.AsNoTracking().Where(x => x.Id == ltCaseId)
            .Select(x => x.CaseId).FirstOrDefaultAsync(ct);
        if (caseId == Guid.Empty) return Array.Empty<GeneratedDocumentDto>();

        return await _db.GeneratedDocuments.AsNoTracking()
            .Where(g => g.CaseId == caseId)
            .OrderByDescending(g => g.GeneratedAtUtc)
            .Select(g => new GeneratedDocumentDto(
                g.Id, g.CaseId, g.FormType, g.FileName, g.Version, g.IsMergedPacket, g.IsCurrent,
                g.SizeBytes, g.GeneratedBy, g.GeneratedAtUtc))
            .ToListAsync(ct);
    }

    // ─── helpers ────────────────────────────────────────────────────────────
    private async Task<(Guid caseId, IReadOnlyDictionary<string, string?> fields, LtValidationSummary? validation, LtFormSchemaDto schema)>
        PrepareGenerationAsync(Guid ltCaseId, LtFormType formType, GenerateFormRequest req, CancellationToken ct)
    {
        var lt = await _db.LtCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ltCaseId, ct)
                 ?? throw new KeyNotFoundException("LT case not found");
        var bundle = await GetFormBundleAsync(ltCaseId, ct)
                     ?? throw new InvalidOperationException("Bundle missing");

        var schema = _mapper.GetSchema(formType);
        var fields = _mapper.BuildFields(formType, bundle.Sections, req.Overrides);

        // Validate before generating (or previewing).
        var validation = await ValidateAsync(ltCaseId, formType, ct);
        return (lt.CaseId, fields, validation, schema);
    }

    private static LtFormPhase ResolvePhase(CaseStageCode stage) => stage switch
    {
        CaseStageCode.Filed or CaseStageCode.CourtDateScheduled or CaseStageCode.Judgment or CaseStageCode.Settlement
            => LtFormPhase.TrialCertification,
        CaseStageCode.WarrantRequested => LtFormPhase.Warrant,
        _ => LtFormPhase.Filing,
    };

    private static string PhaseName(LtFormPhase p) => p switch
    {
        LtFormPhase.Filing => "Phase 1 — Filing",
        LtFormPhase.TrialCertification => "Phase 2 — Trial / Certification",
        LtFormPhase.Warrant => "Phase 3 — Warrant",
        _ => p.ToString(),
    };

    private static LtFormDataSections? DeserializeSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<LtFormDataSections>(json, Json); }
        catch { return null; }
    }

    /// <summary>Build a sections object pre-populated from PMS snapshot + attorney settings.</summary>
    private async Task<LtFormDataSections> BuildAutoFilledSectionsAsync(LtCase lt, CancellationToken ct)
    {
        var c = await _db.Cases.AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.AssignedAttorney)
            .Include(x => x.LawFirm).ThenInclude(f => f.AttorneySetting)
            .FirstAsync(x => x.Id == lt.CaseId, ct);

        var sections = new LtFormDataSections();
        var attorney = c.LawFirm.AttorneySetting;

        // Caption
        sections.Caption.CourtName = "Superior Court of New Jersey";
        sections.Caption.CourtVenue = c.CourtVenue ?? attorney?.DefaultCourtVenue;
        sections.Caption.CountyName = lt.PremisesCounty;
        sections.Caption.DocketNumber = c.CourtDocketNumber;
        sections.Caption.CaseNumber = c.CaseNumber;
        sections.Caption.FilingDate = DateTime.UtcNow;

        // Attorney
        sections.Attorney.FirmName = attorney?.FirmDisplayName ?? c.LawFirm.Name;
        sections.Attorney.AttorneyName = attorney?.AttorneyName
            ?? (c.AssignedAttorney != null ? $"{c.AssignedAttorney.FirstName} {c.AssignedAttorney.LastName}" : null);
        sections.Attorney.BarNumber = attorney?.BarNumber ?? c.AssignedAttorney?.BarNumber;
        sections.Attorney.Email = attorney?.AttorneyEmail ?? c.AssignedAttorney?.Email;
        sections.Attorney.Phone = attorney?.AttorneyPhone ?? c.AssignedAttorney?.Phone;
        sections.Attorney.OfficeAddressLine1 = attorney?.OfficeAddressLine1;
        sections.Attorney.OfficeAddressLine2 = attorney?.OfficeAddressLine2;
        sections.Attorney.OfficeCity = attorney?.OfficeCity;
        sections.Attorney.OfficeState = attorney?.OfficeState;
        sections.Attorney.OfficePostalCode = attorney?.OfficePostalCode;

        // Plaintiff (= the client / property manager)
        sections.Plaintiff.Name = lt.LandlordName ?? c.Client.Name;
        sections.Plaintiff.AddressLine1 = c.Client.AddressLine1;
        sections.Plaintiff.AddressLine2 = c.Client.AddressLine2;
        sections.Plaintiff.City = c.Client.City;
        sections.Plaintiff.State = c.Client.State;
        sections.Plaintiff.PostalCode = c.Client.PostalCode;
        sections.Plaintiff.Phone = c.Client.ContactPhone;
        sections.Plaintiff.Email = c.Client.ContactEmail;

        // Premises (already mirrored on LtCase)
        sections.Premises.AddressLine1 = lt.PremisesAddressLine1;
        sections.Premises.AddressLine2 = lt.PremisesAddressLine2;
        sections.Premises.City = lt.PremisesCity;
        sections.Premises.County = lt.PremisesCounty;
        sections.Premises.State = lt.PremisesState ?? "NJ";
        sections.Premises.PostalCode = lt.PremisesPostalCode;

        // Defendant + Lease + RentOwed from PMS snapshot (if any)
        if (!string.IsNullOrWhiteSpace(c.PmsSnapshotJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(c.PmsSnapshotJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("tenant", out var t) && t.ValueKind == JsonValueKind.Object)
                {
                    sections.Defendant.FirstName = t.TryGetProperty("FirstName", out var f) ? f.GetString() : null;
                    sections.Defendant.LastName  = t.TryGetProperty("LastName",  out var l) ? l.GetString() : null;
                    sections.Defendant.Email     = t.TryGetProperty("Email",     out var e) ? e.GetString() : null;
                    sections.Defendant.Phone     = t.TryGetProperty("Phone",     out var p) ? p.GetString() : null;
                }
                if (root.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.Object)
                {
                    sections.Premises.UnitNumber = u.TryGetProperty("UnitNumber", out var v) ? v.GetString() : null;
                }
                if (root.TryGetProperty("lease", out var lj) && lj.ValueKind == JsonValueKind.Object)
                {
                    sections.Lease.StartDate = ParseDate(lj, "StartDate");
                    sections.Lease.EndDate = ParseDate(lj, "EndDate");
                    sections.Lease.IsMonthToMonth = lj.TryGetProperty("IsMonthToMonth", out var mtm) && mtm.ValueKind == JsonValueKind.True;
                    sections.Lease.MonthlyRent = ParseDecimal(lj, "MonthlyRent");
                    sections.Lease.SecurityDeposit = ParseDecimal(lj, "SecurityDeposit");
                    sections.RentOwed.Total = ParseDecimal(lj, "CurrentBalance");
                    sections.RentOwed.AsOfDate = DateTime.UtcNow;
                }
            }
            catch { /* tolerate malformed snapshot */ }
        }

        // RentOwed mirror from LtCase columns when present
        sections.RentOwed.Total ??= lt.RentDue;
        sections.RentOwed.AsOfDate ??= lt.RentDueAsOf;
        sections.AdditionalRent.LateFees = lt.LateFees;
        sections.AdditionalRent.OtherCharges = lt.OtherCharges;

        sections.Registration.IsRegisteredMultipleDwelling = lt.IsRegisteredMultipleDwelling;
        sections.Registration.RegistrationNumber = lt.RegistrationNumber;
        sections.Registration.RegistrationDate = lt.RegistrationDate;

        sections.Certification.AttorneyReviewed = lt.AttorneyReviewed;

        return sections;
    }

    private static DateTime? ParseDate(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), out var d) ? d : null;
    private static decimal? ParseDecimal(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    private static GeneratedDocumentDto ToGenDto(GeneratedDocument g) => new(
        g.Id, g.CaseId, g.FormType, g.FileName, g.Version, g.IsMergedPacket, g.IsCurrent,
        g.SizeBytes, g.GeneratedBy, g.GeneratedAtUtc);
}
