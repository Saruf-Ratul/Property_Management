namespace PropertyManagement.Application.DTOs;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FirstName, string LastName, string Role, Guid? ClientId);
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    UserDto User);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    Guid LawFirmId,
    Guid? ClientId,
    IReadOnlyList<string> Roles);
