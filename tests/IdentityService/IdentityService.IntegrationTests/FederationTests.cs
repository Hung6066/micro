using System.Net;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class FederationTests
{
    private readonly HttpClient _client;

    public FederationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
    }

    [Fact]
    public async Task ExternalProvidersEndpoint_ReturnsProviders()
    {
        var response = await _client.GetAsync("/api/v1/auth/external-providers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("providers", body);
    }

    [Fact]
    public async Task ExternalLogin_Challenge_RedirectsToProvider()
    {
        var response = await _client.GetAsync("/api/v1/auth/external-login/Google");
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.Found ||
            response.StatusCode == HttpStatusCode.RedirectMethod);
    }

    [Fact]
    public async Task LinkedAccounts_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/v1/auth/account/linked-accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
