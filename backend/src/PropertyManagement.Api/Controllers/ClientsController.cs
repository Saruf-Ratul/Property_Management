using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Roles = $"{Roles.FirmAdmin},{Roles.Lawyer},{Roles.Paralegal}")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clients;
    private readonly IValidator<CreateClientRequest> _createVal;

    public ClientsController(IClientService clients, IValidator<CreateClientRequest> createVal)
    {
        _clients = clients; _createVal = createVal;
    }

    [HttpGet]
    public Task<PagedResult<ClientDto>> List([FromQuery] PageRequest page, CancellationToken ct) =>
        _clients.ListAsync(page, ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await _clients.GetAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    [Authorize(Roles = Roles.FirmAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest req, CancellationToken ct)
    {
        await _createVal.ValidateAndThrowAsync(req, ct);
        var r = await _clients.CreateAsync(req, ct);
        return r.IsSuccess ? CreatedAtAction(nameof(Get), new { id = r.Value!.Id }, r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.FirmAdmin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest req, CancellationToken ct)
    {
        var r = await _clients.UpdateAsync(id, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>
    /// Soft-deletes a client. Returns 409 Conflict if the client still has open cases,
    /// PMS integrations, or portal users — those must be cleared first.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.FirmAdmin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var r = await _clients.DeleteAsync(id, ct);
        if (r.IsSuccess) return NoContent();
        return r.Error?.StartsWith("Client not found", StringComparison.OrdinalIgnoreCase) == true
            ? NotFound(new { error = r.Error })
            : Conflict(new { error = r.Error });
    }
}
