using System.Text.Json;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PropertyManagement.Infrastructure.Services;

public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _log;

    public AuditService(AppDbContext db, ICurrentUser user, IHttpContextAccessor http, ILogger<AuditService> log)
    {
        _db = db; _user = user; _http = http; _log = log;
    }

    public Task LogAsync(AuditAction action, string entityType, string? entityId, string? summary,
        object? payload = null, CancellationToken ct = default)
        => WriteAsync(action, entityType, entityId, summary,
            payload: payload, oldValue: null, newValue: null,
            userId: _user.UserId?.ToString(), userEmail: _user.Email,
            lawFirmId: _user.LawFirmId, ct: ct);

    public Task LogChangeAsync(AuditAction action, string entityType, string? entityId, string? summary,
        object? oldValue, object? newValue, CancellationToken ct = default)
        => WriteAsync(action, entityType, entityId, summary,
            payload: null, oldValue: oldValue, newValue: newValue,
            userId: _user.UserId?.ToString(), userEmail: _user.Email,
            lawFirmId: _user.LawFirmId, ct: ct);

    public Task LogLoginSuccessAsync(string userId, string userEmail, Guid? lawFirmId,
        string? ip, string? userAgent, CancellationToken ct = default)
        => WriteAsync(AuditAction.Login, "User", userId,
            $"User {userEmail} logged in",
            payload: null, oldValue: null, newValue: null,
            userId: userId, userEmail: userEmail, lawFirmId: lawFirmId,
            ct: ct, ip: ip, userAgent: userAgent);

    public Task LogLoginFailedAsync(string attemptedEmail, string reason,
        Guid? lawFirmId, string? ip, string? userAgent, CancellationToken ct = default)
        => WriteAsync(AuditAction.LoginFailed, "User", null,
            $"Failed login for {attemptedEmail}: {reason}",
            payload: new { attemptedEmail, reason },
            oldValue: null, newValue: null,
            userId: null, userEmail: attemptedEmail, lawFirmId: lawFirmId,
            ct: ct, ip: ip, userAgent: userAgent);

    // ─── core writer ────────────────────────────────────────────────────────
    private async Task WriteAsync(
        AuditAction action, string entityType, string? entityId, string? summary,
        object? payload, object? oldValue, object? newValue,
        string? userId, string? userEmail, Guid? lawFirmId,
        CancellationToken ct,
        string? ip = null, string? userAgent = null)
    {
        try
        {
            ip       ??= _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
            userAgent??= _http.HttpContext?.Request.Headers.UserAgent.ToString();

            _db.AuditLogs.Add(new AuditLog
            {
                LawFirmId = lawFirmId,
                UserId = Guid.TryParse(userId, out var g) ? g : null,
                UserEmail = userEmail,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Summary = Truncate(summary, 1000),
                PayloadJson = SerializeRedacted(payload),
                OldValueJson = SerializeRedacted(oldValue),
                NewValueJson = SerializeRedacted(newValue),
                IpAddress = ip,
                UserAgent = Truncate(userAgent, 500),
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failures must NEVER abort the originating business operation.
            _log.LogError(ex, "Failed to write audit log {Action} {EntityType}/{EntityId}",
                action, entityType, entityId);
        }
    }

    // ─── helpers ────────────────────────────────────────────────────────────
    private static string? Truncate(string? s, int max) =>
        s is null ? null : (s.Length <= max ? s : s.Substring(0, max));

    /// <summary>
    /// Serialize the value to JSON, scrubbing common credential property names so we never write
    /// secrets into the audit table — defense in depth for the audit log itself.
    /// </summary>
    private static string? SerializeRedacted(object? value)
    {
        if (value is null) return null;
        try
        {
            var raw = JsonSerializer.Serialize(value, Json);
            return ScrubSecrets(raw);
        }
        catch (Exception ex)
        {
            return $"{{\"_serializationError\":\"{ex.GetType().Name}\"}}";
        }
    }

    private static readonly string[] SecretKeys =
        { "password", "credentialscipher", "apikey", "token", "secret", "refreshtoken" };

    private static string ScrubSecrets(string json)
    {
        // Cheap & cheerful — replace the value of any "password":"..." style pair.
        // Good enough for the audit-log defense-in-depth pass; the primary defense is not
        // passing secrets into LogAsync() in the first place.
        foreach (var key in SecretKeys)
        {
            var pattern = $"\"{key}\"\\s*:\\s*\"[^\"]*\"";
            json = System.Text.RegularExpressions.Regex.Replace(
                json, pattern, $"\"{key}\":\"***\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return json;
    }
}
