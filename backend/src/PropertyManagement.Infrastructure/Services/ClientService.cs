using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class ClientService : IClientService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public ClientService(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<PagedResult<ClientDto>> ListAsync(PageRequest req, CancellationToken ct = default)
    {
        var q = _db.Clients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(x => EF.Functions.Like(x.Name, $"%{s}%") || EF.Functions.Like(x.ContactEmail!, $"%{s}%"));
        }
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(x => x.Name)
            .Skip(req.Skip).Take(req.Take)
            .Select(x => new ClientDto(
                x.Id, x.Name, x.ContactName, x.ContactEmail, x.ContactPhone, x.City, x.State, x.IsActive,
                x.PmsIntegrations.Count, x.Cases.Count))
            .ToListAsync(ct);
        return new PagedResult<ClientDto> { Items = items, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public async Task<ClientDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Clients.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ClientDto(x.Id, x.Name, x.ContactName, x.ContactEmail, x.ContactPhone, x.City, x.State, x.IsActive,
                x.PmsIntegrations.Count, x.Cases.Count))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result<ClientDto>> CreateAsync(CreateClientRequest req, CancellationToken ct = default)
    {
        if (await _db.Clients.AnyAsync(x => x.Name == req.Name, ct))
            return Result<ClientDto>.Failure($"A client named '{req.Name}' already exists.");

        var c = new Client
        {
            Name = req.Name,
            ContactName = req.ContactName,
            ContactEmail = req.ContactEmail,
            ContactPhone = req.ContactPhone,
            AddressLine1 = req.AddressLine1,
            AddressLine2 = req.AddressLine2,
            City = req.City,
            State = req.State,
            PostalCode = req.PostalCode
        };
        _db.Clients.Add(c);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(Domain.Enums.AuditAction.ClientCreated,
            nameof(Client), c.Id.ToString(),
            $"Created client {c.Name}",
            new { c.Id, c.Name, c.ContactEmail, c.City, c.State }, ct);

        var dto = (await GetAsync(c.Id, ct))!;
        return Result<ClientDto>.Success(dto);
    }

    public async Task<Result<ClientDto>> UpdateAsync(Guid id, UpdateClientRequest req, CancellationToken ct = default)
    {
        var c = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<ClientDto>.Failure("Client not found");

        if (!string.Equals(c.Name, req.Name, StringComparison.OrdinalIgnoreCase) &&
            await _db.Clients.AnyAsync(x => x.Id != id && x.Name == req.Name, ct))
            return Result<ClientDto>.Failure($"A client named '{req.Name}' already exists.");

        var before = new { c.Name, c.ContactName, c.ContactEmail, c.ContactPhone, c.City, c.State, c.IsActive };
        c.Name = req.Name;
        c.ContactName = req.ContactName;
        c.ContactEmail = req.ContactEmail;
        c.ContactPhone = req.ContactPhone;
        c.AddressLine1 = req.AddressLine1;
        c.AddressLine2 = req.AddressLine2;
        c.City = req.City;
        c.State = req.State;
        c.PostalCode = req.PostalCode;
        c.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);

        var after = new { c.Name, c.ContactName, c.ContactEmail, c.ContactPhone, c.City, c.State, c.IsActive };
        await _audit.LogChangeAsync(Domain.Enums.AuditAction.ClientUpdated,
            nameof(Client), c.Id.ToString(),
            $"Updated client {c.Name}",
            before, after, ct);

        var dto = (await GetAsync(c.Id, ct))!;
        return Result<ClientDto>.Success(dto);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<bool>.Failure("Client not found");

        // Refuse to delete a client that still has dependent records — prevents orphaning
        // cases / sync history / portal users. Operator must close cases and remove
        // integrations first, or set the client inactive instead of deleting.
        var caseCount = await _db.Cases.CountAsync(x => x.ClientId == id, ct);
        var integrationCount = await _db.PmsIntegrations.CountAsync(x => x.ClientId == id, ct);
        var userCount = await _db.UserProfiles.IgnoreQueryFilters().CountAsync(x => x.ClientId == id, ct);

        if (caseCount > 0 || integrationCount > 0 || userCount > 0)
        {
            var parts = new List<string>();
            if (caseCount > 0) parts.Add($"{caseCount} case(s)");
            if (integrationCount > 0) parts.Add($"{integrationCount} PMS integration(s)");
            if (userCount > 0) parts.Add($"{userCount} portal user(s)");
            return Result<bool>.Failure(
                $"Cannot delete '{c.Name}' — it still has {string.Join(", ", parts)}. " +
                "Remove or reassign those first, or set the client inactive instead.");
        }

        var snapshot = new { c.Id, c.Name, c.ContactEmail, c.City, c.State };
        _db.Clients.Remove(c);                 // global soft-delete interceptor flips IsDeleted=true
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(Domain.Enums.AuditAction.ClientDeleted,
            nameof(Client), c.Id.ToString(),
            $"Deleted client {c.Name}",
            snapshot, ct);

        return Result<bool>.Success(true);
    }
}
