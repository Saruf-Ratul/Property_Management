using System.Net;
using FluentAssertions;

namespace PropertyManagement.IntegrationTests;

[Collection("api")]
public class PublicEndpointTests
{
    private readonly HttpClient _client;
    public PublicEndpointTests(PropertyManagementApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_is_public()
    {
        var r = await _client.GetAsync("/api/health");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/cases")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/audit-logs")]
    [InlineData("/api/lt-cases")]
    [InlineData("/api/client-portal/dashboard")]
    [InlineData("/api/properties")]
    [InlineData("/api/tenants")]
    [InlineData("/api/pms-integrations")]
    public async Task Protected_endpoints_require_authentication(string path)
    {
        var r = await _client.GetAsync(path);
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
