using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

 [Collection("IdentityServiceIntegration")]
public class FederationTests
{
    private readonly HttpClient _client;

    public FederationTests(IdentityServiceTestFixture fixture)
    {
        _client = fixture.AnonymousClient;
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

    [Fact]
    public async Task LoginPage_UsesCspCompatiblePasskeyScriptAndHidesUnavailableSaml()
    {
        var response = await _client.GetAsync("/Account/Login?returnUrl=%2Fconnect%2Fauthorize");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/auth/identity-login.js", body, StringComparison.Ordinal);
        Assert.DoesNotContain("addEventListener('click'", body, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/federation/saml/login", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasskeyRegistrationOptions_RequiresAuth()
    {
        var response = await _client.PostAsync("/api/v1/auth/passkeys/register/options", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LdapLogin_IsFailClosedWhenFederationIsNotConfigured()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/ldap/login",
            new { userName = "unknown", password = "invalid" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SamlLogin_IsNotAvailableWithoutIdpMetadata()
    {
        var response = await _client.GetAsync("/api/v1/federation/saml/login");
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Unauthorized });
    }
}
