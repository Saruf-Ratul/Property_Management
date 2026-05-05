using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// Immutable audit row. Once written, never updated or deleted.
/// </summary>
public class AuditLog : Entity
{
    public Guid? LawFirmId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? Summary { get; set; }

    /// <summary>Free-form JSON payload — used for events that don't have a clean before/after pair.</summary>
    public string? PayloadJson { get; set; }
    /// <summary>JSON snapshot of the relevant fields BEFORE the change (for update/delete events).</summary>
    public string? OldValueJson { get; set; }
    /// <summary>JSON snapshot of the relevant fields AFTER the change (for create/update events).</summary>
    public string? NewValueJson { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public class SyncLog : TenantEntity
{
    public Guid IntegrationId { get; set; }
    public PmsIntegration Integration { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Started;
    public int PropertiesSynced { get; set; }
    public int UnitsSynced { get; set; }
    public int TenantsSynced { get; set; }
    public int LeasesSynced { get; set; }
    public int LedgerItemsSynced { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
}
