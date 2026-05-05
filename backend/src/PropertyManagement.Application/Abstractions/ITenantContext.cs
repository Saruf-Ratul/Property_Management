namespace PropertyManagement.Application.Abstractions;

/// <summary>
/// Holds the active tenant (law firm) for the current request/job. Bypass = true disables
/// the global query filter (used for system jobs and cross-tenant admin operations).
/// </summary>
public interface ITenantContext
{
    Guid? LawFirmId { get; }
    bool BypassFilter { get; }
    void SetTenant(Guid? lawFirmId);
    IDisposable Bypass();
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    Guid? LawFirmId { get; }
    Guid? ClientId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
}
