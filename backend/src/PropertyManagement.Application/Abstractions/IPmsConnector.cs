using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions;

/// <summary>
/// Generic abstraction over a Property Management System (PMS) provider.
/// One implementation per provider, exposed through a marker interface so that
/// dependency injection can resolve the right connector by type.
/// </summary>
public interface IPmsConnector
{
    PmsProvider Provider { get; }

    /// <summary>Validate that the supplied credentials/URL allow a successful authenticated request.</summary>
    Task<PmsConnectionTestOutcome> TestConnectionAsync(PmsConnectionContext ctx, CancellationToken ct);

    IAsyncEnumerable<PmsPropertyDto> GetPropertiesAsync(PmsConnectionContext ctx, CancellationToken ct);
    IAsyncEnumerable<PmsUnitDto> GetUnitsAsync(PmsConnectionContext ctx, string propertyExternalId, CancellationToken ct);
    IAsyncEnumerable<PmsTenantDto> GetTenantsAsync(PmsConnectionContext ctx, CancellationToken ct);
    IAsyncEnumerable<PmsLeaseDto> GetLeasesAsync(PmsConnectionContext ctx, CancellationToken ct);
    IAsyncEnumerable<PmsLedgerItemDto> GetLedgerAsync(PmsConnectionContext ctx, string leaseExternalId, CancellationToken ct);
}

/// <summary>Marker interface — DI resolves Rent Manager bindings via this type.</summary>
public interface IRentManagerConnector : IPmsConnector { }

/// <summary>Marker interface — DI resolves Yardi bindings via this type.</summary>
public interface IYardiConnector : IPmsConnector { }

/// <summary>Marker interface — DI resolves AppFolio bindings via this type.</summary>
public interface IAppFolioConnector : IPmsConnector { }

/// <summary>Marker interface — DI resolves Buildium bindings via this type.</summary>
public interface IBuildiumConnector : IPmsConnector { }

/// <summary>Marker interface — DI resolves PropertyFlow bindings via this type.</summary>
public interface IPropertyFlowConnector : IPmsConnector { }

/// <summary>Resolves the right connector for a given provider at runtime.</summary>
public interface IPmsConnectorFactory
{
    IPmsConnector Get(PmsProvider provider);
}

// ────────────────────────────────────────────────────────────────────────────
// Connection / context types
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Credentials and endpoint info passed to a connector for one operation.</summary>
public class PmsConnectionContext
{
    public string? BaseUrl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? CompanyCode { get; set; }
    public string? LocationId { get; set; }
    public string? ApiKey { get; set; }
}

/// <summary>Returned by <see cref="IPmsConnector.TestConnectionAsync"/> — contract for connection probes.</summary>
public class PmsConnectionTestOutcome
{
    public bool IsConnected { get; init; }
    public string? Message { get; init; }
    public string? ServerVersion { get; init; }
    public TimeSpan Latency { get; init; }

    public static PmsConnectionTestOutcome Ok(string? message = null, string? version = null, TimeSpan latency = default)
        => new() { IsConnected = true, Message = message ?? "Connection OK", ServerVersion = version, Latency = latency };

    public static PmsConnectionTestOutcome Fail(string message)
        => new() { IsConnected = false, Message = message };
}

// ────────────────────────────────────────────────────────────────────────────
// Source-of-truth DTOs for entities pulled from PMS providers
// ────────────────────────────────────────────────────────────────────────────

public class PmsPropertyDto
{
    public string ExternalId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? County { get; set; }
    public int? UnitCount { get; set; }
}

public class PmsUnitDto
{
    public string ExternalId { get; set; } = null!;
    public string PropertyExternalId { get; set; } = null!;
    public string UnitNumber { get; set; } = null!;
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public decimal? SquareFeet { get; set; }
    public decimal? MarketRent { get; set; }
    public bool IsOccupied { get; set; }
}

public class PmsTenantDto
{
    public string ExternalId { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PmsLeaseDto
{
    public string ExternalId { get; set; } = null!;
    public string UnitExternalId { get; set; } = null!;
    public string TenantExternalId { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public bool IsMonthToMonth { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal CurrentBalance { get; set; }
}

public class PmsLedgerItemDto
{
    public string ExternalId { get; set; } = null!;
    public string LeaseExternalId { get; set; } = null!;
    public DateTime PostedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public bool IsCharge { get; set; }
    public bool IsPayment { get; set; }
}
