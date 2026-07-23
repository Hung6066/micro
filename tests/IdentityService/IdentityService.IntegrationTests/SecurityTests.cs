using System.Net;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class SecurityTests
{
    private readonly HttpClient _client;

    public SecurityTests()
    {
        _client = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
    }

    [Fact]
    public async Task AuthorizeEndpoint_RejectsWithoutPkce()
    {
        var response = await _client.GetAsync(
            "/connect/authorize?client_id=his-hope-spa&redirect_uri=https://localhost/callback&response_type=code&scope=openid&state=test");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyEndpoints_HaveDeprecationHeaders()
    {
        var response = await _client.PostAsync("/api/v1/auth/login", 
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.Headers.Contains("Deprecation"));
    }

    [Fact]
    public async Task IntrospectionEndpoint_RejectsEmptyToken()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = "",
            ["token_type_hint"] = "access_token"
        });

        var response = await _client.PostAsync("/connect/introspect", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"active\":false", body);
    }
}
