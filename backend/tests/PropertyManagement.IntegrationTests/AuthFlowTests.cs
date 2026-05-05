using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit.Abstractions;

namespace PropertyManagement.IntegrationTests;

[Collection("api")]
public class AuthFlowTests
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _out;
    public AuthFlowTests(PropertyManagementApiFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _out = output;
    }

    [Fact]
    public async Task Login_with_seeded_admin_returns_jwt_and_grants_access_to_protected_endpoints()
    {
        // Arrange: log in as the seeded FirmAdmin.
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@pm.local", password = "Admin!2345" });
        var loginBody = await loginResp.Content.ReadAsStringAsync();
        _out.WriteLine($"Login: {(int)loginResp.StatusCode} → {loginBody.Substring(0, Math.Min(120, loginBody.Length))}…");
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK, "login should succeed for the seeded admin");
        var auth = JsonSerializer.Deserialize<JsonElement>(loginBody);

        var token = auth.GetProperty("accessToken").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        // Act: hit /me and /cases with the token.
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await _client.GetAsync("/api/auth/me");
        var cases = await _client.GetAsync("/api/cases");

        // Assert
        var meBody = await me.Content.ReadAsStringAsync();
        _out.WriteLine($"/me: {(int)me.StatusCode} → {meBody.Substring(0, Math.Min(200, meBody.Length))}");
        _out.WriteLine($"/cases: {(int)cases.StatusCode}");

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        cases.StatusCode.Should().Be(HttpStatusCode.OK);

        var meJson = JsonSerializer.Deserialize<JsonElement>(meBody);
        meJson.GetProperty("email").GetString().Should().Be("admin@pm.local");
        meJson.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).Should().Contain("FirmAdmin");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@pm.local", password = "Wrong-Password-123" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClientUser_cannot_access_firm_endpoints()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "client@acme.local", password = "Admin!2345" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        var c = _client;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await c.GetAsync("/api/clients")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.GetAsync("/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await c.GetAsync("/api/pms-integrations")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // …but the client portal endpoints are accessible.
        (await c.GetAsync("/api/client-portal/dashboard")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FirmAdmin_cannot_access_client_portal_endpoints()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@pm.local", password = "Admin!2345" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        var c = _client;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await c.GetAsync("/api/client-portal/dashboard")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
