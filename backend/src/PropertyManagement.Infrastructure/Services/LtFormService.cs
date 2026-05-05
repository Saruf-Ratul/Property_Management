using System.Security.Cryptography;
using System.Text.Json;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class LtFormService : ILtFormService
{
    private static readonly HashSet<LtFormType> PublicForms = new()
    {
        LtFormType.VerifiedComplaint,
        LtFormType.Summons,
        LtFormType.LandlordCaseInformationStatement,
        LtFormType.RequestForResidentialWarrantOfRemoval
    };

    private readonly AppDbContext _db;
    private readonly IPdfFormFiller _filler;
    private readonly IRedactionValidator _redaction;
    private readonly IDocumentStorage _storage;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _user;

    public LtFormService(AppDbContext db, IPdfFormFiller filler, IRedactionValidator redaction,
        IDocumentStorage storage, IAuditService audit, ICurrentUser user)
    {
        _db = db; _filler = filler; _redaction = redaction; _storage = storage; _audit = audit; _user = user;
    }

    public async Task<LtFormAutofillResponse> AutofillAsync(Guid caseId, LtFormType formType, CancellationToken ct = default)
    {
        var c = await _db.Cases
            .Include(x => x.LtCase)
            .Include(x => x.Client)
            .Include(x => x.AssignedAttorney)
            .Include(x => x.LawFirm).ThenInclude(f => f.AttorneySetting)
            .FirstOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new KeyNotFoundException("Case not found");

        var attorney = c.LawFirm.AttorneySetting;
        var lt = c.LtCase ?? new LtCase();

        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CaseNumber"] = c.CaseNumber,
            ["DocketNumber"] = c.CourtDocketNumber,
            ["CourtVenue"] = c.CourtVenue ?? attorney?.DefaultCourtVenue,
            ["FilingDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["LandlordName"] = lt.LandlordName ?? c.Client.Name,
            ["LandlordAddress"] = lt.LandlordAddress
                ?? string.Join(", ", new[] { c.Client.AddressLine1, c.Client.City, c.Client.State, c.Client.PostalCode }
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
            ["AttorneyName"] = attorney?.AttorneyName ?? (c.AssignedAttorney != null ? $"{c.AssignedAttorney.FirstName} {c.AssignedAttorney.LastName}" : null),
            ["AttorneyBarNumber"] = attorney?.BarNumber ?? c.AssignedAttorney?.BarNumber,
            ["AttorneyEmail"] = attorney?.AttorneyEmail ?? c.AssignedAttorney?.Email,
            ["AttorneyPhone"] = attorney?.AttorneyPhone ?? c.AssignedAttorney?.Phone,
            ["FirmName"] = attorney?.FirmDisplayName ?? c.LawFirm.Name,
            ["PremisesAddress"] = lt.PremisesAddressLine1,
            ["PremisesCity"] = lt.PremisesCity,
            ["PremisesCounty"] = lt.PremisesCounty,
            ["PremisesState"] = lt.PremisesState ?? "NJ",
            ["PremisesZipCode"] = lt.PremisesPostalCode,
            ["RentDue"] = lt.RentDue?.ToString("0.00"),
            ["LateFees"] = lt.LateFees?.ToString("0.00"),
            ["OtherCharges"] = lt.OtherCharges?.ToString("0.00"),
            ["TotalDue"] = lt.TotalDue?.ToString("0.00"),
            ["RentDueAsOf"] = lt.RentDueAsOf?.ToString("yyyy-MM-dd"),
            ["IsRegisteredMultipleDwelling"] = lt.IsRegisteredMultipleDwelling ? "Yes" : "No",
            ["RegistrationNumber"] = lt.RegistrationNumber
        };

        // Hydrate tenant from snapshot if present
        if (!string.IsNullOrWhiteSpace(c.PmsSnapshotJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(c.PmsSnapshotJson);
                if (doc.RootElement.TryGetProperty("tenant", out var t) && t.ValueKind == JsonValueKind.Object)
                {
                    var first = t.TryGetProperty("FirstName", out var f) ? f.GetString() : null;
                    var last = t.TryGetProperty("LastName", out var l) ? l.GetString() : null;
                    fields["TenantName"] = $"{first} {last}".Trim();
                    if (t.TryGetProperty("Email", out var e)) fields["TenantEmail"] = e.GetString();
                    if (t.TryGetProperty("Phone", out var p)) fields["TenantPhone"] = p.GetString();
                }
            }
            catch { /* tolerate malformed snapshot */ }
        }

        // Form-specific fields
        switch (formType)
        {
            case LtFormType.Summons:
                fields["AppearByDate"] = DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd");
                break;
            case LtFormType.RequestForResidentialWarrantOfRemoval:
                fields["JudgmentDate"] = c.FiledOnUtc?.ToString("yyyy-MM-dd");
                break;
        }

        return new LtFormAutofillResponse(formType, fields);
    }

    public async Task<IReadOnlyList<LtFormDataDto>> ListFormDataAsync(Guid caseId, CancellationToken ct = default)
    {
        return await _db.LtCaseFormData.AsNoTracking()
            .Where(x => x.LtCase.CaseId == caseId)
            .OrderBy(x => x.FormType)
            .Select(x => new LtFormDataDto(
                x.Id, x.LtCaseId, x.FormType, x.DataJson, x.IsApproved, x.ApprovedAtUtc,
                x.ApprovedBy != null ? x.ApprovedBy.FirstName + " " + x.ApprovedBy.LastName : null))
            .ToListAsync(ct);
    }

    public async Task<Result<LtFormDataDto>> SaveFormDataAsync(Guid caseId, SaveLtFormDataRequest req, CancellationToken ct = default)
    {
        var ltCase = await _db.LtCases.FirstOrDefaultAsync(x => x.CaseId == caseId, ct);
        if (ltCase is null) return Result<LtFormDataDto>.Failure("LT case not found");

        var existing = await _db.LtCaseFormData.FirstOrDefaultAsync(x => x.LtCaseId == ltCase.Id && x.FormType == req.FormType, ct);
        if (existing is null)
        {
            existing = new LtCaseFormData { LtCaseId = ltCase.Id, FormType = req.FormType, DataJson = req.DataJson };
            _db.LtCaseFormData.Add(existing);
        }
        else
        {
            existing.DataJson = req.DataJson;
            existing.IsApproved = false;
            existing.ApprovedAtUtc = null;
            existing.ApprovedById = null;
        }
        await _db.SaveChangesAsync(ct);
        return Result<LtFormDataDto>.Success(new LtFormDataDto(existing.Id, ltCase.Id, existing.FormType, existing.DataJson, existing.IsApproved, existing.ApprovedAtUtc, null));
    }

    public async Task<Result<LtFormDataDto>> ApproveAsync(Guid caseId, ApproveLtFormRequest req, CancellationToken ct = default)
    {
        if (!(_user.IsInRole(Domain.Common.Roles.Lawyer) || _user.IsInRole(Domain.Common.Roles.FirmAdmin)))
            return Result<LtFormDataDto>.Failure("Only Lawyers / FirmAdmins can approve forms");

        var ltCase = await _db.LtCases.FirstOrDefaultAsync(x => x.CaseId == caseId, ct);
        if (ltCase is null) return Result<LtFormDataDto>.Failure("LT case not found");
        var fd = await _db.LtCaseFormData.FirstOrDefaultAsync(x => x.LtCaseId == ltCase.Id && x.FormType == req.FormType, ct);
        if (fd is null) return Result<LtFormDataDto>.Failure("Form data not found");

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(x => x.IdentityUserId == _user.UserId.ToString(), ct);
        fd.IsApproved = true;
        fd.ApprovedAtUtc = DateTime.UtcNow;
        fd.ApprovedById = profile?.Id;

        ltCase.AttorneyReviewed = true;
        ltCase.AttorneyReviewedAtUtc = DateTime.UtcNow;
        ltCase.AttorneyReviewedById = profile?.Id;

        await _db.SaveChangesAsync(ct);
        return Result<LtFormDataDto>.Success(new LtFormDataDto(fd.Id, ltCase.Id, fd.FormType, fd.DataJson, fd.IsApproved, fd.ApprovedAtUtc, profile?.FullName));
    }

    public async Task<Result<GeneratedDocumentDto>> GenerateAsync(Guid caseId, GenerateLtPdfRequest req, CancellationToken ct = default)
    {
        var ltCase = await _db.LtCases.Include(x => x.Case).FirstOrDefaultAsync(x => x.CaseId == caseId, ct);
        if (ltCase is null) return Result<GeneratedDocumentDto>.Failure("LT case not found");

        var fd = await _db.LtCaseFormData.FirstOrDefaultAsync(x => x.LtCaseId == ltCase.Id && x.FormType == req.FormType, ct);
        IReadOnlyDictionary<string, string?> fields;
        if (fd is null)
        {
            var auto = await AutofillAsync(caseId, req.FormType, ct);
            fields = auto.Fields;
        }
        else
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, string?>>(fd.DataJson) ?? new();
        }

        // Redaction validation for public forms
        if (PublicForms.Contains(req.FormType))
        {
            var findings = _redaction.Validate(fields);
            if (findings.Count > 0)
            {
                var msg = "Form blocked: detected possible sensitive identifiers in fields: " +
                          string.Join(", ", findings.Select(f => $"{f.FieldName} ({f.Pattern})"));
                return Result<GeneratedDocumentDto>.Failure(msg);
            }
        }

        // Attorney review required
        if (fd is not null && !fd.IsApproved && !_user.IsInRole(Domain.Common.Roles.Lawyer) && !_user.IsInRole(Domain.Common.Roles.FirmAdmin))
            return Result<GeneratedDocumentDto>.Failure("Form data must be approved by an attorney before generation");

        var pdfBytes = await _filler.FillAsync(req.FormType, fields, ct);
        var sha = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();
        var fileName = $"{req.FormType}_{ltCase.Case.CaseNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        // Compute next version
        var existing = await _db.GeneratedDocuments
            .Where(x => x.CaseId == caseId && x.FormType == req.FormType)
            .OrderByDescending(x => x.Version).ToListAsync(ct);
        var nextVersion = (existing.FirstOrDefault()?.Version ?? 0) + 1;
        foreach (var e in existing) e.IsCurrent = false;

        using var ms = new MemoryStream(pdfBytes);
        var path = await _storage.SaveAsync($"cases/{caseId:N}/generated", fileName, ms, ct);

        var doc = new GeneratedDocument
        {
            CaseId = caseId, FormType = req.FormType, FileName = fileName, StoragePath = path,
            Version = nextVersion, IsCurrent = true, IsMergedPacket = false,
            Sha256 = sha, SizeBytes = pdfBytes.LongLength, GeneratedBy = _user.Email
        };
        _db.GeneratedDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.GeneratePdf, nameof(GeneratedDocument), doc.Id.ToString(),
            $"Generated {req.FormType} v{nextVersion} for case {ltCase.Case.CaseNumber}", null, ct);

        return Result<GeneratedDocumentDto>.Success(ToDto(doc));
    }

    public async Task<Result<GeneratedDocumentDto>> GeneratePacketAsync(Guid caseId, GenerateLtPacketRequest req, CancellationToken ct = default)
    {
        var ltCase = await _db.LtCases.Include(x => x.Case).FirstOrDefaultAsync(x => x.CaseId == caseId, ct);
        if (ltCase is null) return Result<GeneratedDocumentDto>.Failure("LT case not found");
        if (req.Forms.Count == 0) return Result<GeneratedDocumentDto>.Failure("No forms specified");

        if (!ltCase.AttorneyReviewed)
            return Result<GeneratedDocumentDto>.Failure("Attorney must review case before final packet generation");

        var pdfs = new List<byte[]>();
        foreach (var ft in req.Forms)
        {
            var fd = await _db.LtCaseFormData.FirstOrDefaultAsync(x => x.LtCaseId == ltCase.Id && x.FormType == ft, ct);
            IReadOnlyDictionary<string, string?> fields = fd is null
                ? (await AutofillAsync(caseId, ft, ct)).Fields
                : (JsonSerializer.Deserialize<Dictionary<string, string?>>(fd.DataJson) ?? new());

            if (PublicForms.Contains(ft))
            {
                var findings = _redaction.Validate(fields);
                if (findings.Count > 0)
                    return Result<GeneratedDocumentDto>.Failure($"Form {ft} blocked: redaction validation failed");
            }
            pdfs.Add(await _filler.FillAsync(ft, fields, ct));
        }

        var merged = await _filler.MergeAsync(pdfs, ct);
        var sha = Convert.ToHexString(SHA256.HashData(merged)).ToLowerInvariant();
        var fileName = $"FilingPacket_{ltCase.Case.CaseNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        var existing = await _db.GeneratedDocuments
            .Where(x => x.CaseId == caseId && x.IsMergedPacket)
            .OrderByDescending(x => x.Version).ToListAsync(ct);
        foreach (var e in existing) e.IsCurrent = false;
        var nextVersion = (existing.FirstOrDefault()?.Version ?? 0) + 1;

        using var ms = new MemoryStream(merged);
        var path = await _storage.SaveAsync($"cases/{caseId:N}/packets", fileName, ms, ct);

        var doc = new GeneratedDocument
        {
            CaseId = caseId, FormType = null, FileName = fileName, StoragePath = path,
            Version = nextVersion, IsCurrent = true, IsMergedPacket = true,
            Sha256 = sha, SizeBytes = merged.LongLength, GeneratedBy = _user.Email
        };
        _db.GeneratedDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.GeneratePdf, nameof(GeneratedDocument), doc.Id.ToString(),
            $"Generated filing packet v{nextVersion} for case {ltCase.Case.CaseNumber}", new { req.Forms }, ct);

        return Result<GeneratedDocumentDto>.Success(ToDto(doc));
    }

    public async Task<IReadOnlyList<GeneratedDocumentDto>> GetGeneratedAsync(Guid caseId, CancellationToken ct = default) =>
        await _db.GeneratedDocuments.AsNoTracking().Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.GeneratedAtUtc)
            .Select(x => new GeneratedDocumentDto(x.Id, x.CaseId, x.FormType, x.FileName, x.Version, x.IsMergedPacket, x.IsCurrent, x.SizeBytes, x.GeneratedBy, x.GeneratedAtUtc))
            .ToListAsync(ct);

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(Guid generatedDocumentId, CancellationToken ct = default)
    {
        var doc = await _db.GeneratedDocuments.FirstOrDefaultAsync(x => x.Id == generatedDocumentId, ct);
        if (doc is null) return null;
        await _audit.LogAsync(AuditAction.DownloadPdf, nameof(GeneratedDocument), doc.Id.ToString(),
            $"Downloaded {doc.FileName}", null, ct);
        var s = await _storage.OpenAsync(doc.StoragePath, ct);
        return (s, "application/pdf", doc.FileName);
    }

    private static GeneratedDocumentDto ToDto(GeneratedDocument doc) =>
        new(doc.Id, doc.CaseId, doc.FormType, doc.FileName, doc.Version, doc.IsMergedPacket, doc.IsCurrent, doc.SizeBytes, doc.GeneratedBy, doc.GeneratedAtUtc);
}
