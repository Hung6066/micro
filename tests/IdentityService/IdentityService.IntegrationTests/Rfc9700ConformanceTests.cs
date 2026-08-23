using System.Net;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>
/// RFC 9700 OAuth 2.0 Security BCP conformance matrix exercised against the
/// Identity Service test host. Public-ingress replay is tracked separately via
/// artifacts/security/oidc-conformance/report.json.
/// </summary>
[Collection("IdentityServiceIntegration")]
public sealed class Rfc9700ConformanceTests
{
    private readonly HttpClient _client;

    public Rfc9700ConformanceTests(IdentityServiceTestFixture fixture) =>
        _client = fixture.AnonymousClient;

    [Fact]
    public async Task Rfc9700_4_1_AuthorizationCode_RequiresPkce()
    {
        var response = await _client.GetAsync(
            $"{IdentityApiRoutes.OidcAuthorize}?client_id=his-hope-spa&redirect_uri=https://localhost/callback&response_type=code&scope=openid&state=test-state");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rfc9700_4_1_AuthorizationCode_RejectsPlainPkceChallenge()
    {
        var response = await _client.GetAsync(
            $"{IdentityApiRoutes.OidcAuthorize}?client_id=his-hope-spa&redirect_uri=https://localhost/callback&response_type=code&scope=openid&state=test-state&code_challenge=plain-challenge&code_challenge_method=plain");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rfc9700_4_1_1_ExactRedirectUri_RejectsUnknownRedirect()
    {
        var verifier = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var response = await _client.GetAsync(
            $"{IdentityApiRoutes.OidcAuthorize}?client_id=his-hope-spa&redirect_uri=https://evil.example/callback&response_type=code&scope=openid&state=test-state&code_challenge={challenge}&code_challenge_method=S256");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rfc9700_4_2_Discovery_PublishesRequiredEndpoints()
    {
        var response = await _client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        foreach (var property in new[] { "issuer", "authorization_endpoint", "token_endpoint", "jwks_uri", "response_types_supported" })
            Assert.True(root.TryGetProperty(property, out _), $"Missing discovery field: {property}");
    }

    [Fact]
    public async Task Rfc9700_4_3_TokenEndpoint_RejectsMissingGrantType()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "his-hope-spa"
        });
        var response = await _client.PostAsync(IdentityApiRoutes.OidcToken, content);
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rfc9700_4_4_RefreshToken_RejectsEmptyToken()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = "",
            ["client_id"] = "his-hope-spa"
        });
        var response = await _client.PostAsync(IdentityApiRoutes.OidcToken, content);
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rfc9700_4_5_Introspection_RejectsInactiveToken()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = "not-a-valid-token",
            ["token_type_hint"] = "access_token"
        });
        var response = await _client.PostAsync(IdentityApiRoutes.OidcIntrospect, content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            body.Contains("\"active\":false", StringComparison.OrdinalIgnoreCase) ||
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rfc9700_4_6_Revocation_AcceptsEmptyTokenSafely()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = "",
            ["token_type_hint"] = "access_token"
        });
        var response = await _client.PostAsync(IdentityApiRoutes.OidcRevoke, content);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rfc9700_4_7_LegacyPasswordGrant_NotAdvertised()
    {
        var response = await _client.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var grants = document.RootElement.GetProperty("grant_types_supported").EnumerateArray()
            .Select(element => element.GetString()).ToArray();
        Assert.DoesNotContain("password", grants);
        Assert.DoesNotContain("implicit", grants);
    }
}
