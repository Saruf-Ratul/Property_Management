using PropertyManagement.Domain.Common;
using Hangfire.Dashboard;

namespace PropertyManagement.Api.Hangfire;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var user = http.User;
        if (user.Identity?.IsAuthenticated != true) return false;
        return user.IsInRole(Roles.FirmAdmin);
    }
}
