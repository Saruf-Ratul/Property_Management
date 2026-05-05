using PropertyManagement.Application.Abstractions;

namespace PropertyManagement.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenant, ICurrentUser user)
    {
        if (user.IsAuthenticated && user.LawFirmId.HasValue)
            tenant.SetTenant(user.LawFirmId);
        await _next(ctx);
    }
}
