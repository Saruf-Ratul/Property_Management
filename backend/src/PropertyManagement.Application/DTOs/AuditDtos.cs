using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.DTOs;

public record AuditLogDto(
    Guid Id,
    DateTime OccurredAtUtc,
    AuditAction Action,
    string EntityType,
    string? EntityId,
    string? Summary,
    string? UserEmail,
    string? IpAddress);

/// <summary>Full single-record audit log payload — includes the old/new value JSON, payload, and user agent.</summary>
public record AuditLogDetailDto(
    Guid Id,
    DateTime OccurredAtUtc,
    AuditAction Action,
    string EntityType,
    string? EntityId,
    string? Summary,
    string? UserEmail,
    Guid? UserId,
    Guid? LawFirmId,
    string? IpAddress,
    string? UserAgent,
    string? PayloadJson,
    string? OldValueJson,
    string? NewValueJson);

public record SyncLogDto(
    Guid Id,
    Guid IntegrationId,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    SyncStatus Status,
    int PropertiesSynced,
    int UnitsSynced,
    int TenantsSynced,
    int LeasesSynced,
    int LedgerItemsSynced,
    string? Message,
    string? ErrorDetail);

public record DashboardStatsDto(
    int TotalCases,
    int ActiveCases,
    int ClosedCases,
    int DelinquentTenants,
    decimal TotalOutstandingBalance,
    IReadOnlyList<CaseStageCountDto> CasesByStage,
    IReadOnlyList<CaseClientCountDto> CasesByClient,
    IReadOnlyList<RecentActivityDto> RecentActivity,
    IReadOnlyList<PmsSyncStatusDto> PmsSyncStatus);

public record CaseStageCountDto(CaseStageCode Code, string Name, int Count);
public record CaseClientCountDto(Guid ClientId, string ClientName, int Count);
public record RecentActivityDto(DateTime OccurredAtUtc, string Summary, string? CaseNumber);
public record PmsSyncStatusDto(Guid IntegrationId, string DisplayName, string ClientName, DateTime? LastSyncAtUtc, SyncStatus? LastSyncStatus);
