using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Infrastructure.Multitenancy;

namespace PropertyManagement.Infrastructure.Persistence;

/// <summary>
/// Used by the <c>dotnet ef</c> CLI to construct an <see cref="AppDbContext"/> at design time
/// (e.g. for <c>migrations add</c> / <c>database update</c>). The runtime DI container is wired
/// for in-memory by default, which doesn't support migrations — this factory always uses the
/// SQL Server provider so the tooling works regardless of <c>Database:UseInMemory</c>.
///
/// The connection string is resolved from (in order):
///   1. <c>--connection</c> arg passed to <c>dotnet ef</c>
///   2. <c>ConnectionStrings__Default</c> environment variable
///   3. <c>ConnectionStrings:Default</c> in the API's <c>appsettings.json</c> /
///      <c>appsettings.Development.json</c>
///   4. A LocalDB fallback so a fresh clone works without configuration.
/// </summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var apiProjectDir = ResolveApiProjectDir();
        var config = new ConfigurationBuilder()
            .SetBasePath(apiProjectDir)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connStr = config["ConnectionStrings:Default"]
                   ?? config["connection"]
                   ?? @"Server=(localdb)\MSSQLLocalDB;Database=PropertyManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connStr, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        // Migrations run with the tenant filter bypassed; we don't need a real tenant or user.
        var tenant = new TenantContext();
        _ = tenant.Bypass();   // BypassScope only resets on Dispose, which we never call.
        return new AppDbContext(options, tenant, new DesignTimeUser());
    }

    /// <summary>Stand-in <see cref="ICurrentUser"/> for design-time tooling — never authenticated.</summary>
    private sealed class DesignTimeUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public string? Email => null;
        public Guid? LawFirmId => null;
        public Guid? ClientId => null;
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public bool IsInRole(string role) => false;
    }

    private static string ResolveApiProjectDir()
    {
        // We're invoked from the Infrastructure project folder; the API project lives one level up.
        var infrastructureDir = Directory.GetCurrentDirectory();
        var solutionRoot = new DirectoryInfo(infrastructureDir);
        while (solutionRoot is not null && !File.Exists(Path.Combine(solutionRoot.FullName, "PropertyManagement.sln")))
            solutionRoot = solutionRoot.Parent;

        var api = solutionRoot is not null
            ? Path.Combine(solutionRoot.FullName, "src", "PropertyManagement.Api")
            : infrastructureDir;
        return Directory.Exists(api) ? api : infrastructureDir;
    }
}
