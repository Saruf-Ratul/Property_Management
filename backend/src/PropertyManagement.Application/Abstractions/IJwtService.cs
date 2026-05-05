namespace PropertyManagement.Application.Abstractions;

public interface IJwtService
{
    AuthTokens Issue(string userId, string email, IEnumerable<string> roles, Guid? lawFirmId, Guid? clientId);
}

public record AuthTokens(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
