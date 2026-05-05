using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/lt-forms")]
[Authorize(Roles = $"{Roles.FirmAdmin},{Roles.Lawyer},{Roles.Paralegal}")]
public class LtFormsController : ControllerBase
{
    private readonly ILtFormService _svc;
    public LtFormsController(ILtFormService svc) => _svc = svc;

    [HttpGet]
    public Task<IReadOnlyList<LtFormDataDto>> List(Guid caseId, CancellationToken ct) => _svc.ListFormDataAsync(caseId, ct);

    [HttpGet("autofill/{formType}")]
    public Task<LtFormAutofillResponse> Autofill(Guid caseId, LtFormType formType, CancellationToken ct)
        => _svc.AutofillAsync(caseId, formType, ct);

    [HttpPut]
    public async Task<IActionResult> Save(Guid caseId, [FromBody] SaveLtFormDataRequest req, CancellationToken ct)
    {
        var r = await _svc.SaveFormDataAsync(caseId, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("approve")]
    [Authorize(Roles = $"{Roles.FirmAdmin},{Roles.Lawyer}")]
    public async Task<IActionResult> Approve(Guid caseId, [FromBody] ApproveLtFormRequest req, CancellationToken ct)
    {
        var r = await _svc.ApproveAsync(caseId, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(Guid caseId, [FromBody] GenerateLtPdfRequest req, CancellationToken ct)
    {
        var r = await _svc.GenerateAsync(caseId, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("packet")]
    [Authorize(Roles = $"{Roles.FirmAdmin},{Roles.Lawyer}")]
    public async Task<IActionResult> Packet(Guid caseId, [FromBody] GenerateLtPacketRequest req, CancellationToken ct)
    {
        var r = await _svc.GeneratePacketAsync(caseId, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpGet("generated")]
    public Task<IReadOnlyList<GeneratedDocumentDto>> Generated(Guid caseId, CancellationToken ct) => _svc.GetGeneratedAsync(caseId, ct);
}

[ApiController]
[Route("api/generated-documents")]
[Authorize]
public class GeneratedDocumentsController : ControllerBase
{
    private readonly ILtFormService _svc;
    public GeneratedDocumentsController(ILtFormService svc) => _svc = svc;

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var r = await _svc.DownloadAsync(id, ct);
        return r is null ? NotFound() : File(r.Value.Stream, r.Value.ContentType, r.Value.FileName);
    }
}
