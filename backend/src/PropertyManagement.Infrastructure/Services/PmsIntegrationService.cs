using System.Diagnostics;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PropertyManagement.Infrastructure.Services;

public class PmsIntegrationService : IPmsIntegrationService
{
    private readonly AppDbContext _db;
    private readonly IPmsConnectorFactory _connectors;
    private readonly ISecretProtector _protector;
    private readonly IBackgroundJobClient _jobs;
    private readonly IPmsSyncService _sync;
    private readonly IAuditService _audit;
    private readonly ILogger<PmsIntegrationService> _log;

    public PmsIntegrationService(
        AppDbContext db, IPmsConnectorFactory connectors, ISecretProtector protector,
        IBackgroundJobClient jobs, IPmsSyncService sync, IAuditService audit,
        ILogger<PmsIntegrationService> log)
    {
        _db = db; _connectors = connectors; _protector = protector;
        _jobs = jobs; _sync = sync; _audit = audit; _log = log;
    }

    public async Task<PagedResult<PmsIntegrationDto>> ListAsync(PageRequest req, Guid? clientId, CancellationToken ct = default)
    {
        var q = _db.PmsIntegrations.AsNoTracking().AsQueryable();
        if (clientId.HasValue) q = q.Where(x => x.ClientId == clientId.Value);
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = q.Where(x => EF.Functions.Like(x.DisplayName, $"%{s}%"));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.LastSyncAtUtc).Skip(req.Skip).Take(req.Take)
            .Select(x => new PmsIntegrationDto(
                x.Id, x.ClientId, x.Client.Name, x.Provider, x.DisplayName, x.BaseUrl, x.CompanyCode, x.LocationId,
                x.IsActive, x.LastSyncAtUtc, x.LastSyncStatus, x.LastSyncMessage, x.SyncIntervalMinutes))
            .ToListAsync(ct);
        return new PagedResult<PmsIntegrationDto> { Items = items, Page = req.Page, PageSize = req.Take, TotalCount = total };
    }

    public Task<PmsIntegrationDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.PmsIntegrations.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new PmsIntegrationDto(
                x.Id, x.ClientId, x.Client.Name, x.Provider, x.DisplayName, x.BaseUrl, x.CompanyCode, x.LocationId,
                x.IsActive, x.LastSyncAtUtc, x.LastSyncStatus, x.LastSyncMessage, x.SyncIntervalMinutes))
            .FirstOrDefaultAsync(ct)!;

    public async Task<Result<PmsIntegrationDto>> CreateAsync(CreatePmsIntegrationRequest req, CancellationToken ct = default)
    {
        var client = await _db.Clients.FindAsync(new object[] { req.ClientId }, ct);
        if (client is null) return Result<PmsIntegrationDto>.Failure("Client not found");

        var integration = new PmsIntegration
        {
            ClientId = req.ClientId,
            Provider = req.Provider,
            DisplayName = req.DisplayName,
            BaseUrl = req.BaseUrl,
            Username = req.Username,
            CompanyCode = req.CompanyCode,
            LocationId = req.LocationId,
            CredentialsCipher = string.IsNullOrEmpty(req.Password) ? null : _protector.Protect(req.Password!),
            SyncIntervalMinutes = req.SyncIntervalMinutes,
            IsActive = true
        };
        _db.PmsIntegrations.Add(integration);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.PmsIntegrationCreated, nameof(PmsIntegration), integration.Id.ToString(),
            $"Created PMS integration {integration.DisplayName}",
            new
            {
                req.Provider, req.DisplayName, req.ClientId, req.BaseUrl, req.SyncIntervalMinutes,
                hasPassword = !string.IsNullOrEmpty(req.Password)
            },
            ct);
        var dto = (await GetAsync(integration.Id, ct))!;
        return Result<PmsIntegrationDto>.Success(dto);
    }

    public async Task<Result<PmsIntegrationDto>> UpdateAsync(Guid id, UpdatePmsIntegrationRequest req, CancellationToken ct = default)
    {
        var integ = await _db.PmsIntegrations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (integ is null) return Result<PmsIntegrationDto>.Failure("Integration not found");

        // Snapshot before/after for the audit trail (passwords are never included).
        var oldValue = new {
            integ.DisplayName, integ.BaseUrl, integ.Username, integ.CompanyCode,
            integ.LocationId, integ.SyncIntervalMinutes, integ.IsActive
        };

        integ.DisplayName = req.DisplayName;
        integ.BaseUrl = req.BaseUrl;
        integ.Username = req.Username;
        integ.CompanyCode = req.CompanyCode;
        integ.LocationId = req.LocationId;
        integ.SyncIntervalMinutes = req.SyncIntervalMinutes;
        integ.IsActive = req.IsActive;

        // Empty password = keep existing cipher.
        var passwordChanged = !string.IsNullOrEmpty(req.Password);
        if (passwordChanged)
            integ.CredentialsCipher = _protector.Protect(req.Password!);

        await _db.SaveChangesAsync(ct);

        var newValue = new {
            integ.DisplayName, integ.BaseUrl, integ.Username, integ.CompanyCode,
            integ.LocationId, integ.SyncIntervalMinutes, integ.IsActive,
            passwordChanged
        };
        await _audit.LogChangeAsync(AuditAction.PmsIntegrationUpdated, nameof(PmsIntegration), integ.Id.ToString(),
            $"Updated PMS integration {integ.DisplayName}",
            oldValue: oldValue, newValue: newValue, ct);

        var dto = (await GetAsync(integ.Id, ct))!;
        return Result<PmsIntegrationDto>.Success(dto);
    }

    public async Task<Result<PmsConnectionTestResult>> TestAsync(Guid id, CancellationToken ct = default)
    {
        var integ = await _db.PmsIntegrations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (integ is null) return Result<PmsConnectionTestResult>.Failure("Integration not found");

        var connector = _connectors.Get(integ.Provider);
        var ctx = new PmsConnectionContext
        {
            BaseUrl = integ.BaseUrl,
            Username = integ.Username,
            CompanyCode = integ.CompanyCode,
            LocationId = integ.LocationId,
            Password = integ.CredentialsCipher is null ? null : _protector.Unprotect(integ.CredentialsCipher)
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var outcome = await connector.TestConnectionAsync(ctx, ct);
            sw.Stop();
            await _audit.LogAsync(AuditAction.PmsSync, nameof(PmsIntegration), integ.Id.ToString(),
                $"Tested connection — {(outcome.IsConnected ? "OK" : "FAIL")}", new { outcome.Message, LatencyMs = sw.ElapsedMilliseconds }, ct);
            return Result<PmsConnectionTestResult>.Success(new PmsConnectionTestResult(
                outcome.IsConnected, outcome.Message ?? "", outcome.ServerVersion,
                (long)outcome.Latency.TotalMilliseconds, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Connection test threw for integration {Id}", id);
            sw.Stop();
            return Result<PmsConnectionTestResult>.Success(new PmsConnectionTestResult(
                false, ex.Message, null, sw.ElapsedMilliseconds, DateTime.UtcNow));
        }
    }

    public async Task<Result<PmsConnectionTestResult>> TestAdHocAsync(PmsConnectionTestRequest req, CancellationToken ct = default)
    {
        if (!req.Provider.HasValue)
            return Result<PmsConnectionTestResult>.Failure("Provider is required for an ad-hoc connection test.");

        var connector = _connectors.Get(req.Provider.Value);
        var ctx = new PmsConnectionContext
        {
            BaseUrl = req.BaseUrl,
            Username = req.Username,
            Password = req.Password,
            CompanyCode = req.CompanyCode,
            LocationId = req.LocationId,
            ApiKey = req.ApiKey
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var outcome = await connector.TestConnectionAsync(ctx, ct);
            sw.Stop();
            return Result<PmsConnectionTestResult>.Success(new PmsConnectionTestResult(
                outcome.IsConnected, outcome.Message ?? "", outcome.ServerVersion,
                (long)outcome.Latency.TotalMilliseconds, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Result<PmsConnectionTestResult>.Success(new PmsConnectionTestResult(
                false, ex.Message, null, sw.ElapsedMilliseconds, DateTime.UtcNow));
        }
    }

    public async Task<Result<PmsSyncResult>> TriggerSyncAsync(Guid id, PmsSyncRequest req, CancellationToken ct = default)
    {
        var integ = await _db.PmsIntegrations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (integ is null) return Result<PmsSyncResult>.Failure("Integration not found");

        if (req.RunInBackground)
        {
            // Hand off to Hangfire — returns immediately. The PmsSyncService will write the
            // PmsSyncStarted / PmsSyncCompleted / PmsSyncFailed audit events when it actually runs.
            var jobId = _jobs.Enqueue<IPmsSyncService>(s => s.SyncIntegrationAsync(id, req, CancellationToken.None));
            await _audit.LogAsync(AuditAction.PmsSync, nameof(PmsIntegration), id.ToString(),
                $"Queued PMS sync for {integ.DisplayName}",
                new { jobId, req.FullSync, req.RunInBackground }, ct);
            var queued = new PmsSyncResult(
                SyncLogId: Guid.Empty,
                IntegrationId: id,
                Status: SyncStatus.Started,
                PropertiesSynced: 0, UnitsSynced: 0, TenantsSynced: 0, LeasesSynced: 0, LedgerItemsSynced: 0,
                StartedAtUtc: DateTime.UtcNow,
                FinishedAtUtc: null,
                Message: "Sync queued for background execution",
                ErrorDetail: null,
                BackgroundJobId: jobId);
            return Result<PmsSyncResult>.Success(queued);
        }

        // Foreground sync: PmsSyncService.RunAsync writes the started/completed/failed audit events.
        var result = await _sync.SyncIntegrationAsync(id, req, ct);
        return Result<PmsSyncResult>.Success(result);
    }

    public async Task<IReadOnlyList<SyncLogDto>> GetSyncLogsAsync(Guid integrationId, int take, CancellationToken ct = default) =>
        await _db.SyncLogs.AsNoTracking()
            .Where(x => x.IntegrationId == integrationId)
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new SyncLogDto(x.Id, x.IntegrationId, x.StartedAtUtc, x.FinishedAtUtc, x.Status,
                x.PropertiesSynced, x.UnitsSynced, x.TenantsSynced, x.LeasesSynced, x.LedgerItemsSynced,
                x.Message, x.ErrorDetail))
            .ToListAsync(ct);

}
