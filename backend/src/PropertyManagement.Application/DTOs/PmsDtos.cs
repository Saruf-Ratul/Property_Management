using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.DTOs;

// ────────────────────────────────────────────────────────────────────────────
// Public API output for the PMS Integration table
// ────────────────────────────────────────────────────────────────────────────

public record PmsIntegrationDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    PmsProvider Provider,
    string DisplayName,
    string? BaseUrl,
    string? CompanyCode,
    string? LocationId,
    bool IsActive,
    DateTime? LastSyncAtUtc,
    SyncStatus? LastSyncStatus,
    string? LastSyncMessage,
    int SyncIntervalMinutes);

public record CreatePmsIntegrationRequest(
    Guid ClientId,
    PmsProvider Provider,
    string DisplayName,
    string? BaseUrl,
    string? Username,
    string? Password,
    string? CompanyCode,
    string? LocationId,
    int SyncIntervalMinutes);

public record UpdatePmsIntegrationRequest(
    string DisplayName,
    string? BaseUrl,
    string? Username,
    /// <summary>Leave null/empty to keep the previously-stored password unchanged.</summary>
    string? Password,
    string? CompanyCode,
    string? LocationId,
    int SyncIntervalMinutes,
    bool IsActive);

// ────────────────────────────────────────────────────────────────────────────
// Connection test request/result
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Used both for an in-place test against a stored integration and for an ad-hoc test using literal credentials.</summary>
public record PmsConnectionTestRequest(
    PmsProvider? Provider,
    string? BaseUrl,
    string? Username,
    string? Password,
    string? CompanyCode,
    string? LocationId,
    string? ApiKey);

public record PmsConnectionTestResult(
    bool IsConnected,
    string Message,
    string? ServerVersion,
    long LatencyMs,
    DateTime TestedAtUtc);

// ────────────────────────────────────────────────────────────────────────────
// Sync request/result
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Granular scope for /sync. Set FullSync=true to sync everything (default).</summary>
public record PmsSyncRequest(
    bool FullSync = true,
    bool SyncProperties = false,
    bool SyncUnits = false,
    bool SyncTenants = false,
    bool SyncLeases = false,
    bool SyncLedgerItems = false,
    bool RunInBackground = true);

public record PmsSyncResult(
    Guid SyncLogId,
    Guid IntegrationId,
    SyncStatus Status,
    int PropertiesSynced,
    int UnitsSynced,
    int TenantsSynced,
    int LeasesSynced,
    int LedgerItemsSynced,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    string? Message,
    string? ErrorDetail,
    string? BackgroundJobId);

// ────────────────────────────────────────────────────────────────────────────
// Property / unit / tenant / lease / ledger DTOs returned by the public /api/properties etc. endpoints.
// These are mapped from local EF entities (not directly from the PMS).
// (The connector contract types — also named Pms*Dto — live in IPmsConnector.cs.)
// ────────────────────────────────────────────────────────────────────────────

public record PropertyDto(
    Guid Id,
    Guid IntegrationId,
    string ExternalId,
    string Name,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? County,
    int? UnitCount,
    bool IsActive);

public record UnitDto(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    string ExternalId,
    string UnitNumber,
    int? Bedrooms,
    int? Bathrooms,
    decimal? MarketRent,
    bool IsOccupied);

public record TenantDto(
    Guid Id,
    string ExternalId,
    string FirstName,
    string LastName,
    string FullName,
    string? Email,
    string? Phone,
    bool IsActive,
    decimal CurrentBalance,
    string? UnitNumber,
    string? PropertyName);

public record LeaseDto(
    Guid Id,
    string ExternalId,
    Guid TenantId,
    string TenantName,
    Guid UnitId,
    string UnitNumber,
    string PropertyName,
    DateTime StartDate,
    DateTime? EndDate,
    decimal MonthlyRent,
    decimal CurrentBalance,
    bool IsActive);

public record LedgerItemDto(
    Guid Id,
    DateTime PostedDate,
    DateTime? DueDate,
    string Category,
    string? Description,
    decimal Amount,
    decimal Balance,
    bool IsCharge,
    bool IsPayment);

public record DelinquentTenantDto(
    Guid TenantId,
    string TenantName,
    Guid LeaseId,
    string PropertyName,
    string UnitNumber,
    decimal CurrentBalance,
    decimal MonthlyRent,
    int DaysDelinquent,
    string ClientName,
    Guid ClientId,
    Guid PropertyId);

// ────────────────────────────────────────────────────────────────────────────
// Filter & detail DTOs for the front-end PMS data pages
// ────────────────────────────────────────────────────────────────────────────

public class PropertyFilter
{
    public Guid? ClientId { get; set; }
    public PmsProvider? Provider { get; set; }
    public string? County { get; set; }
    public string? State { get; set; }
    public bool? IsActive { get; set; }
}

public class TenantFilter
{
    public Guid? ClientId { get; set; }
    public Guid? PropertyId { get; set; }
    /// <summary>If true, include only tenants on an active lease whose CurrentBalance > 0.</summary>
    public bool DelinquentOnly { get; set; }
    public decimal? MinBalance { get; set; }
    public bool? IsActive { get; set; }
}

public record PropertyDetailDto(
    Guid Id,
    Guid IntegrationId,
    string IntegrationDisplayName,
    PmsProvider Provider,
    Guid ClientId,
    string ClientName,
    string ExternalId,
    string Name,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? County,
    int? UnitCount,
    int OccupiedUnitCount,
    int VacantUnitCount,
    int ActiveLeaseCount,
    int DelinquentTenantCount,
    decimal OutstandingBalance,
    decimal AverageMarketRent,
    bool IsActive,
    DateTime CreatedAtUtc);

public record TenantDetailDto(
    Guid Id,
    string ExternalId,
    string FirstName,
    string LastName,
    string FullName,
    string? Email,
    string? Phone,
    DateTime? DateOfBirth,
    bool IsActive,
    Guid IntegrationId,
    string IntegrationDisplayName,
    Guid ClientId,
    string ClientName,
    decimal CurrentBalance,
    Guid? ActiveLeaseId,
    Guid? PropertyId,
    string? PropertyName,
    string? UnitNumber,
    decimal? MonthlyRent,
    DateTime? LeaseStart,
    DateTime? LeaseEnd);

public record PropertyLedgerSummaryDto(
    Guid PropertyId,
    decimal TotalCharges,
    decimal TotalPayments,
    decimal OutstandingBalance,
    int ActiveLeases,
    int DelinquentLeases,
    DateTime? OldestUnpaidPostedAt,
    IReadOnlyList<LedgerItemDto> RecentItems);

// ────────────────────────────────────────────────────────────────────────────
// Delinquency dashboard
// ────────────────────────────────────────────────────────────────────────────

public record DelinquencyStatsDto(
    int TotalDelinquentTenants,
    decimal TotalOutstandingBalance,
    decimal AverageBalance,
    int OldestUnpaidDays,
    IReadOnlyList<TopDelinquentPropertyDto> TopPropertiesByBalance,
    IReadOnlyList<DelinquentTenantDto> OldestUnpaidTenants);

public record TopDelinquentPropertyDto(
    Guid PropertyId,
    string PropertyName,
    string ClientName,
    Guid ClientId,
    int DelinquentTenantCount,
    decimal OutstandingBalance,
    decimal LargestSingleBalance);
