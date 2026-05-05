using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PropertyManagement.Application.Abstractions;

namespace PropertyManagement.Infrastructure.Pms;

/// <summary>
/// Thin wrapper over the Rent Manager 12 REST API.
///
/// Auth flow:
///   POST {base}/Authentication/AuthorizeUser  body { Username, Password, LocationID }
///     → returns the token as a JSON-quoted string (e.g. <c>"abc123…"</c>) which then has
///       to be sent as <c>X-RM12Api-ApiToken: &lt;token&gt;</c> on every subsequent request.
///
/// Endpoint discovery: the RM12 customer's API host is <c>{customer}.api.rentmanager.com</c>.
/// Some users paste the web client URL <c>{customer}.rmx.rentmanager.com</c> instead, which is
/// not reachable from outside their VPN. <see cref="NormalizeApiBaseUrl"/> rewrites that for them.
/// </summary>
public sealed class RentManagerApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public RentManagerApiClient(HttpClient http) { _http = http; }

    /// <summary>
    /// Rewrites the BaseUrl the user typed into the canonical RM12 API host.
    /// Trims trailing slashes and turns <c>https://acme.rmx.rentmanager.com</c> into
    /// <c>https://acme.api.rentmanager.com</c>. Returns the original URL untouched if it
    /// already points at <c>.api.</c> or some other host.
    /// </summary>
    public static string NormalizeApiBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
        var url = baseUrl.Trim().TrimEnd('/');
        if (url.Contains(".rmx.rentmanager.com", StringComparison.OrdinalIgnoreCase))
            url = url.Replace(".rmx.rentmanager.com", ".api.rentmanager.com",
                              StringComparison.OrdinalIgnoreCase);
        return url;
    }

    /// <summary>Authenticate and return a tuple of (token, latency, error?).</summary>
    public async Task<(string? Token, TimeSpan Elapsed, string? Error)> AuthorizeAsync(
        PmsConnectionContext ctx, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var apiBase = NormalizeApiBaseUrl(ctx.BaseUrl);
        if (string.IsNullOrWhiteSpace(apiBase))
            return (null, sw.Elapsed, "Base URL is required.");
        if (string.IsNullOrWhiteSpace(ctx.Username) || string.IsNullOrWhiteSpace(ctx.Password))
            return (null, sw.Elapsed, "Username and password are required.");

        // Rent Manager wants LocationID as a number; if the user typed a string like "NJ-1"
        // we fall back to 1. They can override with a numeric LocationId on the integration.
        var locationId = 1;
        if (!string.IsNullOrWhiteSpace(ctx.LocationId) && int.TryParse(ctx.LocationId, out var parsed))
            locationId = parsed;

        var body = new { Username = ctx.Username, Password = ctx.Password, LocationID = locationId };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/Authentication/AuthorizeUser")
            {
                Content = JsonContent.Create(body)
            };
            using var resp = await _http.SendAsync(req, ct);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var bodyText = await SafeReadBodyAsync(resp, ct);
                return (null, sw.Elapsed,
                    $"Authentication failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). {Truncate(bodyText, 240)}");
            }

            // Response is the token wrapped in JSON quotes: "abc123..."
            var raw = await resp.Content.ReadAsStringAsync(ct);
            var token = raw?.Trim().Trim('"');
            return string.IsNullOrWhiteSpace(token)
                ? (null, sw.Elapsed, "Authentication response did not include a token.")
                : (token, sw.Elapsed, null);
        }
        catch (TaskCanceledException tex) when (!ct.IsCancellationRequested)
        {
            return (null, sw.Elapsed, $"Timed out contacting Rent Manager at {apiBase}: {tex.Message}");
        }
        catch (HttpRequestException hex)
        {
            return (null, sw.Elapsed, $"Cannot reach {apiBase}: {hex.Message}");
        }
        catch (Exception ex)
        {
            return (null, sw.Elapsed, $"Unexpected error during auth: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Generic GET helper that injects the API token and deserializes JSON arrays.
    /// Tolerates empty/whitespace bodies (returns an empty list) so a missing-data response
    /// from RM12 doesn't blow up the whole sync pass. RM12 caps pagesize at 1000 by default,
    /// so use <see cref="GetAllPagesAsync{T}"/> to fetch all rows for a resource.</summary>
    public async Task<List<T>> GetListAsync<T>(string apiBase, string token, string path, CancellationToken ct)
    {
        var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : $"{apiBase}{path}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-RM12Api-ApiToken", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound ||
            resp.StatusCode == HttpStatusCode.NoContent) return new List<T>();

        var body = await SafeReadBodyAsync(resp, ct);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Rent Manager GET {path} failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. {Truncate(body, 240)}");

        if (string.IsNullOrWhiteSpace(body)) return new List<T>();

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
            return JsonSerializer.Deserialize<List<T>>(body, JsonOpts) ?? new List<T>();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            // Some RM12 endpoints return a single object or a paged envelope { Items: [...] }.
            // Try the envelope shape first, then fall back to a single-item list.
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("Items", out var items) && items.ValueKind == JsonValueKind.Array)
                    return JsonSerializer.Deserialize<List<T>>(items.GetRawText(), JsonOpts) ?? new List<T>();
            }
            catch { /* fall through */ }

            var single = JsonSerializer.Deserialize<T>(body, JsonOpts);
            return single is null ? new List<T>() : new List<T> { single };
        }

        return new List<T>();
    }

    /// <summary>
    /// Fetches all pages of a resource by appending <c>pagenumber</c> + <c>pagesize=1000</c>
    /// to <paramref name="basePath"/> and looping until a page returns &lt; 1000 rows or 0 rows.
    /// Pass <paramref name="basePath"/> with any existing query string (we'll add ours with the
    /// right separator).
    /// </summary>
    public async Task<List<T>> GetAllPagesAsync<T>(string apiBase, string token, string basePath,
        int maxPages, CancellationToken ct)
    {
        var sep = basePath.Contains('?') ? '&' : '?';
        var all = new List<T>();
        const int pageSize = 1000;
        for (var page = 1; page <= maxPages; page++)
        {
            var url = $"{basePath}{sep}pagenumber={page}&pagesize={pageSize}";
            var batch = await GetListAsync<T>(apiBase, token, url, ct);
            all.AddRange(batch);
            if (batch.Count < pageSize) break;   // last page
        }
        return all;
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await resp.Content.ReadAsStringAsync(ct); } catch { return string.Empty; }
    }

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");
}

