using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PropertyManagement.Infrastructure.Persistence;

namespace PropertyManagement.IntegrationTests;

/// <summary>
/// WebApplicationFactory that boots the full API in-process against the in-memory EF + Hangfire stack.
/// Tests share a single instance via <see cref="ApiCollection"/>.
/// The fixture explicitly invokes <see cref="DataSeeder.SeedAsync"/> (bootstrap admin + reference data)
/// and then <see cref="DataSeeder.SeedTestFixturesAsync"/> (test-only demo client + extra role accounts)
/// so role/scoping tests can authenticate as a Lawyer/Paralegal/ClientAdmin/Auditor without polluting
/// production seeding with demo data.
/// </summary>
public class PropertyManagementApiFactory : WebApplicationFactory<Program>
{
    private bool _seeded;
    private readonly object _seedLock = new();

    static PropertyManagementApiFactory()
    {
        // AddInfrastructure reads "Database:UseInMemory" / "Hangfire:UseInMemory" inline
        // during Program.cs execution, BEFORE the WebApplicationFactory's
        // ConfigureAppConfiguration callbacks fire. Pushing the override into environment
        // variables (loaded by the default ASP.NET Core config pipeline before the host
        // builder runs) is the only way to make the override visible at registration time.
        // We also clear the connection string so AddInfrastructure unambiguously falls
        // through to the InMemory branch.
        Environment.SetEnvironmentVariable("Database__UseInMemory", "true");
        Environment.SetEnvironmentVariable("Hangfire__UseInMemory", "true");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:LocalRoot"] = Path.Combine(Path.GetTempPath(), "pm-tests-" + Guid.NewGuid().ToString("N")),
            });
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        EnsureSeeded();
        base.ConfigureClient(client);
    }

    private void EnsureSeeded()
    {
        if (_seeded) return;
        lock (_seedLock)
        {
            if (_seeded) return;
            using var scope = Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PropertyManagementApiFactory>>();
            DataSeeder.SeedAsync(Services, logger).GetAwaiter().GetResult();
            DataSeeder.SeedTestFixturesAsync(Services, logger).GetAwaiter().GetResult();
            _seeded = true;
        }
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<PropertyManagementApiFactory> { }
