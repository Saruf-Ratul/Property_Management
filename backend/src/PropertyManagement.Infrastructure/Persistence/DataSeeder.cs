using PropertyManagement.Application.Abstractions;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PropertyManagement.Infrastructure.Persistence;

/// <summary>
/// Database bootstrap.
///
/// <see cref="SeedAsync"/> only seeds the bare minimum required to use the platform:
///   • System reference data — roles, case stages, case statuses
///   • One law firm and one bootstrap FirmAdmin so the operator can sign in on first launch
///
/// All "demo" content (sample client, sample PMS integration, sample tenants/leases,
/// pre-built example cases, additional seeded users) lives in <see cref="SeedTestFixturesAsync"/>
/// and is invoked only by the integration test harness — it never runs in production.
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// Default password for the bootstrap admin. Change this on first login or override the
    /// account from your secret store. The seeder never overwrites an existing user.
    /// </summary>
    public const string BootstrapAdminEmail = "admin@pm.local";
    public const string BootstrapAdminPassword = "Admin!2345";

    public static async Task SeedAsync(IServiceProvider sp, ILogger logger)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var rolesMgr = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        using var bypass = tenant.Bypass();

        await SeedRolesAsync(rolesMgr);
        await SeedCaseStagesAndStatusesAsync(db);
        var firm = await EnsureBootstrapFirmAsync(db);
        await EnsureBootstrapAdminAsync(users, db, firm.Id, logger);

        logger.LogInformation(
            "Seed complete. Bootstrap admin: {Email} (change this password on first login).",
            BootstrapAdminEmail);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // System reference data — roles, case stages, case statuses
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> rolesMgr)
    {
        foreach (var role in Roles.All)
        {
            if (!await rolesMgr.RoleExistsAsync(role))
                await rolesMgr.CreateAsync(new ApplicationRole { Name = role });
        }
    }

    private static async Task SeedCaseStagesAndStatusesAsync(AppDbContext db)
    {
        var stageMap = new (CaseStageCode Code, string Name, int Sort, bool Terminal)[]
        {
            (CaseStageCode.Intake, "Intake", 1, false),
            (CaseStageCode.Draft, "Draft", 2, false),
            (CaseStageCode.FormReview, "Form Review", 3, false),
            (CaseStageCode.ReadyToFile, "Ready to File", 4, false),
            (CaseStageCode.Filed, "Filed", 5, false),
            (CaseStageCode.CourtDateScheduled, "Court Date Scheduled", 6, false),
            (CaseStageCode.Judgment, "Judgment", 7, false),
            (CaseStageCode.Settlement, "Settlement", 8, false),
            (CaseStageCode.Dismissed, "Dismissed", 9, true),
            (CaseStageCode.WarrantRequested, "Warrant Requested", 10, false),
            (CaseStageCode.Closed, "Closed", 11, true)
        };
        foreach (var (code, name, sort, term) in stageMap)
        {
            if (!await db.CaseStages.AnyAsync(s => s.Code == code))
                db.CaseStages.Add(new CaseStage { Code = code, Name = name, SortOrder = sort, IsTerminal = term });
        }

        var statusMap = new (CaseStatusCode Code, string Name, bool Term)[]
        {
            (CaseStatusCode.Open, "Open", false),
            (CaseStatusCode.OnHold, "On Hold", false),
            (CaseStatusCode.Closed, "Closed", true),
            (CaseStatusCode.Cancelled, "Cancelled", true)
        };
        foreach (var (code, name, term) in statusMap)
        {
            if (!await db.CaseStatuses.AnyAsync(s => s.Code == code))
                db.CaseStatuses.Add(new CaseStatus { Code = code, Name = name, IsTerminal = term });
        }
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bootstrap law firm + bootstrap admin (always present so the app is usable)
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<LawFirm> EnsureBootstrapFirmAsync(AppDbContext db)
    {
        var firm = await db.LawFirms.IgnoreQueryFilters()
            .OrderBy(f => f.CreatedAtUtc).FirstOrDefaultAsync();
        if (firm is not null) return firm;

        firm = new LawFirm
        {
            Name = "Your Law Firm",
            AddressLine1 = "",
            City = "",
            State = "",
            PostalCode = "",
            Phone = "",
            Email = ""
        };
        db.LawFirms.Add(firm);
        await db.SaveChangesAsync();
        return firm;
    }

    private static async Task EnsureBootstrapAdminAsync(
        UserManager<ApplicationUser> users, AppDbContext db, Guid lawFirmId, ILogger logger)
    {
        var existing = await users.FindByEmailAsync(BootstrapAdminEmail);
        if (existing is not null) return;

        var admin = new ApplicationUser
        {
            UserName = BootstrapAdminEmail,
            Email = BootstrapAdminEmail,
            EmailConfirmed = true,
            FirstName = "Firm",
            LastName = "Administrator",
            LawFirmId = lawFirmId,
            IsActive = true
        };
        var res = await users.CreateAsync(admin, BootstrapAdminPassword);
        if (!res.Succeeded)
        {
            logger.LogError("Bootstrap admin creation failed: {Errors}",
                string.Join("; ", res.Errors.Select(e => e.Description)));
            return;
        }
        await users.AddToRoleAsync(admin, Roles.FirmAdmin);
        db.UserProfiles.Add(new UserProfile
        {
            IdentityUserId = admin.Id,
            LawFirmId = lawFirmId,
            Email = BootstrapAdminEmail,
            FirstName = admin.FirstName,
            LastName = admin.LastName,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test fixtures — invoked only by the integration test harness.
    // Adds a demo client, a demo PMS integration (mock-mode), additional seeded
    // users, mock-synced PMS data, and two example cases. Never called from
    // production startup.
    // ─────────────────────────────────────────────────────────────────────────

    public static async Task SeedTestFixturesAsync(IServiceProvider sp, ILogger logger)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        using var bypass = tenant.Bypass();

        var firm = await db.LawFirms.IgnoreQueryFilters().FirstAsync();

        if (!await db.AttorneySettings.IgnoreQueryFilters().AnyAsync(a => a.LawFirmId == firm.Id))
        {
            db.AttorneySettings.Add(new AttorneySetting
            {
                LawFirmId = firm.Id,
                FirmDisplayName = "Test Law Firm, P.C.",
                AttorneyName = "Jane Q. Counselor, Esq.",
                BarNumber = "NJ-TEST-0001",
                AttorneyEmail = "jane@pm.local",
                AttorneyPhone = "(973) 555-0100",
                OfficeAddressLine1 = "100 Court Street",
                OfficeCity = "Newark",
                OfficeState = "NJ",
                OfficePostalCode = "07102",
                DefaultCourtVenue = "Essex County Special Civil Part — Landlord/Tenant"
            });
            await db.SaveChangesAsync();
        }

        var client = await db.Clients.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.LawFirmId == firm.Id && c.Name == "Acme Property Management");
        if (client is null)
        {
            client = new Client
            {
                LawFirmId = firm.Id,
                Name = "Acme Property Management",
                ContactName = "Pat Manager",
                ContactEmail = "pat@acme.local",
                ContactPhone = "(201) 555-0500",
                AddressLine1 = "10 Hudson Plaza",
                City = "Jersey City",
                State = "NJ",
                PostalCode = "07310"
            };
            db.Clients.Add(client);
            await db.SaveChangesAsync();
        }

        async Task EnsureUserAsync(string email, string first, string last, string role, Guid? clientId = null)
        {
            if (await users.FindByEmailAsync(email) is not null) return;
            var u = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                FirstName = first, LastName = last,
                LawFirmId = firm.Id, ClientId = clientId, IsActive = true
            };
            var res = await users.CreateAsync(u, "Admin!2345");
            if (!res.Succeeded)
            {
                logger.LogWarning("Test user {Email} failed: {Errors}", email,
                    string.Join(';', res.Errors.Select(e => e.Description)));
                return;
            }
            await users.AddToRoleAsync(u, role);
            db.UserProfiles.Add(new UserProfile
            {
                IdentityUserId = u.Id, LawFirmId = firm.Id, Email = email,
                FirstName = first, LastName = last, ClientId = clientId, IsActive = true
            });
            await db.SaveChangesAsync();
        }

        await EnsureUserAsync("lawyer@pm.local", "Lara", "Lawyer", Roles.Lawyer);
        await EnsureUserAsync("paralegal@pm.local", "Pam", "Paralegal", Roles.Paralegal);
        await EnsureUserAsync("client@acme.local", "Pat", "Manager", Roles.ClientAdmin, client.Id);
        await EnsureUserAsync("auditor@pm.local", "Audrey", "Auditor", Roles.Auditor);

        logger.LogInformation("Test fixtures seeded for {Firm} / {Client}.", firm.Name, client.Name);
    }
}