// ─────────────────────────────────────────────────────────────────────────────
// Rent Manager 12 response DTOs — only the fields we map. Extra/unknown fields
// are ignored. Property names use PascalCase to match the JSON.
// ─────────────────────────────────────────────────────────────────────────────

internal class RmProperty
{
    public int PropertyID { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public List<RmAddress>? Addresses { get; set; }
    public RmAddress? PrimaryAddress { get; set; }
    public int? TotalUnits { get; set; }
}

internal class RmAddress
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? County { get; set; }
    public bool? IsPrimary { get; set; }
}

internal class RmUnit
{
    public int UnitID { get; set; }
    public int PropertyID { get; set; }
    public string? Name { get; set; }
    public string? UnitNumber { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public decimal? SquareFootage { get; set; }
    public decimal? Rent { get; set; }
    public decimal? MarketRent { get; set; }
    public bool? IsAvailable { get; set; }
    public bool? IsOccupied { get; set; }
}

internal class RmTenant
{
    public int TenantID { get; set; }
    public int? PropertyID { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
    public DateTime? PostingStartDate { get; set; }
    public decimal? Balance { get; set; }            // from embeds=Balance
    public List<RmContact>? Contacts { get; set; }   // from embeds=Contacts
    public List<RmLease>? Leases { get; set; }       // from embeds=Leases
}

/// <summary>RM12 contact record nested under Tenant via embeds=Contacts.</summary>
internal class RmContact
{
    public int ContactID { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? IsPrimary { get; set; }
    public bool? IsActive { get; set; }
    public string? Email { get; set; }
    public List<RmPhoneNumber>? PhoneNumbers { get; set; }   // populated when embeds=Contacts.PhoneNumbers
}

/// <summary>RM12 lease record. Available either embedded on Tenant (embeds=Leases)
/// or as a top-level resource at GET /Leases.</summary>
internal class RmLease
{
    public int LeaseID { get; set; }
    public int TenantID { get; set; }
    public int UnitID { get; set; }
    public int? PropertyID { get; set; }
    public DateTime? MoveInDate { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
}

internal class RmCharge
{
    public int ChargeID { get; set; }
    public int? UnitID { get; set; }
    public int? PropertyID { get; set; }
    public int? AccountID { get; set; }            // For AccountType=Customer this == TenantID
    public string? AccountType { get; set; }       // "Customer" for tenant-side activity
    public DateTime? TransactionDate { get; set; }
    public decimal? Amount { get; set; }
    public string? TransactionType { get; set; }
    public int? ChargeTypeID { get; set; }
    public string? Comment { get; set; }
    public RmChargeType? ChargeType { get; set; }  // populated when embeds=ChargeType
}

internal class RmPayment
{
    public int PaymentID { get; set; }
    public int? UnitID { get; set; }
    public int? AccountID { get; set; }
    public string? AccountType { get; set; }
    public DateTime? TransactionDate { get; set; }
    public decimal? Amount { get; set; }
    public string? Reference { get; set; }
    public string? Comment { get; set; }
}

/// <summary>RM12 phone number record. Joined to a tenant/property/owner via <c>ParentID + ParentType</c>.</summary>
internal class RmPhoneNumber
{
    public int PhoneNumberID { get; set; }
    public string? PhoneNumber { get; set; }
    public string? StrippedPhoneNumber { get; set; }
    public string? Extension { get; set; }
    public bool? IsPrimary { get; set; }
    public int? ParentID { get; set; }
    public string? ParentType { get; set; }   // "Tenant", "Property", "Owner", "Vendor", …
}

/// <summary>RM12 recurring charge — one row per (tenant or property, charge type) combination.</summary>
internal class RmRecurringCharge
{
    public int RecurringChargeID { get; set; }
    public string? EntityType { get; set; }       // "Tenant" or "Property"
    public int? EntityKeyID { get; set; }         // == TenantID when EntityType="Tenant"
    public int? TenantID { get; set; }
    public int? UnitID { get; set; }
    public int? Frequency { get; set; }            // 1 = monthly, 0 = one-time, …
    public int? ChargeTypeID { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ThroughDate { get; set; }
    public string? Comment { get; set; }
    public RmChargeType? ChargeType { get; set; }  // populated when embeds=ChargeType
}

/// <summary>RM12 charge type (e.g. "RC" = "Rent Charge", "PETFEE" = "Pet Fee").</summary>
internal class RmChargeType
{
    public int ChargeTypeID { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
