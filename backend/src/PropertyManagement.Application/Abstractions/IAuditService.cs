using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions;

/// <summary>
/// Append-only audit log writer. Every sensitive action across the platform routes through here.
/// All implementations must:
///   • capture the calling user, IP, and User-Agent automatically;
///   • be safe to call inside a transaction (no nested SaveChanges that would commit early);
///   • never throw — audit failures must be swallowed and logged so the underlying business
///     operation is not disrupted.
/// </summary>
public interface IAuditService
{
    /// <summary>Generic event log — use <see cref="LogChangeAsync"/> when you have a clean before/after pair.</summary>
    Task LogAsync(AuditAction action, string entityType, string? entityId, string? summary,
        object? payload = null, CancellationToken ct = default);

    /// <summary>Update / change event with explicit before/after snapshots.</summary>
    Task LogChangeAsync(AuditAction action, string entityType, string? entityId, string? summary,
        object? oldValue, object? newValue, CancellationToken ct = default);

    /// <summary>Convenience: successful login.</summary>
    Task LogLoginSuccessAsync(string userId, string userEmail, Guid? lawFirmId,
        string? ip, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Convenience: failed login. <paramref name="lawFirmId"/> may be passed when the failing user
    /// exists (so firm admins can see attempts against their tenant); pass null for "user not found"
    /// attempts whose firm is unknown.
    /// </summary>
    Task LogLoginFailedAsync(string attemptedEmail, string reason,
        Guid? lawFirmId, string? ip, string? userAgent, CancellationToken ct = default);
}
