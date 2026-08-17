using System.Net;
using His.Hope.Contracts.Identity;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

 [Collection("IdentityServiceIntegration")]
public class SecurityTests
{
    private readonly HttpClient _client;

    public SecurityTests(IdentityServiceTestFixture fixture)
    {
        _client = fixture.CreateSessionClient().InnerClient;
    }

    [Fact]
    public async Task AuthorizeEndpoint_RejectsWithoutPkce()
    {
        var response = await _client.GetAsync(
            $"{IdentityApiRoutes.OidcAuthorize}?client_id=his-hope-spa&redirect_uri=https://localhost/callback&response_type=code&scope=openid&state=test");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LegacyEndpoints_HaveDeprecationHeaders()
    {
        var response = await _client.PostAsync(IdentityApiRoutes.Login,
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

        var response = await _client.PostAsync(IdentityApiRoutes.OidcIntrospect, content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest && body.Contains("invalid_request")
            || body.Contains("\"active\":false"));
    }

}
