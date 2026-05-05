using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

/// <summary>
/// Client Portal — authenticated UI for property-management company users (ClientAdmin / ClientUser).
/// Every endpoint is scoped to the authenticated user's <c>ClientId</c> claim by the service layer.
/// FirmAdmin / Lawyer / Paralegal / Auditor users are explicitly *excluded* from this controller —
/// they use the firm-side admin endpoints under /api/cases, /api/dashboard, etc.
/// </summary>
[ApiController]
[Route("api/client-portal")]
[Authorize(Roles = $"{Roles.ClientAdmin},{Roles.ClientUser}")]
[Produces("application/json")]
public class ClientPortalController : ControllerBase
{
    private readonly IClientPortalService _svc;
    public ClientPortalController(IClientPortalService svc) => _svc = svc;

    /// <summary>Aggregate dashboard for the client — KPIs, upcoming court dates, recent activity.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ClientPortalDashboardDto), 200)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var r = await _svc.GetDashboardAsync(ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>List the client's cases with optional stage / status / search filters.</summary>
    [HttpGet("cases")]
    public async Task<IActionResult> ListCases(
        [FromQuery] PageRequest page,
        [FromQuery] CaseStageCode? stage,
        [FromQuery] CaseStatusCode? status,
        CancellationToken ct = default)
    {
        var r = await _svc.ListCasesAsync(page, stage, status, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Get a single case (PMS-internal fields are stripped before returning to the client).</summary>
    [HttpGet("cases/{id:guid}")]
    public async Task<IActionResult> GetCase(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetCaseAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Activity timeline for a single case.</summary>
    [HttpGet("cases/{id:guid}/timeline")]
    public async Task<IActionResult> Timeline(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetCaseActivityAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Comments visible to the client (internal-only comments are filtered out).</summary>
    [HttpGet("cases/{id:guid}/comments")]
    public async Task<IActionResult> Comments(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetCaseCommentsAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Documents the firm has marked client-visible.</summary>
    [HttpGet("cases/{id:guid}/documents")]
    public async Task<IActionResult> Documents(Guid id, CancellationToken ct)
    {
        var r = await _svc.GetCaseDocumentsAsync(id, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Recent client-facing notifications across all of the client's cases.</summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] int take = 25, CancellationToken ct = default)
    {
        var r = await _svc.GetNotificationsAsync(take, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Post a comment from the client portal. ClientAdmin only — ClientUser is read-only.</summary>
    [HttpPost("cases/{id:guid}/comments")]
    [Authorize(Roles = Roles.ClientAdmin)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] ClientPortalCommentRequest req, CancellationToken ct)
    {
        var r = await _svc.AddCommentAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Upload a supporting document. ClientAdmin only — ClientUser is read-only.</summary>
    [HttpPost("cases/{id:guid}/documents")]
    [Authorize(Roles = Roles.ClientAdmin)]
    [RequestSizeLimit(50_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid id, [FromForm] PortalUploadForm form, CancellationToken ct = default)
    {
        var file = form.File;
        if (file is null || file.Length == 0) return BadRequest(new { error = "File required" });
        await using var stream = file.OpenReadStream();
        var r = await _svc.UploadDocumentAsync(id, file.FileName, file.ContentType, file.Length,
            stream, form.Description, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }
}

/// <summary>Multipart form payload for client-portal document uploads.</summary>
public class PortalUploadForm
{
    public IFormFile? File { get; set; }
    public string? Description { get; set; }
}
