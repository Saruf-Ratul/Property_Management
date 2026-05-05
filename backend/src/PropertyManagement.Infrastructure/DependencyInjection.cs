using System.Text;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Infrastructure.Auth;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Multitenancy;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Pms;
using PropertyManagement.Infrastructure.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;

namespace PropertyManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Database
        var connStr = config.GetConnectionString("Default") ?? string.Empty;
        var useInMemory = config.GetValue<bool>("Database:UseInMemory");
        services.AddDbContext<AppDbContext>(opt =>
        {
            if (useInMemory || string.IsNullOrWhiteSpace(connStr))
            {
                opt.UseInMemoryDatabase("PropertyManagementDev");
            }
            else
            {
                opt.UseSqlServer(connStr, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            }
            // EF Core 9 raises PendingModelChangesWarning when the model differs from the latest
            // migration snapshot (e.g. when running against the InMemory provider in tests, or when
            // dev-branching ahead of `dotnet ef migrations add`). The dev/test path doesn't apply
            // SQL migrations, so we degrade this warning to a log line instead of an exception.
            opt.ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Identity
        services
            .AddIdentity<ApplicationUser, ApplicationRole>(opts =>
            {
                opts.Password.RequiredLength = 8;
                opts.Password.RequireNonAlphanumeric = true;
                opts.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // JWT
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        var jwtCfg = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions
        {
            SigningKey = "PropertyManagementDevelopmentSigningKey_ChangeMe_Min32Chars_!"
        };
        if (string.IsNullOrWhiteSpace(jwtCfg.SigningKey) || jwtCfg.SigningKey.Length < 32)
            jwtCfg.SigningKey = "PropertyManagementDevelopmentSigningKey_ChangeMe_Min32Chars_!";

        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtCfg.Issuer,
                ValidAudience = jwtCfg.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtCfg.SigningKey)),
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });

        // Global authorization: every endpoint REQUIRES an authenticated user by default.
        // Endpoints that should be public must opt out with [AllowAnonymous] (login, swagger,
        // health). Named policies for role-scoped controllers live alongside [Authorize] attributes.
        services.AddAuthorization(opts =>
        {
            opts.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            opts.AddPolicy("FirmStaff",  p => p.RequireRole(
                Domain.Common.Roles.FirmAdmin, Domain.Common.Roles.Lawyer, Domain.Common.Roles.Paralegal));
            opts.AddPolicy("FirmAdminOnly",   p => p.RequireRole(Domain.Common.Roles.FirmAdmin));
            opts.AddPolicy("LawyerOrFirmAdmin", p => p.RequireRole(
                Domain.Common.Roles.FirmAdmin, Domain.Common.Roles.Lawyer));
            opts.AddPolicy("ClientPortal", p => p.RequireRole(
                Domain.Common.Roles.ClientAdmin, Domain.Common.Roles.ClientUser));
            opts.AddPolicy("AuditViewer", p => p.RequireRole(
                Domain.Common.Roles.FirmAdmin, Domain.Common.Roles.Auditor));
        });

        // Multi-tenancy & current user
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Data protection (used to encrypt PMS credentials)
        services.AddDataProtection();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Storage / PDF
        services.AddSingleton<IDocumentStorage, LocalFileDocumentStorage>();
        services.AddSingleton<PdfFormFiller>();
        services.AddSingleton<IPdfFormFiller>(sp => sp.GetRequiredService<PdfFormFiller>());
        services.AddSingleton<IPdfFormFillerService>(sp => sp.GetRequiredService<PdfFormFiller>());
        services.AddSingleton<IPdfFieldMappingService, PdfFieldMappingService>();
        services.AddSingleton<IRedactionValidator, RedactionValidator>();

        // JWT
        services.AddScoped<IJwtService, JwtService>();

        // PMS connectors — one impl per provider, exposed via marker interface, plus a factory.
        // The Rent Manager connector also gets a Polly retry policy on its HttpClient so transient
        // network/HTTP failures during real API calls retry with exponential backoff.
        services.AddHttpClient<IRentManagerConnector, RentManagerConnector>(c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
                c.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: a => TimeSpan.FromMilliseconds(Math.Pow(2, a) * 250)));

        services.AddScoped<IYardiConnector, YardiConnector>();
        services.AddScoped<IAppFolioConnector, AppFolioConnector>();
        services.AddScoped<IBuildiumConnector, BuildiumConnector>();
        services.AddScoped<IPropertyFlowConnector, PropertyFlowConnector>();
        services.AddScoped<IPmsConnectorFactory, PmsConnectorFactory>();

        // Services
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IPmsIntegrationService, PmsIntegrationService>();
        services.AddScoped<IPmsSyncService, PmsSyncService>();
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<ILtFormService, LtFormService>();
        services.AddScoped<ILtCaseService, LtCaseService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IClientPortalService, ClientPortalService>();

        // Hangfire
        var useMemoryHangfire = useInMemory || string.IsNullOrWhiteSpace(connStr) ||
                                config.GetValue<bool>("Hangfire:UseInMemory");
        services.AddHangfire(cfg =>
        {
            cfg.UseSimpleAssemblyNameTypeSerializer()
               .UseRecommendedSerializerSettings();
            if (useMemoryHangfire)
                cfg.UseMemoryStorage();
            else
                cfg.UseSqlServerStorage(connStr, new SqlServerStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(15)
                });
        });
        services.AddHangfireServer();

        return services;
    }
}
