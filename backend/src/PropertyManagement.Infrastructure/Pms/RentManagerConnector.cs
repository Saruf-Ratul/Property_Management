using System.Runtime.CompilerServices;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PropertyManagement.Infrastructure.Pms;

/// <summary>
/// Rent Manager 12 connector. Calls the real RM12 REST API at
/// <c>https://{customer}.api.rentmanager.com</c>: authenticates with
/// POST <c>/Authentication/AuthorizeUser</c> and forwards the returned token as
/// <c>X-RM12Api-ApiToken</c> on every subsequent request.
///
/// When the integration is missing credentials (no BaseUrl / Username / Password)
/// the connector returns no data. There is no fake/demo dataset — operators must
/// configure real credentials to receive any results.
///
/// <c>Pms:RentManager:Mode</c> = <c>Live</c> forces live calls even without all
/// credentials (auth will fail explicitly). <c>Mode = Mock</c> short-circuits to
/// empty results without ever contacting the API — useful for tests that don't
/// want network.
/// </summary>
public class RentManagerConnector : IRentManagerConnector
{
    private readonly ILogger<RentManagerConnector> _log;
    private readonly RentManagerApiClient _api;
    private readonly IConfiguration _config;

    public PmsProvider Provider => PmsProvider.RentManager;

    public RentManagerConnector(ILogger<RentManagerConnector> log, HttpClient http, IConfiguration config)
    {
        _log = log;
        _config = config;
        _api = new RentManagerApiClient(http);
    }

    private enum Mode { Auto, Live, Mock }

