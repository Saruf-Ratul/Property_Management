using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    public DashboardService(AppDbContext db, ICurrentUser user) { _db = db; _user = user; }

    public async Task<DashboardStatsDto> GetAsync(CancellationToken ct = default)
    {
        var caseQ = _db.Cases.AsNoTracking().AsQueryable();
        var leaseQ = _db.PmsLeases.AsNoTracking().AsQueryable();
        var integQ = _db.PmsIntegrations.AsNoTracking().AsQueryable();

        if (_user.IsInRole(Domain.Common.Roles.ClientAdmin) || _user.IsInRole(Domain.Common.Roles.ClientUser))
        {
            if (_user.ClientId is null)
            {
                return new DashboardStatsDto(0, 0, 0, 0, 0,
                    Array.Empty<CaseStageCountDto>(), Array.Empty<CaseClientCountDto>(),
                    Array.Empty<RecentActivityDto>(), Array.Empty<PmsSyncStatusDto>());
            }
            caseQ = caseQ.Where(c => c.ClientId == _user.ClientId.Value);
            leaseQ = leaseQ.Where(l => l.Integration.ClientId == _user.ClientId.Value);
            integQ = integQ.Where(i => i.ClientId == _user.ClientId.Value);
        }

        var total = await caseQ.CountAsync(ct);
        var closed = await caseQ.CountAsync(c => c.CaseStatus.Code == CaseStatusCode.Closed, ct);
        var active = total - closed;
        var delinquent = await leaseQ.CountAsync(l => l.IsActive && l.CurrentBalance > 0, ct);
        var outstanding = await leaseQ.Where(l => l.IsActive).SumAsync(l => (decimal?)l.CurrentBalance, ct) ?? 0m;

        var byStage = await caseQ.GroupBy(c => new { c.CaseStage.Code, c.CaseStage.Name })
            .Select(g => new CaseStageCountDto(g.Key.Code, g.Key.Name, g.Count())).ToListAsync(ct);
        var byClient = await caseQ.GroupBy(c => new { c.ClientId, c.Client.Name })
            .Select(g => new CaseClientCountDto(g.Key.ClientId, g.Key.Name, g.Count())).ToListAsync(ct);

        var recent = await _db.CaseActivities.AsNoTracking()
            .Where(a => caseQ.Select(c => c.Id).Contains(a.CaseId))
            .OrderByDescending(a => a.OccurredAtUtc).Take(15)
            .Select(a => new RecentActivityDto(a.OccurredAtUtc, a.Summary, a.Case.CaseNumber))
            .ToListAsync(ct);

        var sync = await integQ.OrderByDescending(i => i.LastSyncAtUtc).Take(10)
            .Select(i => new PmsSyncStatusDto(i.Id, i.DisplayName, i.Client.Name, i.LastSyncAtUtc, i.LastSyncStatus))
            .ToListAsync(ct);

        return new DashboardStatsDto(total, active, closed, delinquent, outstanding, byStage, byClient, recent, sync);
    }
}
