using PropertyManagement.Api.Hangfire;
using PropertyManagement.Api.Middleware;
using PropertyManagement.Api.Swagger;
using PropertyManagement.Application;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Infrastructure;
using PropertyManagement.Infrastructure.Persistence;
using Hangfire;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/PropertyManagement-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

// --- Services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddCors(o => o.AddPolicy("frontend", p =>
    p.WithOrigins(
        "http://localhost:5173", "http://127.0.0.1:5173",
        "http://localhost:4173")
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Property Management API",
        Version = "v1",
        Description =
            "Multi-tenant property management & landlord-tenant legal case automation platform.\n\n" +
            "**How to authenticate:**\n" +
            "1. Call `POST /api/auth/login` with the bootstrap admin (`admin@pm.local` / `Admin!2345`) " +
            "or any user you've created.\n" +
            "2. Copy the `accessToken` from the response.\n" +
            "3. Click **Authorize** below and enter `Bearer <accessToken>`.\n",
        Contact = new OpenApiContact { Name = "Property Management Platform" }
    });

    // Group endpoints by module
    c.DocumentFilter<SwaggerModuleTagger>();
    c.TagActionsBy(api => new[] { api.ActionDescriptor.RouteValues["controller"] ?? "Other" });
    c.OrderActionsBy(api => $"{api.RelativePath}_{api.HttpMethod}");

    // Pull <summary>, <param>, <returns> from XML docs of the Api / Application / Infrastructure projects.
    foreach (var asm in new[] { "PropertyManagement.Api", "PropertyManagement.Application", "PropertyManagement.Infrastructure" })
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{asm}.xml");
        if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // JWT bearer
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT bearer token. Paste \"Bearer <token>\" or just the raw token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// --- Pipeline
app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Property Management API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Property Management API";
    c.DefaultModelsExpandDepth(-1);
});

app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Recurring jobs
RecurringJob.AddOrUpdate<IPmsSyncService>("pms-sync-all", s => s.SyncAllActiveAsync(CancellationToken.None), Cron.Daily(2));

// Seed
await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try { await DataSeeder.SeedAsync(app.Services, logger); }
    catch (Exception ex) { logger.LogError(ex, "Seeding failed"); }
}

app.Run();

// Allow WebApplicationFactory<Program> in the integration test project to find Program.
public partial class Program { }
