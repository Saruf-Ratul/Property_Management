using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
public class CasesController : ControllerBase
{
    private const string FirmStaff = $"{Roles.FirmAdmin},{Roles.Lawyer},{Roles.Paralegal}";
    private const string LawyerOrAdmin = $"{Roles.FirmAdmin},{Roles.Lawyer}";

    private readonly ICaseService _cases;
    private readonly IValidator<CreateCaseRequest> _createVal;

    public CasesController(ICaseService cases, IValidator<CreateCaseRequest> createVal)
    {
        _cases = cases; _createVal = createVal;
    }

    // ─── List & lookup ──────────────────────────────────────────────────────
    [HttpGet]
    public Task<PagedResult<CaseListItemDto>> List(
        [FromQuery] PageRequest page,
        [FromQuery] Guid? clientId,
        [FromQuery] CaseStageCode? stage,
        [FromQuery] CaseStatusCode? status,
        [FromQuery] Guid? assignedAttorneyId,
        [FromQuery] Guid? assignedParalegalId,
        [FromQuery] DateTime? createdFrom,
        [FromQuery] DateTime? createdTo,
        [FromQuery] CaseListTab tab = CaseListTab.All,
        CancellationToken ct = default)
        => _cases.ListAsync(page, new CaseListFilter
        {
            ClientId = clientId, Stage = stage, Status = status,
            AssignedAttorneyId = assignedAttorneyId, AssignedParalegalId = assignedParalegalId,
            CreatedFrom = createdFrom, CreatedTo = createdTo, Tab = tab,
        }, ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await _cases.GetAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet("{id:guid}/snapshot")]
    public async Task<IActionResult> Snapshot(Guid id, CancellationToken ct)
    {
        var s = await _cases.GetSnapshotAsync(id, ct);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpGet("stages")]
    public Task<IReadOnlyList<CaseStageDto>> Stages(CancellationToken ct) => _cases.GetStagesAsync(ct);

    [HttpGet("statuses")]
    public Task<IReadOnlyList<CaseStatusDto>> Statuses(CancellationToken ct) => _cases.GetStatusesAsync(ct);

    [HttpGet("assignees")]
    [Authorize(Roles = FirmStaff)]
    public Task<IReadOnlyList<AssigneeDto>> Assignees(CancellationToken ct) => _cases.GetAssigneesAsync(ct);

    // ─── Create & update ────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = FirmStaff)]
    public async Task<IActionResult> Create([FromBody] CreateCaseRequest req, CancellationToken ct)
    {
        await _createVal.ValidateAndThrowAsync(req, ct);
        var r = await _cases.CreateAsync(req, ct);
        return r.IsSuccess ? CreatedAtAction(nameof(Get), new { id = r.Value!.Id }, r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>
    /// Create a case from a selected PMS lease. Looks up the related tenant/unit/property and
    /// captures a full PMS snapshot at intake time so subsequent PMS changes do not mutate the case.
    /// </summary>
    [HttpPost("create-from-pms")]
    [Authorize(Roles = FirmStaff)]
    public async Task<IActionResult> CreateFromPms([FromBody] CreateCaseFromPmsRequest req, CancellationToken ct)
    {
        var r = await _cases.CreateFromPmsAsync(req, ct);
        return r.IsSuccess ? CreatedAtAction(nameof(Get), new { id = r.Value!.Id }, r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = FirmStaff)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCaseRequest req, CancellationToken ct)
    {
        var r = await _cases.UpdateAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Move the case to another lifecycle stage (Intake / Draft / Filed / Judgment / etc.).</summary>
    [HttpPut("{id:guid}/stage")]
    [Authorize(Roles = FirmStaff)]
    public async Task<IActionResult> ChangeStage(Guid id, [FromBody] ChangeCaseStageRequest req, CancellationToken ct)
    {
        var r = await _cases.ChangeStageAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Change the high-level status (Open / OnHold / Closed / Cancelled).</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = FirmStaff)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeCaseStatusRequest req, CancellationToken ct)
    {
        var r = await _cases.ChangeStatusAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Assign / re-assign attorney and/or paralegal. Pass null to unassign.</summary>
    [HttpPut("{id:guid}/assign")]
    [Authorize(Roles = LawyerOrAdmin)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignCaseRequest req, CancellationToken ct)
    {
        var r = await _cases.AssignAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Close the case (sets stage=Closed and status=Closed, records outcome and notes).</summary>
    [HttpPut("{id:guid}/close")]
    [Authorize(Roles = LawyerOrAdmin)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseCaseRequest req, CancellationToken ct)
    {
        var r = await _cases.CloseAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{id:guid}/snapshot")]
    [Authorize(Roles = FirmStaff)]
    public async Task<IActionResult> SnapshotPms(Guid id, CancellationToken ct)
    {
        var r = await _cases.SnapshotPmsAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ─── Comments ───────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/comments")]
    public Task<IReadOnlyList<CaseCommentDto>> Comments(Guid id, CancellationToken ct) => _cases.GetCommentsAsync(id, ct);

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateCaseCommentRequest req, CancellationToken ct)
    {
        var r = await _cases.AddCommentAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ─── Payments ───────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/payments")]
    public Task<IReadOnlyList<CasePaymentDto>> Payments(Guid id, CancellationToken ct) => _cases.GetPaymentsAsync(id, ct);

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = FirmStaff)]
    public async Task<IActionResult> AddPayment(Guid id, [FromBody] CreateCasePaymentRequest req, CancellationToken ct)
    {
        var r = await _cases.AddPaymentAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ─── Documents ──────────────────────────────────────────────────────────
    [HttpGet("{id:guid}/documents")]
    public Task<IReadOnlyList<CaseDocumentDto>> Documents(Guid id, CancellationToken ct) => _cases.GetDocumentsAsync(id, ct);

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(50_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid id, [FromForm] UploadDocumentForm form, CancellationToken ct = default)
    {
        var file = form.File;
        if (file is null || file.Length == 0) return BadRequest(new { error = "File required" });
        await using var stream = file.OpenReadStream();
        var r = await _cases.UploadDocumentAsync(id, file.FileName, file.ContentType, file.Length, stream,
            form.DocumentType ?? Domain.Enums.DocumentType.Other, form.Description, form.IsClientVisible, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ─── Activity (singular and plural aliases) ─────────────────────────────
    [HttpGet("{id:guid}/activity")]
    public Task<IReadOnlyList<CaseActivityDto>> Activity(Guid id, CancellationToken ct) => _cases.GetActivityAsync(id, ct);

    [HttpGet("{id:guid}/activities")]
    public Task<IReadOnlyList<CaseActivityDto>> Activities(Guid id, CancellationToken ct) => _cases.GetActivityAsync(id, ct);
}

/// <summary>Multipart form payload for case document upload.</summary>
public class UploadDocumentForm
{
    /// <summary>The file to upload.</summary>
    public IFormFile? File { get; set; }
    /// <summary>Document classification.</summary>
    public DocumentType? DocumentType { get; set; }
    /// <summary>Optional description shown in the UI.</summary>
    public string? Description { get; set; }
    /// <summary>If true, the document is visible to ClientAdmin/ClientUser portal users.</summary>
    public bool IsClientVisible { get; set; } = true;
}
