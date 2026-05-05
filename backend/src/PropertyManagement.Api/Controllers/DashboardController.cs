using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _svc;
    public DashboardController(IDashboardService svc) => _svc = svc;

    [HttpGet]
    public Task<DashboardStatsDto> Get(CancellationToken ct) => _svc.GetAsync(ct);
}

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = $"{Roles.FirmAdmin},{Roles.Auditor}")]
[Produces("application/json")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditQueryService _svc;
    public AuditLogsController(IAuditQueryService svc) => _svc = svc;

    /// <summary>Paged list of audit log entries scoped to the caller's law firm.</summary>
    [HttpGet]
    public Task<PagedResult<AuditLogDto>> List(
        [FromQuery] PageRequest page,
        [FromQuery] AuditAction? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
        => _svc.ListAsync(page, action, from, to, ct);

    /// <summary>Single-record detail with full payload + old/new values.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Export the filtered audit log as a CSV file (UTF-8 with BOM, capped at 50k rows).</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search,
        [FromQuery] AuditAction? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var (bytes, fileName) = await _svc.ExportCsvAsync(search, action, from, to, ct);
        return File(bytes, "text/csv", fileName);
    }
}
