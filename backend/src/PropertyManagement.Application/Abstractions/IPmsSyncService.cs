using PropertyManagement.Application.DTOs;

namespace PropertyManagement.Application.Abstractions;

/// <summary>
/// Orchestrates fetching data from a PMS connector and upserting it into the local database.
/// Implementations are expected to wrap each entity-type pass in its own try/catch + structured log
/// so that a transient failure on one entity does not abort the whole sync.
/// </summary>
public interface IPmsSyncService
{
    /// <summary>Run a full sync for one integration. Returns the sync log id.</summary>
    Task<Guid> SyncIntegrationAsync(Guid integrationId, CancellationToken ct = default);

    /// <summary>Run a sync of only a subset of entities (used by the scoped /sync endpoint).</summary>
    Task<PmsSyncResult> SyncIntegrationAsync(Guid integrationId, PmsSyncRequest scope, CancellationToken ct = default);

    /// <summary>Iterate all active integrations and full-sync each. Used by the Hangfire recurring job.</summary>
    Task SyncAllActiveAsync(CancellationToken ct = default);
}
