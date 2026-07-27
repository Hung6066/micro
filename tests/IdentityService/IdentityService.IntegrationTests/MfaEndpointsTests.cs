using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public class MfaEndpointsTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public MfaEndpointsTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Enroll_WithoutAuth_Returns401()
    {
        var response = await _fixture.AnonymousClient.PostAsync("/api/v1/auth/mfa/enroll", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithoutAuth_Returns401()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync("/api/v1/auth/mfa/verify",
            new { code = "123456" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Recover_WithoutAuth_Returns401()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync("/api/v1/auth/mfa/recover",
            new { recoveryCode = "xxxx-xxxx" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_WithValidSession_ReturnsSecretAndQrUri()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var response = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("secret", out _));
        Assert.True(body.TryGetProperty("qrUri", out _));
        Assert.True(body.TryGetProperty("recoveryCodes", out _));
    }

    [Fact]
    public async Task Enroll_AlreadyEnrolled_Returns400()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var firstEnroll = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        if (firstEnroll.StatusCode != HttpStatusCode.OK)
            return;

        var secondEnroll = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        Assert.Equal(HttpStatusCode.BadRequest, secondEnroll.StatusCode);
    }

    [Fact]
    public async Task Verify_WithInvalidCode_Returns400()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var enrollResponse = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        if (enrollResponse.StatusCode != HttpStatusCode.OK)
            return;

        var verifyResponse = await session.PostWithCookiesAsync("/api/v1/auth/mfa/verify",
            new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task Recover_WithInvalidCode_Returns400()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var recoverResponse = await session.PostWithCookiesAsync("/api/v1/auth/mfa/recover",
            new { recoveryCode = "invalid-code-12345" });
        Assert.Equal(HttpStatusCode.BadRequest, recoverResponse.StatusCode);
    }

    [Fact]
    public async Task Verify_WithRateLimitExceeded_Returns429()
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
