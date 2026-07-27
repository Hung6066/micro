using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public class AuthEndpointsTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public AuthEndpointsTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSessionCookies()
    {
        var session = _fixture.CreateSessionClient();
        var response = await session.LoginAsync("admin@hishop.test", "Test@123456");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());

        Assert.NotNull(session.GetCookieValue("hishop_sid"));
        Assert.NotNull(session.GetCookieValue("hishop_csrf"));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        var session = _fixture.CreateSessionClient();
        var response = await session.LoginAsync("wrong@email.test", "WrongPass1!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithRateLimitExceeded_Returns429()
    {
        var client = _fixture.AnonymousClient;
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 130; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = $"rate-test{i}@test.test", password = "Test@123456" });
            if ((int)lastResponse.StatusCode == 429) break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidSession_ReturnsNewTokens()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.test", "Test@123456");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var refreshResponse = await session.PostWithCookiesAsync("/api/v1/auth/refresh", new
        {
            accessToken = "",
            refreshToken = ""
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        session.ApplySetCookieHeaders(refreshResponse);

        Assert.NotNull(session.GetCookieValue("hishop_sid"));
        Assert.NotNull(session.GetCookieValue("hishop_csrf"));
    }

    [Fact]
    public async Task Refresh_WithoutCsrfToken_Returns403()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.test", "Test@123456");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { accessToken = "", refreshToken = "" })
        };
        foreach (var cookie in new[] { "hishop_sid" })
        {
            var val = session.GetCookieValue(cookie);
            if (val is not null)
                request.Headers.TryAddWithoutValidation("Cookie", $"{cookie}={val}");
        }

        var response = await session.InnerClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithExpiredSession_Returns401()
    {
        var client = _fixture.AnonymousClient;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { accessToken = "expired", refreshToken = "expired" })
        };
        request.Headers.TryAddWithoutValidation("Cookie", "hishop_sid=nonexistent");
        request.Headers.Add("X-CSRF-Token", "fake-token");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InternalRefresh_WithoutSession_Returns400()
    {
        var response = await _fixture.AnonymousClient.PostAsync("/api/v1/auth/internal/refresh", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InternalRefresh_WithExpiredSessionCookie_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/internal/refresh");
        request.Headers.TryAddWithoutValidation("Cookie", "hishop_sid=invalid-session-id");

        var response = await _fixture.AnonymousClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSessionAndRevokesTokens()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.test", "Test@123456");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var logoutResponse = await session.PostWithCookiesAsync("/api/v1/auth/logout");
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        Assert.Contains(logoutResponse.Headers,
            h => h.Key == "Set-Cookie" && h.Value.Any(v => v.Contains("hishop_sid=")));
    }

    [Fact]
    public async Task Me_WithoutAuth_Returns401()
    {
        var response = await _fixture.AnonymousClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidSession_ReturnsUser()
    {
        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.LoginAsync("admin@hishop.test", "Test@123456");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var meResponse = await session.GetWithCookiesAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var body = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Logout_WithoutActiveSession_ReturnsNoContent()
    {
        var response = await _fixture.AnonymousClient.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Verify_ReturnsAuthenticationStatus()
    {
        var response = await _fixture.AnonymousClient.GetAsync("/api/v1/auth/verify");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("authenticated").GetBoolean());
    }
}
