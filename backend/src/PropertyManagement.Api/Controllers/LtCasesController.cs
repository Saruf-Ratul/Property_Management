using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

/// <summary>
/// NJ Landlord-Tenant case form-automation module. Operates on LT-case ids directly
/// (the LtCase row is the form-automation overlay attached to a Case).
/// </summary>
[ApiController]
[Route("api/lt-cases")]
[Authorize(Roles = $"{Roles.FirmAdmin},{Roles.Lawyer},{Roles.Paralegal}")]
[Produces("application/json")]
public class LtCasesController : ControllerBase
{
    private const string LawyerOrAdmin = $"{Roles.FirmAdmin},{Roles.Lawyer}";
    private readonly ILtCaseService _svc;
    public LtCasesController(ILtCaseService svc) => _svc = svc;

    /// <summary>List LT cases with optional filtering by phase and client.</summary>
    [HttpGet]
    public Task<PagedResult<LtCaseSummaryDto>> List(
        [FromQuery] PageRequest page,
        [FromQuery] LtFormPhase? phase,
        [FromQuery] Guid? clientId,
        CancellationToken ct = default)
        => _svc.ListAsync(page, phase, clientId, ct);

    /// <summary>Get a single LT case (header info — call /form-data for the structured bundle).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Get the metadata (phase, public flag, sections) for all 7 NJ LT forms.</summary>
    [HttpGet("schemas")]
    public Task<IReadOnlyList<LtFormSchemaDto>> Schemas(CancellationToken ct) => _svc.GetSchemasAsync(ct);

    /// <summary>Create (or fetch the existing) LT case overlay for a base case.</summary>
    [HttpPost("create-from-case/{caseId:guid}")]
    public async Task<IActionResult> CreateFromCase(Guid caseId, CancellationToken ct)
    {
        var r = await _svc.CreateFromCaseAsync(caseId, ct);
        return r.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = r.Value!.Id }, r.Value)
            : BadRequest(new { error = r.Error });
    }

    /// <summary>Get the structured form-data bundle (caption, attorney, plaintiff, defendant, premises, lease, rent, notices, registration, certification, warrant) plus per-form approval state.</summary>
    [HttpGet("{id:guid}/form-data")]
    public async Task<IActionResult> GetFormData(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetFormBundleAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Save the entire form-data bundle. Saving invalidates per-form approvals.</summary>
    [HttpPut("{id:guid}/form-data")]
    public async Task<IActionResult> SaveFormData(Guid id, [FromBody] SaveLtFormBundleRequest req, CancellationToken ct)
    {
        var r = await _svc.SaveFormBundleAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Run validation + redaction checks for one form without generating anything.</summary>
    [HttpGet("{id:guid}/validate/{formType}")]
    public Task<LtValidationSummary> Validate(Guid id, LtFormType formType, CancellationToken ct)
        => _svc.ValidateAsync(id, formType, ct);

    /// <summary>Generate a single PDF for the specified form. Set <c>preview=true</c> to return PDF bytes inline without persisting.</summary>
    [HttpPost("{id:guid}/generate-form/{formType}")]
    public async Task<IActionResult> GenerateForm(
        Guid id, LtFormType formType,
        [FromBody] GenerateFormRequest? req,
        [FromQuery] bool preview = false,
        CancellationToken ct = default)
    {
        var body = req ?? new GenerateFormRequest(null, preview);
        if (preview || body.Preview)
        {
            var p = await _svc.PreviewFormAsync(id, formType, body, ct);
            return p.IsSuccess
                ? File(p.Value.Bytes, "application/pdf", p.Value.FileName)
                : BadRequest(new { error = p.Error });
        }
        var r = await _svc.GenerateFormAsync(id, formType, body, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Generate the merged filing packet. Requires attorney review unless <c>requireApproval=false</c>.</summary>
    [HttpPost("{id:guid}/generate-packet")]
    [Authorize(Roles = LawyerOrAdmin)]
    public async Task<IActionResult> GeneratePacket(Guid id, [FromBody] GeneratePacketRequestNew? req, CancellationToken ct)
    {
        var r = await _svc.GeneratePacketAsync(id, req ?? new GeneratePacketRequestNew(null), ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Approve or un-approve a single form (Lawyer / FirmAdmin only).</summary>
    [HttpPut("{id:guid}/forms/{formType}/approval")]
    [Authorize(Roles = LawyerOrAdmin)]
    public async Task<IActionResult> SetApproval(Guid id, LtFormType formType, [FromBody] SetApprovalBody body, CancellationToken ct)
    {
        var r = await _svc.SetFormApprovalAsync(id, formType, body.IsApproved, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Mark or clear the LT case attorney-review flag (precondition for final packet generation).</summary>
    [HttpPut("{id:guid}/attorney-review")]
    [Authorize(Roles = LawyerOrAdmin)]
    public async Task<IActionResult> SetAttorneyReview(Guid id, [FromBody] SetReviewBody body, CancellationToken ct)
    {
        var r = await _svc.MarkAttorneyReviewedAsync(id, body.Reviewed, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>List all generated documents (forms + packets) with version history.</summary>
    [HttpGet("{id:guid}/generated-documents")]
    public Task<IReadOnlyList<GeneratedDocumentDto>> Generated(Guid id, CancellationToken ct)
        => _svc.GetGeneratedAsync(id, ct);
}

public record SetApprovalBody(bool IsApproved);
public record SetReviewBody(bool Reviewed);
