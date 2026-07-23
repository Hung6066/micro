using System.Net;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class OidcFlowTests
{
    private readonly HttpClient _client;

    public OidcFlowTests()
    {
        // In CI, IdentityService would be running as a Testcontainer
        // For now: test against local running instance
        _client = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
    }

    [Fact]
    public async Task DiscoveryEndpoint_ReturnsValidOidcConfiguration()
    {
        var response = await _client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task JwksEndpoint_ReturnsKeys()
    {
        var response = await _client.GetAsync("/.well-known/jwks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizeEndpoint_RequiresClientId()
    {
        var response = await _client.GetAsync(
            "/connect/authorize?redirect_uri=https://localhost/callback&response_type=code&scope=openid");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenEndpoint_RejectsInvalidCode()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = "invalid",
            ["client_id"] = "his-hope-spa",
            ["redirect_uri"] = "https://localhost/callback",
            ["code_verifier"] = "test"
        });

        var response = await _client.PostAsync("/connect/token", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
