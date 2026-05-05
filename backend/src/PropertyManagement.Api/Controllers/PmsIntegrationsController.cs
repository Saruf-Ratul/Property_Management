using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

/// <summary>
/// PMS Integration module — manages connections to external Property Management Systems.
///
/// Authorization:
///  • FirmAdmin / Lawyer  — full read/write (create, update, test, sync).
///  • Paralegal           — read-only (list, get, sync-logs).
///  • ClientAdmin / ClientUser / Auditor — no access.
/// </summary>
[ApiController]
[Route("api/pms-integrations")]
[Authorize(Roles = $"{Roles.FirmAdmin},{Roles.Lawyer},{Roles.Paralegal}")]
[Produces("application/json")]
public class PmsIntegrationsController : ControllerBase
{
    private const string ManageRoles = $"{Roles.FirmAdmin},{Roles.Lawyer}";

    private readonly IPmsIntegrationService _svc;
    private readonly IValidator<CreatePmsIntegrationRequest> _createVal;
    private readonly IValidator<UpdatePmsIntegrationRequest> _updateVal;
    private readonly IValidator<PmsConnectionTestRequest> _testVal;
    private readonly IValidator<PmsSyncRequest> _syncVal;

    public PmsIntegrationsController(
        IPmsIntegrationService svc,
        IValidator<CreatePmsIntegrationRequest> createVal,
        IValidator<UpdatePmsIntegrationRequest> updateVal,
        IValidator<PmsConnectionTestRequest> testVal,
        IValidator<PmsSyncRequest> syncVal)
    {
        _svc = svc; _createVal = createVal; _updateVal = updateVal;
        _testVal = testVal; _syncVal = syncVal;
    }

    // ─── READ ────────────────────────────────────────────────────────────────

    /// <summary>List PMS integrations. Open to FirmAdmin/Lawyer/Paralegal.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PmsIntegrationDto>>), 200)]
    public async Task<ActionResult<ApiResponse<PagedResult<PmsIntegrationDto>>>> List(
        [FromQuery] PageRequest page, [FromQuery] Guid? clientId, CancellationToken ct)
    {
        var data = await _svc.ListAsync(page, clientId, ct);
        return Ok(ApiResponse<PagedResult<PmsIntegrationDto>>.Ok(data,
            $"Returned {data.Items.Count} of {data.TotalCount} integration(s)."));
    }

    /// <summary>Get a single PMS integration.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PmsIntegrationDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<PmsIntegrationDto>), 404)]
    public async Task<ActionResult<ApiResponse<PmsIntegrationDto>>> Get(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetAsync(id, ct);
        return dto is null
            ? NotFound(ApiResponse<PmsIntegrationDto>.Fail("Integration not found."))
            : Ok(ApiResponse<PmsIntegrationDto>.Ok(dto));
    }

