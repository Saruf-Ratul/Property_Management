using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace PropertyManagement.Infrastructure.Services;

public class PmsSyncService : IPmsSyncService
{
    private readonly AppDbContext _db;
    private readonly IPmsConnectorFactory _connectors;
    private readonly ISecretProtector _protector;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly ILogger<PmsSyncService> _log;

    /// <summary>
    /// Per-entity-pass retry policy. Wraps each entity-type collection fetch in 3 exponential-backoff retries
    /// for transient failures (HttpRequestException, TaskCanceled). One bad pass does not abort the rest.
    /// </summary>
    private readonly AsyncRetryPolicy _retry;

    public PmsSyncService(AppDbContext db, IPmsConnectorFactory connectors, ISecretProtector protector,
        ITenantContext tenant, IAuditService audit, ILogger<PmsSyncService> log)
    {
        _db = db; _connectors = connectors; _protector = protector;
        _tenant = tenant; _audit = audit; _log = log;

        _retry = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>(ex => ex.CancellationToken == default)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 250),
                onRetry: (ex, delay, attempt, _) =>
                    _log.LogWarning(ex, "PMS pass retry {Attempt} after {Delay}ms — {Error}",
                        attempt, delay.TotalMilliseconds, ex.Message));
    }

    public Task<Guid> SyncIntegrationAsync(Guid integrationId, CancellationToken ct = default) =>
        RunAsync(integrationId, new PmsSyncRequest(FullSync: true), ct).ContinueWith(t => t.Result.SyncLogId, ct);

    public Task<PmsSyncResult> SyncIntegrationAsync(Guid integrationId, PmsSyncRequest scope, CancellationToken ct = default) =>
        RunAsync(integrationId, scope, ct);

    public async Task SyncAllActiveAsync(CancellationToken ct = default)
    {
        using var bypass = _tenant.Bypass();
        var ids = await _db.PmsIntegrations.Where(x => x.IsActive).Select(x => x.Id).ToListAsync(ct);
        _log.LogInformation("PMS scheduled sync starting for {Count} active integrations", ids.Count);
        foreach (var id in ids)
        {
            try
            {
                await RunAsync(id, new PmsSyncRequest(FullSync: true), ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Scheduled sync failed for integration {Id}", id);
            }
        }
    }

    private async Task<PmsSyncResult> RunAsync(Guid integrationId, PmsSyncRequest scope, CancellationToken ct)
    {
        using var bypass = _tenant.Bypass();
        var integ = await _db.PmsIntegrations.FirstOrDefaultAsync(x => x.Id == integrationId, ct)
            ?? throw new InvalidOperationException($"Integration {integrationId} not found");

        // Whether each entity bucket should run, given the scope.
        var doProperties  = scope.FullSync || scope.SyncProperties;
        var doUnits       = scope.FullSync || scope.SyncUnits;
        var doTenants     = scope.FullSync || scope.SyncTenants;
        var doLeases      = scope.FullSync || scope.SyncLeases;
        var doLedger      = scope.FullSync || scope.SyncLedgerItems;

        var log = new SyncLog { IntegrationId = integ.Id, LawFirmId = integ.LawFirmId, Status = SyncStatus.Started };
        _db.SyncLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "PMS sync starting integration={Id} provider={Provider} scope={Scope}",
            integ.Id, integ.Provider, scope);

        await _audit.LogAsync(AuditAction.PmsSyncStarted, nameof(PmsIntegration), integ.Id.ToString(),
            $"PMS sync starting for {integ.DisplayName}",
            new { integ.Provider, scope.FullSync, scope.SyncProperties, scope.SyncUnits,
                  scope.SyncTenants, scope.SyncLeases, scope.SyncLedgerItems }, ct);

        var failures = new List<string>();
        try
        {
            var connector = _connectors.Get(integ.Provider);
            var ctx = BuildContext(integ);

            if (doProperties)
                log.PropertiesSynced = await SafePassAsync("properties", () => SyncPropertiesAsync(connector, ctx, integ, ct), failures, ct);
            if (doUnits)
                log.UnitsSynced = await SafePassAsync("units", () => SyncUnitsAsync(connector, ctx, integ, ct), failures, ct);
            if (doTenants)
                log.TenantsSynced = await SafePassAsync("tenants", () => SyncTenantsAsync(connector, ctx, integ, ct), failures, ct);
            if (doLeases)
                log.LeasesSynced = await SafePassAsync("leases", () => SyncLeasesAsync(connector, ctx, integ, ct), failures, ct);
            if (doLedger)
                log.LedgerItemsSynced = await SafePassAsync("ledger", () => SyncLedgerAsync(connector, ctx, integ, ct), failures, ct);

            log.FinishedAtUtc = DateTime.UtcNow;
            if (failures.Count == 0)
            {
                log.Status = SyncStatus.Succeeded;
                log.Message = $"Synced {log.PropertiesSynced}P / {log.UnitsSynced}U / {log.TenantsSynced}T / {log.LeasesSynced}L / {log.LedgerItemsSynced} ledger items.";
            }
            else
            {
                log.Status = SyncStatus.PartiallySucceeded;
                log.Message = $"Partial sync. Failures: {string.Join(" | ", failures)}";
            }

            integ.LastSyncAtUtc = log.FinishedAtUtc;
            integ.LastSyncStatus = log.Status;
            integ.LastSyncMessage = log.Message;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "PMS sync done integration={Id} status={Status} P={P} U={U} T={T} L={L} Led={Led}",
                integ.Id, log.Status, log.PropertiesSynced, log.UnitsSynced, log.TenantsSynced, log.LeasesSynced, log.LedgerItemsSynced);

            await _audit.LogAsync(
                log.Status == SyncStatus.Succeeded ? AuditAction.PmsSyncCompleted : AuditAction.PmsSyncFailed,
                nameof(PmsIntegration), integ.Id.ToString(),
                $"PMS sync {log.Status} for {integ.DisplayName}",
                new {
                    log.Status, log.Message, log.PropertiesSynced, log.UnitsSynced,
                    log.TenantsSynced, log.LeasesSynced, log.LedgerItemsSynced, failures
                }, ct);

            return ToResult(log, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "PMS sync FAILED integration={Id}", integ.Id);
            log.Status = SyncStatus.Failed;
            log.FinishedAtUtc = DateTime.UtcNow;
            log.Message = "Sync failed";
            log.ErrorDetail = ex.ToString();
            integ.LastSyncAtUtc = log.FinishedAtUtc;
            integ.LastSyncStatus = log.Status;
            integ.LastSyncMessage = ex.Message;
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(AuditAction.PmsSyncFailed, nameof(PmsIntegration), integ.Id.ToString(),
                $"PMS sync FAILED for {integ.DisplayName}: {ex.Message}",
                new { error = ex.Message, errorType = ex.GetType().Name }, ct);

            return ToResult(log, null);
        }
    }

    private PmsConnectionContext BuildContext(PmsIntegration integ) => new()
    {
        BaseUrl = integ.BaseUrl,
        Username = integ.Username,
        CompanyCode = integ.CompanyCode,
        LocationId = integ.LocationId,
        Password = integ.CredentialsCipher is null ? null : _protector.Unprotect(integ.CredentialsCipher)
    };

    private async Task<int> SafePassAsync(string label, Func<Task<int>> work, List<string> failures, CancellationToken ct)
    {
        try
        {
            return await _retry.ExecuteAsync(work);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "PMS sync pass '{Label}' failed", label);
            failures.Add($"{label}: {ex.Message}");
            return 0;
        }
    }

    private async Task<int> SyncPropertiesAsync(IPmsConnector connector, PmsConnectionContext ctx, PmsIntegration integ, CancellationToken ct)
    {
        var n = 0;
        await foreach (var p in connector.GetPropertiesAsync(ctx, ct))
        {
            var existing = await _db.PmsProperties.FirstOrDefaultAsync(x =>
                x.IntegrationId == integ.Id && x.ExternalId == p.ExternalId, ct);
            if (existing is null)
            {
                _db.PmsProperties.Add(new PmsProperty
                {
                    IntegrationId = integ.Id, LawFirmId = integ.LawFirmId, ExternalId = p.ExternalId,
                    Name = p.Name, AddressLine1 = p.AddressLine1, City = p.City, State = p.State,
                    PostalCode = p.PostalCode, County = p.County, UnitCount = p.UnitCount, IsActive = true
                });
            }
            else
            {
                existing.Name = p.Name; existing.AddressLine1 = p.AddressLine1; existing.City = p.City;
                existing.State = p.State; existing.PostalCode = p.PostalCode; existing.County = p.County;
                existing.UnitCount = p.UnitCount;
            }
            n++;
        }
        await _db.SaveChangesAsync(ct);
        return n;
    }

    private async Task<int> SyncUnitsAsync(IPmsConnector connector, PmsConnectionContext ctx, PmsIntegration integ, CancellationToken ct)
    {
        var n = 0;
        var properties = await _db.PmsProperties.Where(x => x.IntegrationId == integ.Id).ToListAsync(ct);
        foreach (var prop in properties)
        {
            await foreach (var u in connector.GetUnitsAsync(ctx, prop.ExternalId, ct))
            {
                var unit = await _db.PmsUnits.FirstOrDefaultAsync(x => x.PropertyId == prop.Id && x.ExternalId == u.ExternalId, ct);
                if (unit is null)
                {
                    _db.PmsUnits.Add(new PmsUnit
                    {
                        PropertyId = prop.Id, LawFirmId = integ.LawFirmId, ExternalId = u.ExternalId,
                        UnitNumber = u.UnitNumber, Bedrooms = u.Bedrooms, Bathrooms = u.Bathrooms,
                        SquareFeet = u.SquareFeet, MarketRent = u.MarketRent, IsOccupied = u.IsOccupied
                    });
                }
                else
                {
                    unit.UnitNumber = u.UnitNumber; unit.Bedrooms = u.Bedrooms; unit.Bathrooms = u.Bathrooms;
                    unit.SquareFeet = u.SquareFeet; unit.MarketRent = u.MarketRent; unit.IsOccupied = u.IsOccupied;
                }
                n++;
            }
        }
        await _db.SaveChangesAsync(ct);

        // Roll up the persisted unit count back onto each property so the Properties list
        // shows an accurate "Units" column even when the PMS doesn't return TotalUnits.
        var counts = await _db.PmsUnits
            .Where(u => u.Property.IntegrationId == integ.Id)
            .GroupBy(u => u.PropertyId)
            .Select(g => new { PropertyId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byId = counts.ToDictionary(x => x.PropertyId, x => x.Count);
        foreach (var prop in properties)
        {
            prop.UnitCount = byId.TryGetValue(prop.Id, out var c) ? c : 0;
        }
        await _db.SaveChangesAsync(ct);

        return n;
    }

    private async Task<int> SyncTenantsAsync(IPmsConnector connector, PmsConnectionContext ctx, PmsIntegration integ, CancellationToken ct)
    {
        var n = 0;
        await foreach (var t in connector.GetTenantsAsync(ctx, ct))
        {
            var tenant = await _db.PmsTenants.FirstOrDefaultAsync(x => x.IntegrationId == integ.Id && x.ExternalId == t.ExternalId, ct);
            if (tenant is null)
            {
                _db.PmsTenants.Add(new PmsTenant
                {
                    IntegrationId = integ.Id, LawFirmId = integ.LawFirmId, ExternalId = t.ExternalId,
                    FirstName = t.FirstName, LastName = t.LastName, Email = t.Email, Phone = t.Phone,
                    DateOfBirth = t.DateOfBirth, IsActive = t.IsActive
                });
            }
            else
            {
                tenant.FirstName = t.FirstName; tenant.LastName = t.LastName; tenant.Email = t.Email;
                tenant.Phone = t.Phone; tenant.DateOfBirth = t.DateOfBirth; tenant.IsActive = t.IsActive;
            }
            n++;
        }
        await _db.SaveChangesAsync(ct);
        return n;
    }

    private async Task<int> SyncLeasesAsync(IPmsConnector connector, PmsConnectionContext ctx, PmsIntegration integ, CancellationToken ct)
    {
        var n = 0;
        await foreach (var lease in connector.GetLeasesAsync(ctx, ct))
        {
            var unit = await _db.PmsUnits.FirstOrDefaultAsync(x => x.Property.IntegrationId == integ.Id && x.ExternalId == lease.UnitExternalId, ct);
            var tenant = await _db.PmsTenants.FirstOrDefaultAsync(x => x.IntegrationId == integ.Id && x.ExternalId == lease.TenantExternalId, ct);
            if (unit is null || tenant is null)
            {
                _log.LogWarning("Skipping lease {ExternalId} — missing unit ({UnitExt}) or tenant ({TenantExt})",
                    lease.ExternalId, lease.UnitExternalId, lease.TenantExternalId);
                continue;
            }
            var existing = await _db.PmsLeases.FirstOrDefaultAsync(x => x.IntegrationId == integ.Id && x.ExternalId == lease.ExternalId, ct);
            if (existing is null)
            {
                _db.PmsLeases.Add(new PmsLease
                {
                    IntegrationId = integ.Id, LawFirmId = integ.LawFirmId, ExternalId = lease.ExternalId,
                    UnitId = unit.Id, TenantId = tenant.Id, StartDate = lease.StartDate, EndDate = lease.EndDate,
                    MonthlyRent = lease.MonthlyRent, SecurityDeposit = lease.SecurityDeposit,
                    IsMonthToMonth = lease.IsMonthToMonth, IsActive = lease.IsActive, CurrentBalance = lease.CurrentBalance
                });
            }
            else
            {
                existing.UnitId = unit.Id; existing.TenantId = tenant.Id; existing.StartDate = lease.StartDate;
                existing.EndDate = lease.EndDate; existing.MonthlyRent = lease.MonthlyRent;
                existing.SecurityDeposit = lease.SecurityDeposit; existing.IsMonthToMonth = lease.IsMonthToMonth;
                existing.IsActive = lease.IsActive; existing.CurrentBalance = lease.CurrentBalance;
            }
            n++;
        }
        await _db.SaveChangesAsync(ct);
        return n;
    }

    private async Task<int> SyncLedgerAsync(IPmsConnector connector, PmsConnectionContext ctx, PmsIntegration integ, CancellationToken ct)
    {
        var n = 0;
        // Only walk ledgers for leases with an outstanding balance — for a 1000+ lease
        // portfolio, pulling history for every lease takes minutes and adds little value.
        // Active-but-zero-balance leases are picked up the next sync if they go delinquent.
        var leases = await _db.PmsLeases
            .Where(x => x.IntegrationId == integ.Id && x.IsActive && x.CurrentBalance > 0)
            .OrderByDescending(x => x.CurrentBalance)
            .ToListAsync(ct);
        foreach (var lease in leases)
        {
            await foreach (var li in connector.GetLedgerAsync(ctx, lease.ExternalId, ct))
            {
                var existing = await _db.PmsLedgerItems.FirstOrDefaultAsync(x =>
                    x.LeaseId == lease.Id && x.ExternalId == li.ExternalId, ct);
                if (existing is null)
                {
                    _db.PmsLedgerItems.Add(new PmsLedgerItem
                    {
                        LeaseId = lease.Id, LawFirmId = integ.LawFirmId, ExternalId = li.ExternalId,
                        PostedDate = li.PostedDate, DueDate = li.DueDate, Category = li.Category,
                        Description = li.Description, Amount = li.Amount, Balance = li.Balance,
                        IsCharge = li.IsCharge, IsPayment = li.IsPayment
                    });
                }
                else
                {
                    existing.PostedDate = li.PostedDate; existing.DueDate = li.DueDate;
                    existing.Category = li.Category; existing.Description = li.Description;
                    existing.Amount = li.Amount; existing.Balance = li.Balance;
                    existing.IsCharge = li.IsCharge; existing.IsPayment = li.IsPayment;
                }
                n++;
            }
        }
        await _db.SaveChangesAsync(ct);
        return n;
    }

    private static PmsSyncResult ToResult(SyncLog log, string? jobId) => new(
        SyncLogId: log.Id,
        IntegrationId: log.IntegrationId,
        Status: log.Status,
        PropertiesSynced: log.PropertiesSynced,
        UnitsSynced: log.UnitsSynced,
        TenantsSynced: log.TenantsSynced,
        LeasesSynced: log.LeasesSynced,
        LedgerItemsSynced: log.LedgerItemsSynced,
        StartedAtUtc: log.StartedAtUtc,
        FinishedAtUtc: log.FinishedAtUtc,
        Message: log.Message,
        ErrorDetail: log.ErrorDetail,
        BackgroundJobId: jobId);
}
