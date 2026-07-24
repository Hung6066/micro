using System.Net;
using System.Text.Json;
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
    public async Task JwksEndpoint_ReturnsPublicRs256SigningKeys()
    {
        var response = await _client.GetAsync("/.well-known/jwks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = document.RootElement.GetProperty("keys").EnumerateArray().ToArray();

        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            Assert.Equal("RSA", key.GetProperty("kty").GetString());
            Assert.Equal("RS256", key.GetProperty("alg").GetString());
            Assert.Equal("sig", key.GetProperty("use").GetString());
            Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("kid").GetString()));
            Assert.True(key.TryGetProperty("n", out _), "RSA modulus must be published.");
            Assert.True(key.TryGetProperty("e", out _), "RSA exponent must be published.");

            Assert.False(key.TryGetProperty("d", out _), "JWKS must not expose the private exponent.");
            Assert.False(key.TryGetProperty("p", out _), "JWKS must not expose the first prime factor.");
            Assert.False(key.TryGetProperty("q", out _), "JWKS must not expose the second prime factor.");
            Assert.False(key.TryGetProperty("dp", out _), "JWKS must not expose private CRT parameters.");
            Assert.False(key.TryGetProperty("dq", out _), "JWKS must not expose private CRT parameters.");
            Assert.False(key.TryGetProperty("qi", out _), "JWKS must not expose private CRT parameters.");
            Assert.False(key.TryGetProperty("k", out _), "JWKS must not expose symmetric signing material.");
        }
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
