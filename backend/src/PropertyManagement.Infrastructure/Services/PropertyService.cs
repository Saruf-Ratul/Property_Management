using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class PropertyService : IPropertyService
{
    private readonly AppDbContext _db;
    public PropertyService(AppDbContext db) => _db = db;

    public async Task<PagedResult<PropertyDto>> ListAsync(PageRequest req, PropertyFilter filter, CancellationToken ct = default)
    {
        var q = _db.PmsProperties.AsNoTracking().AsQueryable();
        if (filter.ClientId.HasValue) q = q.Where(p => p.Integration.ClientId == filter.ClientId.Value);
        if (filter.Provider.HasValue) q = q.Where(p => p.Integration.Provider == filter.Provider.Value);
        if (!string.IsNullOrWhiteSpace(filter.County)) q = q.Where(p => p.County == filter.County);
        if (!string.IsNullOrWhiteSpace(filter.State)) q = q.Where(p => p.State == filter.State);
        if (filter.IsActive.HasValue) q = q.Where(p => p.IsActive == filter.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(p =>
                EF.Functions.Like(p.Name, $"%{s}%") ||
                EF.Functions.Like(p.AddressLine1!, $"%{s}%") ||
                EF.Functions.Like(p.City!, $"%{s}%") ||
                EF.Functions.Like(p.PostalCode!, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(p => p.Name).Skip(req.Skip).Take(req.Take)
            .Select(p => new PropertyDto(p.Id, p.IntegrationId, p.ExternalId, p.Name, p.AddressLine1, p.City, p.State, p.PostalCode, p.County, p.UnitCount, p.IsActive))
            .ToListAsync(ct);
        return new PagedResult<PropertyDto> { Items = items, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public Task<PropertyDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.PmsProperties.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new PropertyDto(p.Id, p.IntegrationId, p.ExternalId, p.Name, p.AddressLine1, p.City, p.State, p.PostalCode, p.County, p.UnitCount, p.IsActive))
            .FirstOrDefaultAsync(ct);

    public async Task<PropertyDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.PmsProperties.AsNoTracking()
            .Include(x => x.Integration).ThenInclude(i => i.Client)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;

        var unitsQ = _db.PmsUnits.AsNoTracking().Where(u => u.PropertyId == id);
        var leasesQ = _db.PmsLeases.AsNoTracking().Where(l => l.Unit.PropertyId == id);

        var occupied = await unitsQ.CountAsync(u => u.IsOccupied, ct);
        var vacant = await unitsQ.CountAsync(u => !u.IsOccupied, ct);

        var rents = await unitsQ.Where(u => u.MarketRent != null).Select(u => u.MarketRent!.Value).ToListAsync(ct);
        var avgRent = rents.Count > 0 ? rents.Average() : 0m;

        var activeLeases = await leasesQ.CountAsync(l => l.IsActive, ct);
        var delinquentLeases = await leasesQ.CountAsync(l => l.IsActive && l.CurrentBalance > 0, ct);
        var outstanding = await leasesQ.Where(l => l.IsActive)
            .SumAsync(l => (decimal?)l.CurrentBalance, ct) ?? 0m;

        return new PropertyDetailDto(
            p.Id, p.IntegrationId, p.Integration.DisplayName, p.Integration.Provider,
            p.Integration.ClientId, p.Integration.Client.Name,
            p.ExternalId, p.Name,
            p.AddressLine1, p.AddressLine2, p.City, p.State, p.PostalCode, p.County,
            p.UnitCount, occupied, vacant, activeLeases, delinquentLeases, outstanding,
            avgRent, p.IsActive, p.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<UnitDto>> GetUnitsAsync(Guid propertyId, CancellationToken ct = default) =>
        await _db.PmsUnits.AsNoTracking().Where(u => u.PropertyId == propertyId)
            .OrderBy(u => u.UnitNumber)
            .Select(u => new UnitDto(u.Id, u.PropertyId, u.Property.Name, u.ExternalId, u.UnitNumber, u.Bedrooms, u.Bathrooms, u.MarketRent, u.IsOccupied))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(Guid propertyId, CancellationToken ct = default) =>
        await _db.PmsLeases.AsNoTracking()
            .Where(l => l.Unit.PropertyId == propertyId && l.IsActive)
            .OrderBy(l => l.Tenant.LastName).ThenBy(l => l.Tenant.FirstName)
            .Select(l => new TenantDto(
                l.TenantId, l.Tenant.ExternalId, l.Tenant.FirstName, l.Tenant.LastName,
                l.Tenant.FirstName + " " + l.Tenant.LastName,
                l.Tenant.Email, l.Tenant.Phone, l.Tenant.IsActive,
                l.CurrentBalance, l.Unit.UnitNumber, l.Unit.Property.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LeaseDto>> GetLeasesAsync(Guid propertyId, CancellationToken ct = default) =>
        await _db.PmsLeases.AsNoTracking()
            .Where(l => l.Unit.PropertyId == propertyId)
            .OrderByDescending(l => l.IsActive).ThenByDescending(l => l.StartDate)
            .Select(l => new LeaseDto(
                l.Id, l.ExternalId, l.TenantId, l.Tenant.FirstName + " " + l.Tenant.LastName,
                l.UnitId, l.Unit.UnitNumber, l.Unit.Property.Name,
                l.StartDate, l.EndDate, l.MonthlyRent, l.CurrentBalance, l.IsActive))
            .ToListAsync(ct);

    public async Task<PropertyLedgerSummaryDto> GetLedgerSummaryAsync(Guid propertyId, CancellationToken ct = default)
    {
        var leaseIds = await _db.PmsLeases.AsNoTracking()
            .Where(l => l.Unit.PropertyId == propertyId)
            .Select(l => l.Id).ToListAsync(ct);

        var ledger = _db.PmsLedgerItems.AsNoTracking()
            .Where(li => leaseIds.Contains(li.LeaseId));

        var totalCharges = await ledger.Where(li => li.IsCharge).SumAsync(li => (decimal?)li.Amount, ct) ?? 0m;
        var totalPayments = await ledger.Where(li => li.IsPayment).SumAsync(li => (decimal?)Math.Abs(li.Amount), ct) ?? 0m;

        var activeLeases = await _db.PmsLeases.AsNoTracking().CountAsync(l => l.Unit.PropertyId == propertyId && l.IsActive, ct);
        var delinquentLeases = await _db.PmsLeases.AsNoTracking().CountAsync(l => l.Unit.PropertyId == propertyId && l.IsActive && l.CurrentBalance > 0, ct);
        var outstanding = await _db.PmsLeases.AsNoTracking()
            .Where(l => l.Unit.PropertyId == propertyId && l.IsActive)
            .SumAsync(l => (decimal?)l.CurrentBalance, ct) ?? 0m;

        var delinquentLeaseIds = await _db.PmsLeases.AsNoTracking()
            .Where(l => l.Unit.PropertyId == propertyId && l.IsActive && l.CurrentBalance > 0)
            .Select(l => l.Id).ToListAsync(ct);

        DateTime? oldestUnpaid = await _db.PmsLedgerItems.AsNoTracking()
            .Where(li => li.IsCharge && delinquentLeaseIds.Contains(li.LeaseId))
            .OrderBy(li => li.PostedDate)
            .Select(li => (DateTime?)li.PostedDate)
            .FirstOrDefaultAsync(ct);

        var recent = await ledger
            .OrderByDescending(li => li.PostedDate)
            .Take(20)
            .Select(li => new LedgerItemDto(li.Id, li.PostedDate, li.DueDate, li.Category, li.Description, li.Amount, li.Balance, li.IsCharge, li.IsPayment))
            .ToListAsync(ct);

        return new PropertyLedgerSummaryDto(
            propertyId, totalCharges, totalPayments, outstanding,
            activeLeases, delinquentLeases, oldestUnpaid, recent);
    }

    public async Task<IReadOnlyList<string>> GetCountiesAsync(CancellationToken ct = default) =>
        await _db.PmsProperties.AsNoTracking()
            .Where(p => p.County != null && p.County != "")
            .Select(p => p.County!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
}
