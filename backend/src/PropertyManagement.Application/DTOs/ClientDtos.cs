namespace PropertyManagement.Application.DTOs;

public record ClientDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? City,
    string? State,
    bool IsActive,
    int IntegrationsCount,
    int CasesCount);

public record CreateClientRequest(
    string Name,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode);

public record UpdateClientRequest(
    string Name,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    bool IsActive);
