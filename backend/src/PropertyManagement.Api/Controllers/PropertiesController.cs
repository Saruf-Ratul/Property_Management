using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/properties")]
[Authorize]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyService _props;
    public PropertiesController(IPropertyService props) => _props = props;

    /// <summary>
    /// List properties synced from PMS. Supports filtering by client, PMS provider, county, state, active flag,
    /// plus full-text search over name/address/city/zip.
    /// </summary>
    [HttpGet]
    public Task<PagedResult<PropertyDto>> List(
        [FromQuery] PageRequest page,
        [FromQuery] Guid? clientId,
        [FromQuery] PmsProvider? provider,
        [FromQuery] string? county,
        [FromQuery] string? state,
        [FromQuery] bool? isActive,
        CancellationToken ct)
        => _props.ListAsync(page, new PropertyFilter
        {
            ClientId = clientId, Provider = provider, County = county, State = state, IsActive = isActive
        }, ct);

    /// <summary>Returns the distinct list of counties currently in use by synced properties (filter dropdown).</summary>
    [HttpGet("counties")]
    public Task<IReadOnlyList<string>> Counties(CancellationToken ct) => _props.GetCountiesAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var p = await _props.GetAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var d = await _props.GetDetailAsync(id, ct);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpGet("{id:guid}/units")]
    public Task<IReadOnlyList<UnitDto>> Units(Guid id, CancellationToken ct) => _props.GetUnitsAsync(id, ct);

    [HttpGet("{id:guid}/tenants")]
    public Task<IReadOnlyList<TenantDto>> Tenants(Guid id, CancellationToken ct) => _props.GetTenantsAsync(id, ct);

    [HttpGet("{id:guid}/leases")]
    public Task<IReadOnlyList<LeaseDto>> Leases(Guid id, CancellationToken ct) => _props.GetLeasesAsync(id, ct);

    [HttpGet("{id:guid}/ledger-summary")]
    public Task<PropertyLedgerSummaryDto> LedgerSummary(Guid id, CancellationToken ct) => _props.GetLedgerSummaryAsync(id, ct);
}

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsApiController : ControllerBase
{
    private readonly ITenantService _tenants;
    public TenantsApiController(ITenantService tenants) => _tenants = tenants;

    /// <summary>
    /// List tenants. Supports filtering by client, property, delinquent-only, minimum-balance, and active flag.
    /// </summary>
    [HttpGet]
    public Task<PagedResult<TenantDto>> List(
        [FromQuery] PageRequest page,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? propertyId,
        [FromQuery] bool delinquentOnly = false,
        [FromQuery] decimal? minBalance = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
        => _tenants.ListAsync(page, new TenantFilter
        {
            ClientId = clientId, PropertyId = propertyId, DelinquentOnly = delinquentOnly,
            MinBalance = minBalance, IsActive = isActive
        }, ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await _tenants.GetDetailAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpGet("{id:guid}/leases")]
    public Task<IReadOnlyList<LeaseDto>> Leases(Guid id, CancellationToken ct) => _tenants.GetLeasesAsync(id, ct);

    [HttpGet("delinquent")]
    public Task<PagedResult<DelinquentTenantDto>> Delinquent(
        [FromQuery] PageRequest page,
        [FromQuery] Guid? clientId,
        [FromQuery] decimal minBalance = 1m,
        CancellationToken ct = default)
        => _tenants.GetDelinquentAsync(page, clientId, minBalance, ct);

    [HttpGet("delinquency-stats")]
    public Task<DelinquencyStatsDto> DelinquencyStats([FromQuery] Guid? clientId, CancellationToken ct = default)
        => _tenants.GetDelinquencyStatsAsync(clientId, ct);
}

[ApiController]
[Route("api/leases")]
[Authorize]
public class LeasesApiController : ControllerBase
{
    private readonly ITenantService _tenants;
    public LeasesApiController(ITenantService tenants) => _tenants = tenants;

    [HttpGet("{id:guid}/ledger")]
    public Task<IReadOnlyList<LedgerItemDto>> Ledger(Guid id, CancellationToken ct) => _tenants.GetLedgerAsync(id, ct);
}
