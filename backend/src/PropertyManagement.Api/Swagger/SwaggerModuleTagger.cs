using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PropertyManagement.Api.Swagger;

/// <summary>
/// Maps controller names to friendly module tags so the Swagger UI groups endpoints by feature area
/// instead of by raw controller class name.
/// </summary>
public class SwaggerModuleTagger : IDocumentFilter
{
    private static readonly Dictionary<string, (string Tag, string Description)> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Auth"]                  = ("1. Authentication",        "Login, registration, and current-user endpoints (JWT)."),
        ["Clients"]               = ("2. Clients",               "Property-management company clients of the law firm."),
        ["Properties"]            = ("3. Properties",            "Properties synced from the connected PMS systems."),
        ["TenantsApi"]            = ("3. Properties",            "Tenants, leases, and delinquency from PMS data."),
        ["LeasesApi"]             = ("3. Properties",            "Lease ledger detail."),
        ["PmsIntegrations"]       = ("4. PMS Integrations",      "Rent Manager / Yardi / AppFolio / Buildium / PropertyFlow connectors and sync jobs."),
        ["Cases"]                 = ("5. Cases",                 "Landlord-tenant case lifecycle, comments, payments, documents, activity."),
        ["LtForms"]               = ("6. NJ LT Forms",           "Auto-fill, approve, generate, and merge New Jersey landlord-tenant court forms (legacy /api/cases/{id}/lt-forms route)."),
        ["LtCases"]               = ("6. NJ LT Forms",           "LT-case-centric endpoints: list, structured form data, validate, preview, generate, packet."),
        ["GeneratedDocuments"]    = ("6. NJ LT Forms",           "Download generated PDFs and filing packets."),
        ["Dashboard"]             = ("7. Dashboard",             "Aggregated KPIs across cases, tenants, and PMS sync."),
        ["ClientPortal"]          = ("7. Dashboard",             "Client-portal endpoints (ClientAdmin / ClientUser)."),
        ["AuditLogs"]             = ("8. Audit & Compliance",    "Immutable audit trail of sensitive actions."),
        ["Health"]                = ("9. System",                "Health and status endpoints."),
    };

    public void Apply(OpenApiDocument doc, DocumentFilterContext ctx)
    {
        var seen = new Dictionary<string, OpenApiTag>(StringComparer.Ordinal);

        foreach (var api in ctx.ApiDescriptions)
        {
            var controller = api.ActionDescriptor.RouteValues["controller"];
            if (string.IsNullOrEmpty(controller)) continue;

            var (tag, desc) = Map.TryGetValue(controller, out var v)
                ? v
                : (controller, $"{controller} endpoints.");

            // Replace the operation's tags with the module name.
            if (doc.Paths.TryGetValue("/" + api.RelativePath!.TrimStart('/'), out var pathItem))
            {
                foreach (var op in pathItem.Operations.Values)
                {
                    if (op.Tags is null || op.Tags.Count == 0 ||
                        string.Equals(op.Tags[0].Name, controller, StringComparison.OrdinalIgnoreCase))
                    {
                        op.Tags = new List<OpenApiTag> { new() { Name = tag } };
                    }
                }
            }

            if (!seen.ContainsKey(tag))
                seen[tag] = new OpenApiTag { Name = tag, Description = desc };
        }

        doc.Tags = seen.Values.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }
}