    private Mode ResolveMode(PmsConnectionContext ctx)
    {
        var configured = _config["Pms:RentManager:Mode"]?.Trim();
        if (string.Equals(configured, "Live", StringComparison.OrdinalIgnoreCase)) return Mode.Live;
        if (string.Equals(configured, "Mock", StringComparison.OrdinalIgnoreCase)) return Mode.Mock;

        var hasCreds = !string.IsNullOrWhiteSpace(ctx.BaseUrl)
                    && !string.IsNullOrWhiteSpace(ctx.Username)
                    && !string.IsNullOrWhiteSpace(ctx.Password);
        return hasCreds ? Mode.Live : Mode.Mock;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TestConnection
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PmsConnectionTestOutcome> TestConnectionAsync(PmsConnectionContext ctx, CancellationToken ct)
    {
        var mode = ResolveMode(ctx);
        if (mode == Mode.Mock) return MockTestOutcome(ctx);

        var (token, elapsed, error) = await _api.AuthorizeAsync(ctx, ct);
        if (token is null)
        {
            _log.LogWarning("Rent Manager live auth failed: {Error}", error);
            return PmsConnectionTestOutcome.Fail(error ?? "Authentication failed");
        }

        _log.LogInformation("Rent Manager live auth ok base={Base} latency={Ms}ms",
            RentManagerApiClient.NormalizeApiBaseUrl(ctx.BaseUrl), elapsed.TotalMilliseconds);
        return PmsConnectionTestOutcome.Ok(
            message: "Authenticated against Rent Manager 12",
            version: "RentManager v12",
            latency: elapsed);
    }

    private static PmsConnectionTestOutcome MockTestOutcome(PmsConnectionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.BaseUrl))
            return PmsConnectionTestOutcome.Fail("Base URL is required for Rent Manager.");
        if (string.IsNullOrWhiteSpace(ctx.Username) || string.IsNullOrWhiteSpace(ctx.Password))
            return PmsConnectionTestOutcome.Fail(
                "Username and password are required to connect to Rent Manager. " +
                "No credentials were provided.");
        return PmsConnectionTestOutcome.Fail(
            "Connector is in Mock mode (Pms:RentManager:Mode=Mock). " +
            "Set the configuration to Auto or Live to perform a real connection test.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-instance caches.
    //
    // The connector is registered scoped, so one instance backs one full sync
    // run. Caching the heavy collections (units, leases, recurring charges,
    // phones, charges, payments) means we make each /resource page-walk exactly
    // once per sync instead of once per phase or once per property.
    // ─────────────────────────────────────────────────────────────────────────

    private (string? Token, DateTime ExpiresAtUtc) _cached;
    private List<RmUnit>? _allUnits;
    private List<RmLease>? _allLeases;
    private List<RmTenant>? _allTenants;        // with embeds=Contacts,Balance
    private Dictionary<int, string>? _phonesByTenantId;
    private Dictionary<int, decimal>? _monthlyRentByTenantId;
    private List<RmCharge>? _allCharges;
    private List<RmPayment>? _allPayments;

    private async Task<string> EnsureTokenAsync(PmsConnectionContext ctx, CancellationToken ct)
    {
        if (_cached.Token is not null && DateTime.UtcNow < _cached.ExpiresAtUtc) return _cached.Token;
        var (token, _, error) = await _api.AuthorizeAsync(ctx, ct);
        if (token is null) throw new InvalidOperationException(error ?? "Rent Manager auth failed");
        _cached = (token, DateTime.UtcNow.AddMinutes(20));
        return token;
    }

    private async Task<List<RmUnit>> EnsureAllUnitsAsync(string apiBase, string token, CancellationToken ct)
        => _allUnits ??= await _api.GetAllPagesAsync<RmUnit>(apiBase, token, "/Units", maxPages: 50, ct);

    private async Task<List<RmLease>> EnsureAllLeasesAsync(string apiBase, string token, CancellationToken ct)
        => _allLeases ??= await _api.GetAllPagesAsync<RmLease>(apiBase, token, "/Leases", maxPages: 100, ct);

    private async Task<List<RmTenant>> EnsureAllTenantsAsync(string apiBase, string token, CancellationToken ct)
        => _allTenants ??= await TryGetAllPagesAsync<RmTenant>(apiBase, token,
            "/Tenants?embeds=Contacts.PhoneNumbers,Balance",
            "/Tenants?embeds=Contacts,Balance", ct);

    /// <summary>
    /// Builds a TenantID → primary phone map. Phones in RM12 belong to <c>Contact</c>
    /// records, and Contacts hang off Tenants. We prefer the nested embed
    /// <c>?embeds=Contacts.PhoneNumbers</c> so phones come back in one call. If that
    /// returns nothing (some RM configs strip it) we fall back to a global
    /// <c>/PhoneNumbers</c> walk + an in-memory join through Contact.ParentID.
    /// </summary>
    private async Task<Dictionary<int, string>> EnsurePhonesByTenantAsync(
        string apiBase, string token, CancellationToken ct)
    {
        if (_phonesByTenantId is not null) return _phonesByTenantId;

        var byTenant = new Dictionary<int, string>();

        // Path A: phones are already inline on Tenant.Contacts[].PhoneNumbers[].
        var tenants = await EnsureAllTenantsAsync(apiBase, token, ct);
        foreach (var t in tenants)
        {
            var phone = t.Contacts?
                .Where(c => c.PhoneNumbers != null && c.PhoneNumbers.Count > 0)
                .OrderByDescending(c => c.IsPrimary == true)
                .SelectMany(c => c.PhoneNumbers!)
                .OrderByDescending(p => p.IsPrimary == true)
                .Select(p => p.PhoneNumber)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            if (!string.IsNullOrWhiteSpace(phone))
                byTenant[t.TenantID] = phone!;
        }

        // Path B (fallback): if the nested embed returned nothing for any tenant,
        // pull the global /PhoneNumbers list and join through Contact.ParentID.
        if (byTenant.Count == 0)
        {
            var allPhones = await _api.GetAllPagesAsync<RmPhoneNumber>(
                apiBase, token, "/PhoneNumbers", maxPages: 100, ct);

            // Build ContactID → TenantID using each Tenant's embedded Contacts list.
            var contactToTenant = new Dictionary<int, int>();
            foreach (var t in tenants)
            {
                if (t.Contacts is null) continue;
                foreach (var c in t.Contacts)
                    contactToTenant[c.ContactID] = t.TenantID;
            }

            foreach (var grp in allPhones
                .Where(p => string.Equals(p.ParentType, "Contact", StringComparison.OrdinalIgnoreCase)
                         && p.ParentID.HasValue
                         && !string.IsNullOrWhiteSpace(p.PhoneNumber))
                .GroupBy(p => p.ParentID!.Value))
            {
                if (!contactToTenant.TryGetValue(grp.Key, out var tenantId)) continue;
                var primary = grp.FirstOrDefault(p => p.IsPrimary == true) ?? grp.First();
                if (!byTenant.ContainsKey(tenantId))
                    byTenant[tenantId] = primary.PhoneNumber!;
            }
        }

        _phonesByTenantId = byTenant;
        return byTenant;
    }

    /// <summary>
    /// Builds a TenantID → monthly rent map from <c>/RecurringCharges</c>. RM stores
    /// rent as a recurring charge whose <c>ChargeType.Name == "RC"</c> (Rent Charge).
    /// We sum amounts per tenant in case there are multiple "RC" rows. We only count
    /// recurring charges with <c>Frequency = 1</c> (monthly) and a TenantID set.
    /// </summary>
    private async Task<Dictionary<int, decimal>> EnsureMonthlyRentByTenantAsync(
        string apiBase, string token, CancellationToken ct)
    {
        if (_monthlyRentByTenantId is not null) return _monthlyRentByTenantId;

        // Tenant-scoped recurring charges with their charge-type name embedded.
        var recurring = await TryGetAllPagesAsync<RmRecurringCharge>(apiBase, token,
            "/RecurringCharges?embeds=ChargeType&filters=EntityType,eq,Tenant",
            "/RecurringCharges?embeds=ChargeType", ct);

        var byTenant = new Dictionary<int, decimal>();
        foreach (var rc in recurring)
        {
            if (!string.Equals(rc.EntityType, "Tenant", StringComparison.OrdinalIgnoreCase)) continue;
            if (rc.Frequency != 1) continue;
            if (rc.Amount is null || rc.Amount.Value <= 0) continue;
            var tenantId = rc.TenantID ?? rc.EntityKeyID;
            if (tenantId is null) continue;

            // Match RM's standard rent code "RC" first, fall back to "Rent Charge" by description.
            var name = rc.ChargeType?.Name?.Trim();
            var desc = rc.ChargeType?.Description?.Trim() ?? string.Empty;
            var isRent = string.Equals(name, "RC", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(desc, "Rent Charge", StringComparison.OrdinalIgnoreCase)
                      || (desc.StartsWith("Rent", StringComparison.OrdinalIgnoreCase)
                          && !desc.Contains("Pet", StringComparison.OrdinalIgnoreCase)
                          && !desc.Contains("Late", StringComparison.OrdinalIgnoreCase));
            if (!isRent) continue;

            byTenant[tenantId.Value] = byTenant.TryGetValue(tenantId.Value, out var existing)
                ? existing + rc.Amount.Value
                : rc.Amount.Value;
        }

        _monthlyRentByTenantId = byTenant;
        return byTenant;
    }

    private async Task<List<RmCharge>> EnsureAllChargesAsync(string apiBase, string token, CancellationToken ct)
        => _allCharges ??= await _api.GetAllPagesAsync<RmCharge>(apiBase, token, "/Charges", maxPages: 200, ct);

    private async Task<List<RmPayment>> EnsureAllPaymentsAsync(string apiBase, string token, CancellationToken ct)
        => _allPayments ??= await _api.GetAllPagesAsync<RmPayment>(apiBase, token, "/Payments", maxPages: 200, ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Properties
    // ─────────────────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<PmsPropertyDto> GetPropertiesAsync(
        PmsConnectionContext ctx, [EnumeratorCancellation] CancellationToken ct)
    {
        if (ResolveMode(ctx) == Mode.Mock) yield break;

        var token = await EnsureTokenAsync(ctx, ct);
        var apiBase = RentManagerApiClient.NormalizeApiBaseUrl(ctx.BaseUrl);
        var props = await TryGetAllPagesAsync<RmProperty>(apiBase, token,
            "/Properties?embeds=Addresses,PrimaryAddress",
            "/Properties", ct);

        // /Properties doesn't return TotalUnits in their config, so we derive the count
        // by grouping the (already cached) Units list by PropertyID. This adds one extra
        // /Units page-walk to the properties phase but only on the first call per sync.
        var units = await EnsureAllUnitsAsync(apiBase, token, ct);
        var unitCountByProperty = units
            .GroupBy(u => u.PropertyID)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var p in props)
        {
            var addr = p.PrimaryAddress
                       ?? p.Addresses?.FirstOrDefault(a => a.IsPrimary == true)
                       ?? p.Addresses?.FirstOrDefault();
            yield return new PmsPropertyDto
            {
                ExternalId = p.PropertyID.ToString(),
                Name = p.Name ?? p.ShortName ?? $"Property {p.PropertyID}",
                AddressLine1 = addr?.Street,
                City = addr?.City,
                State = addr?.State,
                PostalCode = addr?.PostalCode,
                County = addr?.County,
                UnitCount = p.TotalUnits ?? (unitCountByProperty.TryGetValue(p.PropertyID, out var cnt) ? cnt : 0)
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Units
    // ─────────────────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<PmsUnitDto> GetUnitsAsync(
        PmsConnectionContext ctx, string propertyExternalId, [EnumeratorCancellation] CancellationToken ct)
    {
        if (ResolveMode(ctx) == Mode.Mock) yield break;
        if (!int.TryParse(propertyExternalId, out var propId)) yield break;
        var token = await EnsureTokenAsync(ctx, ct);
        var apiBase = RentManagerApiClient.NormalizeApiBaseUrl(ctx.BaseUrl);

        // Units list is fetched once per sync and reused across property iterations.
        var allUnits = await EnsureAllUnitsAsync(apiBase, token, ct);
        var units = allUnits.Where(u => u.PropertyID == propId).ToList();

        foreach (var u in units)
        {
            yield return new PmsUnitDto
            {
                ExternalId = u.UnitID.ToString(),
                PropertyExternalId = u.PropertyID.ToString(),
                UnitNumber = u.UnitNumber ?? u.Name ?? $"Unit {u.UnitID}",
                Bedrooms = u.Bedrooms,
                Bathrooms = u.Bathrooms.HasValue ? (int?)Math.Round(u.Bathrooms.Value) : null,
                SquareFeet = u.SquareFootage,
                MarketRent = u.MarketRent ?? u.Rent,
                IsOccupied = u.IsOccupied ?? (u.IsAvailable == false)
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tenants — RM12 keeps email + phone in nested Contacts; balance comes from
    // a computed embed. Phone is on a separate /TenantPhoneNumbers endpoint and
    // is not exposed via Tenant embeds in many configurations, so we leave it
    // null in live mode for Phase 0.
    // ─────────────────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<PmsTenantDto> GetTenantsAsync(
        PmsConnectionContext ctx, [EnumeratorCancellation] CancellationToken ct)
    {
        if (ResolveMode(ctx) == Mode.Mock) yield break;

        var token = await EnsureTokenAsync(ctx, ct);
        var apiBase = RentManagerApiClient.NormalizeApiBaseUrl(ctx.BaseUrl);
        var tenants = await EnsureAllTenantsAsync(apiBase, token, ct);
        var phonesByTenant = await EnsurePhonesByTenantAsync(apiBase, token, ct);

        foreach (var t in tenants)
        {
            var primary = t.Contacts?.FirstOrDefault(c => c.IsPrimary == true)
                       ?? t.Contacts?.FirstOrDefault();
            phonesByTenant.TryGetValue(t.TenantID, out var phone);
            yield return new PmsTenantDto
            {
                ExternalId = t.TenantID.ToString(),
                FirstName = t.FirstName ?? primary?.FirstName ?? FirstWord(t.Name) ?? "(unknown)",
                LastName = t.LastName ?? primary?.LastName ?? RestOfWords(t.Name) ?? string.Empty,
                Email = primary?.Email,
                Phone = phone,
                IsActive = !string.Equals(t.Status, "Past", StringComparison.OrdinalIgnoreCase)
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Leases — RM12 has a top-level /Leases endpoint with TenantID + UnitID.
    // Lease end-date / monthly rent are not on the Lease record itself; for now
    // we read CurrentBalance from the parent Tenant (one /Tenants?embeds=Balance
    // call) and attach it to every active lease for that tenant.
    // ─────────────────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<PmsLeaseDto> GetLeasesAsync(
        PmsConnectionContext ctx, [EnumeratorCancellation] CancellationToken ct)
    {
        if (ResolveMode(ctx) == Mode.Mock) yield break;

        var token = await EnsureTokenAsync(ctx, ct);
        var apiBase = RentManagerApiClient.NormalizeApiBaseUrl(ctx.BaseUrl);

        var leases = await EnsureAllLeasesAsync(apiBase, token, ct);
        var tenants = await EnsureAllTenantsAsync(apiBase, token, ct);
        var rentByTenant = await EnsureMonthlyRentByTenantAsync(apiBase, token, ct);
        var tenantById = tenants.ToDictionary(t => t.TenantID, t => t);

        _log.LogInformation("Rent Manager: monthly rent resolved for {Count} tenants", rentByTenant.Count);

        foreach (var l in leases)
        {
            tenantById.TryGetValue(l.TenantID, out var t);
            rentByTenant.TryGetValue(l.TenantID, out var rent);
            var startDate = l.MoveInDate ?? l.ArrivalDate ?? t?.PostingStartDate ?? DateTime.UtcNow.Date;
            var endDate = l.MoveOutDate;
            var isActive = endDate is null
                        && !string.Equals(t?.Status, "Past", StringComparison.OrdinalIgnoreCase);
            yield return new PmsLeaseDto
            {
                ExternalId = $"L{l.LeaseID}",
                UnitExternalId = l.UnitID.ToString(),
                TenantExternalId = l.TenantID.ToString(),
                StartDate = startDate,
                EndDate = endDate,
                IsMonthToMonth = endDate is null,
                MonthlyRent = rent,                          // from /RecurringCharges (RC type)
                IsActive = isActive,
                CurrentBalance = t?.Balance ?? 0m
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ledger — RM12 stores Charges and Payments separately. We merge them into
    // a single chronological ledger and compute a running balance.
    // ─────────────────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<PmsLedgerItemDto> GetLedgerAsync(
        PmsConnectionContext ctx, string leaseExternalId, [EnumeratorCancellation] CancellationToken ct)
    {
        if (ResolveMode(ctx) == Mode.Mock) yield break;

        // RM12 transactions reference a tenant via AccountID + AccountType="Customer".
        // For Customer accounts, AccountID == TenantID. We resolve TenantID from the
        // lease (cached) once and then filter the cached charges/payments lists by
        // (AccountID == TenantID, AccountType == Customer).
        if (!leaseExternalId.StartsWith('L') ||
            !int.TryParse(leaseExternalId[1..], out var leaseId))
            yield break;

        var token = await EnsureTokenAsync(ctx, ct);
        var apiBase = RentManagerApiClient.NormalizeApiBaseUrl(ctx.BaseUrl);

        var leases = await EnsureAllLeasesAsync(apiBase, token, ct);
        var lease = leases.FirstOrDefault(x => x.LeaseID == leaseId);
        if (lease is null) yield break;
        var tenantId = lease.TenantID;

        var charges = await EnsureAllChargesAsync(apiBase, token, ct);
        var payments = await EnsureAllPaymentsAsync(apiBase, token, ct);

        bool IsCustomerAcct(string? t) =>
            string.IsNullOrEmpty(t) || string.Equals(t, "Customer", StringComparison.OrdinalIgnoreCase);

        var combined = new List<(DateTime Date, decimal Amount, string Category, string? Desc, string ExtId, bool IsCharge)>();
        foreach (var c in charges.Where(c => c.AccountID == tenantId && IsCustomerAcct(c.AccountType)))
            combined.Add((
                Date: c.TransactionDate ?? DateTime.UtcNow,
                Amount: c.Amount ?? 0m,
                Category: c.ChargeType?.Name ?? c.TransactionType ?? "Charge",
                Desc: c.Comment,
                ExtId: $"C{c.ChargeID}",
                IsCharge: true));
        foreach (var p in payments.Where(p => p.AccountID == tenantId && IsCustomerAcct(p.AccountType)))
            combined.Add((
                Date: p.TransactionDate ?? DateTime.UtcNow,
                Amount: -Math.Abs(p.Amount ?? 0m),
                Category: "Payment",
                Desc: p.Comment ?? p.Reference,
                ExtId: $"P{p.PaymentID}",
                IsCharge: false));

        decimal running = 0m;
        foreach (var item in combined.OrderBy(x => x.Date))
        {
            running += item.Amount;
            yield return new PmsLedgerItemDto
            {
                ExternalId = item.ExtId,
                LeaseExternalId = leaseExternalId,
                PostedDate = item.Date,
                Category = item.Category,
                Description = item.Desc,
                Amount = item.Amount,
                Balance = running,
                IsCharge = item.IsCharge,
                IsPayment = !item.IsCharge
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Issues a GET against <paramref name="primaryPath"/>; if it fails we log and retry the
    /// simpler <paramref name="fallbackPath"/>. Used to handle the variations in RM12's
    /// <c>embeds=</c> / <c>filters=</c> support across customers' configurations.
    /// </summary>
    private async Task<List<T>> TryGetListAsync<T>(string apiBase, string token,
        string primaryPath, string fallbackPath, CancellationToken ct)
    {
        try { return await _api.GetListAsync<T>(apiBase, token, primaryPath, ct); }
        catch (Exception ex)
        {
            _log.LogWarning("Rent Manager primary path {Path} failed: {Msg} — falling back to {Fallback}",
                primaryPath, ex.Message, fallbackPath);
            return await _api.GetListAsync<T>(apiBase, token, fallbackPath, ct);
        }
    }

    private async Task<List<T>> TryGetAllPagesAsync<T>(string apiBase, string token,
        string primaryPath, string fallbackPath, CancellationToken ct, int maxPages = 50)
    {
        try { return await _api.GetAllPagesAsync<T>(apiBase, token, primaryPath, maxPages, ct); }
        catch (Exception ex)
        {
            _log.LogWarning("Rent Manager primary path {Path} failed: {Msg} — falling back to {Fallback}",
                primaryPath, ex.Message, fallbackPath);
            return await _api.GetAllPagesAsync<T>(apiBase, token, fallbackPath, maxPages, ct);
        }
    }

    private static string? FirstWord(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var i = s.IndexOf(' ');
        return i < 0 ? s : s[..i];
    }

    private static string? RestOfWords(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var i = s.IndexOf(' ');
        return i < 0 ? null : s[(i + 1)..];
    }

}
