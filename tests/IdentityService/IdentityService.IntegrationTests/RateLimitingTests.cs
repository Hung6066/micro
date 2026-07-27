using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public class RateLimitingTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public RateLimitingTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AuthEndpoint_RateLimitExceeded_Returns429()
    {
        var client = _fixture.AnonymousClient;
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 130; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = $"rl-test-{i}@test.test", password = $"TestPass{i}!" });
            if ((int)lastResponse.StatusCode == 429) break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task ScimEndpoint_RateLimitExceeded_Returns429()
    {
        var client = _fixture.AnonymousClient;
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 70; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/scim/v2/Users",
                new { schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" }, userName = $"scim-rl-{i}@test.test" });
            if ((int)lastResponse.StatusCode == 429) break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task MfaEndpoint_RateLimitExceeded_Returns429()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 10; i++)
        {
            lastResponse = await session.PostWithCookiesAsync("/api/v1/auth/mfa/verify",
                new { code = $"00000{i}" });
            if ((int)lastResponse.StatusCode == 429) break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
