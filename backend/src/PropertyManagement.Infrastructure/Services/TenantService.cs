using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly AppDbContext _db;
    public TenantService(AppDbContext db) => _db = db;

    public async Task<PagedResult<TenantDto>> ListAsync(PageRequest req, TenantFilter filter, CancellationToken ct = default)
    {
        var q = _db.PmsTenants.AsNoTracking().AsQueryable();
        if (filter.ClientId.HasValue) q = q.Where(t => t.Integration.ClientId == filter.ClientId.Value);
        if (filter.IsActive.HasValue) q = q.Where(t => t.IsActive == filter.IsActive.Value);
        if (filter.PropertyId.HasValue)
            q = q.Where(t => t.Leases.Any(l => l.Unit.PropertyId == filter.PropertyId.Value));

        if (filter.DelinquentOnly)
            q = q.Where(t => t.Leases.Any(l => l.IsActive && l.CurrentBalance > 0));
        if (filter.MinBalance is { } minBal && minBal > 0)
            q = q.Where(t => t.Leases.Any(l => l.IsActive && l.CurrentBalance >= minBal));

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(t =>
                EF.Functions.Like(t.FirstName, $"%{s}%") ||
                EF.Functions.Like(t.LastName, $"%{s}%") ||
                EF.Functions.Like(t.Email!, $"%{s}%") ||
                EF.Functions.Like(t.Phone!, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);

        // Flatten the projection so the InMemory provider doesn't have to traverse
        // ActiveLease.Unit.Property navigations (those don't get loaded via Select alone).
        var rows = await q.OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .Skip(req.Skip).Take(req.Take)
            .Select(t => new
            {
                t.Id,
                t.ExternalId,
                t.FirstName,
                t.LastName,
                t.Email,
                t.Phone,
                t.IsActive,
                ActiveLease = t.Leases
                    .Where(l => l.IsActive)
                    .OrderByDescending(l => l.StartDate)
                    .Select(l => new
                    {
                        l.CurrentBalance,
                        UnitNumber = l.Unit.UnitNumber,
                        PropertyName = l.Unit.Property.Name,
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var dtos = rows.Select(x => new TenantDto(
            x.Id, x.ExternalId, x.FirstName, x.LastName,
            $"{x.FirstName} {x.LastName}".Trim(),
            x.Email, x.Phone, x.IsActive,
            x.ActiveLease?.CurrentBalance ?? 0,
            x.ActiveLease?.UnitNumber,
            x.ActiveLease?.PropertyName)).ToList();

        return new PagedResult<TenantDto> { Items = dtos, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public async Task<TenantDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.PmsTenants.AsNoTracking()
            .Include(x => x.Integration).ThenInclude(i => i.Client)
            .Include(x => x.Leases.Where(l => l.IsActive))
                .ThenInclude(l => l.Unit).ThenInclude(u => u.Property)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return null;

        var lease = t.Leases.OrderByDescending(l => l.StartDate).FirstOrDefault();
        return new TenantDetailDto(
            t.Id, t.ExternalId, t.FirstName, t.LastName,
            $"{t.FirstName} {t.LastName}".Trim(),
            t.Email, t.Phone, t.DateOfBirth, t.IsActive,
            t.IntegrationId, t.Integration.DisplayName,
            t.Integration.ClientId, t.Integration.Client.Name,
            lease?.CurrentBalance ?? 0,
            lease?.Id, lease?.Unit.PropertyId, lease?.Unit.Property.Name,
            lease?.Unit.UnitNumber, lease?.MonthlyRent,
            lease?.StartDate, lease?.EndDate);
    }

    public async Task<IReadOnlyList<LeaseDto>> GetLeasesAsync(Guid tenantId, CancellationToken ct = default) =>
        await _db.PmsLeases.AsNoTracking().Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.StartDate)
            .Select(l => new LeaseDto(
                l.Id, l.ExternalId, l.TenantId, l.Tenant.FirstName + " " + l.Tenant.LastName,
                l.UnitId, l.Unit.UnitNumber, l.Unit.Property.Name,
                l.StartDate, l.EndDate, l.MonthlyRent, l.CurrentBalance, l.IsActive))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LedgerItemDto>> GetLedgerAsync(Guid leaseId, CancellationToken ct = default) =>
        await _db.PmsLedgerItems.AsNoTracking().Where(x => x.LeaseId == leaseId)
            .OrderByDescending(x => x.PostedDate)
            .Select(x => new LedgerItemDto(x.Id, x.PostedDate, x.DueDate, x.Category, x.Description, x.Amount, x.Balance, x.IsCharge, x.IsPayment))
            .ToListAsync(ct);

    public async Task<PagedResult<DelinquentTenantDto>> GetDelinquentAsync(PageRequest req, Guid? clientId, decimal minBalance, CancellationToken ct = default)
    {
        var q = _db.PmsLeases.AsNoTracking().Where(l => l.IsActive && l.CurrentBalance >= minBalance);
        if (clientId.HasValue) q = q.Where(l => l.Integration.ClientId == clientId.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(l => l.CurrentBalance)
            .Skip(req.Skip).Take(req.Take)
            .Select(l => new DelinquentTenantDto(
                l.TenantId,
                l.Tenant.FirstName + " " + l.Tenant.LastName,
                l.Id,
                l.Unit.Property.Name,
                l.Unit.UnitNumber,
                l.CurrentBalance,
                l.MonthlyRent,
                l.MonthlyRent <= 0 ? 0 : (int)Math.Floor(l.CurrentBalance / l.MonthlyRent * 30),
                l.Integration.Client.Name,
                l.Integration.ClientId,
                l.Unit.PropertyId))
            .ToListAsync(ct);

        return new PagedResult<DelinquentTenantDto> { Items = items, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public async Task<DelinquencyStatsDto> GetDelinquencyStatsAsync(Guid? clientId, CancellationToken ct = default)
    {
        var leaseQ = _db.PmsLeases.AsNoTracking().Where(l => l.IsActive && l.CurrentBalance > 0);
        if (clientId.HasValue) leaseQ = leaseQ.Where(l => l.Integration.ClientId == clientId.Value);

        // Project the slice we need then materialize. Aggregating client-side keeps
        // the query InMemory-provider compatible (production SQL Server would tolerate the
        // group-by translation, but pulling the delinquent set is cheap enough by definition).
        var rows = await leaseQ
            .Select(l => new
            {
                LeaseId = l.Id,
                l.TenantId,
                TenantName = l.Tenant.FirstName + " " + l.Tenant.LastName,
                PropertyId = l.Unit.PropertyId,
                PropertyName = l.Unit.Property.Name,
                UnitNumber = l.Unit.UnitNumber,
                ClientId = l.Integration.ClientId,
                ClientName = l.Integration.Client.Name,
                l.CurrentBalance,
                l.MonthlyRent,
            })
            .ToListAsync(ct);

        var totalDelinquent = rows.Count;
        var totalBalance = rows.Sum(r => r.CurrentBalance);
        var avgBalance = totalDelinquent == 0 ? 0m : totalBalance / totalDelinquent;

        // Pull oldest unpaid charge across the whole delinquent set.
        var oldestPosted = await _db.PmsLedgerItems.AsNoTracking()
            .Where(li => li.IsCharge && rows.Select(r => r.LeaseId).Contains(li.LeaseId))
            .OrderBy(li => li.PostedDate)
            .Select(li => (DateTime?)li.PostedDate)
            .FirstOrDefaultAsync(ct);

        var oldestUnpaidDays = oldestPosted is null
            ? 0
            : Math.Max(0, (int)(DateTime.UtcNow - oldestPosted.Value).TotalDays);

        var top = rows
            .GroupBy(r => new { r.PropertyId, r.PropertyName, r.ClientId, r.ClientName })
            .Select(g => new TopDelinquentPropertyDto(
                g.Key.PropertyId,
                g.Key.PropertyName,
                g.Key.ClientName,
                g.Key.ClientId,
                g.Count(),
                g.Sum(x => x.CurrentBalance),
                g.Max(x => x.CurrentBalance)))
            .OrderByDescending(x => x.OutstandingBalance)
            .Take(8)
            .ToList();

        var oldestTenants = rows
            .OrderByDescending(r => r.CurrentBalance)
            .Take(8)
            .Select(r => new DelinquentTenantDto(
                r.TenantId,
                r.TenantName,
                r.LeaseId,
                r.PropertyName,
                r.UnitNumber,
                r.CurrentBalance,
                r.MonthlyRent,
                r.MonthlyRent <= 0 ? 0 : (int)Math.Floor(r.CurrentBalance / r.MonthlyRent * 30),
                r.ClientName,
                r.ClientId,
                r.PropertyId))
            .ToList();

        return new DelinquencyStatsDto(
            totalDelinquent, totalBalance, avgBalance, oldestUnpaidDays, top, oldestTenants);
    }
}