    /// <summary>Recent sync log entries for an integration. Default 25, max 200.</summary>
    [HttpGet("{id:guid}/sync-logs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SyncLogDto>>), 200)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SyncLogDto>>>> SyncLogs(
        Guid id, [FromQuery] int take = 25, CancellationToken ct = default)
    {
        var logs = await _svc.GetSyncLogsAsync(id, take, ct);
        return Ok(ApiResponse<IReadOnlyList<SyncLogDto>>.Ok(logs, $"{logs.Count} sync log entries."));
    }

    // ─── WRITE / OPERATE ─────────────────────────────────────────────────────

    /// <summary>Create a new PMS integration. Stored credentials are encrypted with DataProtection.</summary>
    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(ApiResponse<PmsIntegrationDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<PmsIntegrationDto>), 400)]
    public async Task<ActionResult<ApiResponse<PmsIntegrationDto>>> Create(
        [FromBody] CreatePmsIntegrationRequest req, CancellationToken ct)
    {
        var validation = await _createVal.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<PmsIntegrationDto>.Fail(
                "Validation failed.", validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList()));

        var r = await _svc.CreateAsync(req, ct);
        if (!r.IsSuccess)
            return BadRequest(ApiResponse<PmsIntegrationDto>.Fail(r.Error ?? "Create failed."));

        return CreatedAtAction(nameof(Get), new { id = r.Value!.Id },
            ApiResponse<PmsIntegrationDto>.Ok(r.Value, "Integration created."));
    }

    /// <summary>Update integration metadata, credentials, sync interval, or active flag.</summary>
    /// <remarks>Send an empty <c>password</c> to keep the previously stored secret unchanged.</remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(ApiResponse<PmsIntegrationDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<PmsIntegrationDto>), 400)]
    [ProducesResponseType(typeof(ApiResponse<PmsIntegrationDto>), 404)]
    public async Task<ActionResult<ApiResponse<PmsIntegrationDto>>> Update(
        Guid id, [FromBody] UpdatePmsIntegrationRequest req, CancellationToken ct)
    {
        var validation = await _updateVal.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<PmsIntegrationDto>.Fail(
                "Validation failed.", validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList()));

        var r = await _svc.UpdateAsync(id, req, ct);
        if (!r.IsSuccess)
            return NotFound(ApiResponse<PmsIntegrationDto>.Fail(r.Error ?? "Update failed."));
        return Ok(ApiResponse<PmsIntegrationDto>.Ok(r.Value!, "Integration updated."));
    }

    /// <summary>
    /// Test a PMS connection. Two modes:
    ///  • <c>POST /api/pms-integrations/{id}/test</c>           — uses the stored credentials of integration <c>{id}</c>.
    ///  • <c>POST /api/pms-integrations/test</c> with body      — ad-hoc test using literal credentials (nothing is persisted).
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(ApiResponse<PmsConnectionTestResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<PmsConnectionTestResult>), 404)]
    public async Task<ActionResult<ApiResponse<PmsConnectionTestResult>>> Test(Guid id, CancellationToken ct)
    {
        var r = await _svc.TestAsync(id, ct);
        if (!r.IsSuccess)
            return NotFound(ApiResponse<PmsConnectionTestResult>.Fail(r.Error ?? "Test failed."));
        var msg = r.Value!.IsConnected ? "Connection OK." : $"Connection failed: {r.Value.Message}";
        return Ok(ApiResponse<PmsConnectionTestResult>.Ok(r.Value, msg));
    }

    /// <inheritdoc cref="Test(System.Guid, System.Threading.CancellationToken)"/>
    [HttpPost("test")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(ApiResponse<PmsConnectionTestResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<PmsConnectionTestResult>), 400)]
    public async Task<ActionResult<ApiResponse<PmsConnectionTestResult>>> TestAdHoc(
        [FromBody] PmsConnectionTestRequest req, CancellationToken ct)
    {
        var validation = await _testVal.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<PmsConnectionTestResult>.Fail(
                "Validation failed.", validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList()));

        var r = await _svc.TestAdHocAsync(req, ct);
        if (!r.IsSuccess)
            return BadRequest(ApiResponse<PmsConnectionTestResult>.Fail(r.Error ?? "Test failed."));
        var msg = r.Value!.IsConnected ? "Connection OK." : $"Connection failed: {r.Value.Message}";
        return Ok(ApiResponse<PmsConnectionTestResult>.Ok(r.Value, msg));
    }

    /// <summary>
    /// Trigger a sync. By default queues a Hangfire background job for a full sync.
    /// Send a body with selected entity flags to scope it. Set <c>RunInBackground=false</c> to run inline and wait for the result.
    /// </summary>
    [HttpPost("{id:guid}/sync")]
    [Authorize(Roles = ManageRoles)]
    [ProducesResponseType(typeof(ApiResponse<PmsSyncResult>), 202)]
    [ProducesResponseType(typeof(ApiResponse<PmsSyncResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<PmsSyncResult>), 400)]
    [ProducesResponseType(typeof(ApiResponse<PmsSyncResult>), 404)]
    public async Task<ActionResult<ApiResponse<PmsSyncResult>>> Sync(
        Guid id, [FromBody] PmsSyncRequest? req, CancellationToken ct)
    {
        var scope = req ?? new PmsSyncRequest();
        var validation = await _syncVal.ValidateAsync(scope, ct);
        if (!validation.IsValid)
            return BadRequest(ApiResponse<PmsSyncResult>.Fail(
                "Validation failed.", validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList()));

        var r = await _svc.TriggerSyncAsync(id, scope, ct);
        if (!r.IsSuccess)
            return NotFound(ApiResponse<PmsSyncResult>.Fail(r.Error ?? "Sync failed."));

        if (scope.RunInBackground)
        {
            return Accepted(ApiResponse<PmsSyncResult>.Ok(r.Value!,
                $"Sync queued (jobId={r.Value!.BackgroundJobId})."));
        }
        return Ok(ApiResponse<PmsSyncResult>.Ok(r.Value!, $"Sync completed: {r.Value!.Status}"));
    }
}
