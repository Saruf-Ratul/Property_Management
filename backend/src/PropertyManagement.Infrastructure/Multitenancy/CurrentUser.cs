using System.Security.Claims;
using PropertyManagement.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace PropertyManagement.Infrastructure.Multitenancy;

public class CurrentUser : ICurrentUser
{
    public const string ClaimLawFirmId = "law_firm_id";
    public const string ClaimClientId = "client_id";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var sub = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? Principal?.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public Guid? LawFirmId
    {
        get
        {
            var v = Principal?.FindFirst(ClaimLawFirmId)?.Value;
            return Guid.TryParse(v, out var id) ? id : null;
        }
    }

    public Guid? ClientId
    {
        get
        {
            var v = Principal?.FindFirst(ClaimClientId)?.Value;
            return Guid.TryParse(v, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
