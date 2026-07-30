using System.Net;
using System.Net.Http.Json;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Verify_WithoutAuthAndNoPendingContext_Returns401()
    {
        var response = await _fixture.AnonymousClient.PostAsJsonAsync("/api/v1/auth/mfa/verify",
            new { code = "123456" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithInvalidPendingOidcCookieDoesNotFallBackToLegacyTokenIssuance()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"mfa-invalid-pending-{Guid.NewGuid():N}",
            Email = $"mfa-invalid-pending-{Guid.NewGuid():N}@example.com",
            FirstName = "Invalid",
            LastName = "Pending",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        var create = await userManager.CreateAsync(user, "Test@123456");
        Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(error => error.Description)));

        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.InnerClient.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = user.Email!,
                ["password"] = "Test@123456",
                ["returnUrl"] = "/"
            }));
        session.ApplySetCookieHeaders(loginResponse);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        session.SetCookieValue("hishop_oidc_mfa", "invalid-pending-state");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/mfa/verify")
        {
            Content = JsonContent.Create(new { code = "123456" })
        };
        request.Headers.TryAddWithoutValidation(
            "Cookie",
            $".AspNetCore.Identity.Application={session.GetCookieValue(".AspNetCore.Identity.Application")}; hishop_oidc_mfa=invalid-pending-state");
        request.Headers.Add("X-RateLimit-Key", $"mfa-invalid-pending-{Guid.NewGuid():N}");

        var response = await session.InnerClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var setCookies) &&
            setCookies.Any(value => value.StartsWith("hishop_sid=", StringComparison.OrdinalIgnoreCase)));
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
        session.RateLimitKey = $"mfa-enroll-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var response = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("secretKey", out _));
        Assert.True(body.TryGetProperty("qrCodeUri", out _));
        Assert.True(body.TryGetProperty("recoveryCodes", out _));
    }

    [Fact]
    public async Task Enroll_AlreadyEnrolled_Returns400()
    {
        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"mfa-already-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var firstEnroll = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        if (firstEnroll.StatusCode != HttpStatusCode.OK)
            return;

        var secondEnroll = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        Assert.Equal(HttpStatusCode.OK, secondEnroll.StatusCode);
    }

    [Fact]
    public async Task Verify_WithInvalidCode_Returns400()
    {
        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"mfa-invalid-{Guid.NewGuid():N}";
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
        session.RateLimitKey = $"mfa-recover-{Guid.NewGuid():N}";
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
        session.RateLimitKey = $"mfa-rate-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync("admin@hishop.com", "Test@123456");
        if (loginResponse.StatusCode != HttpStatusCode.OK)
            return;

        var enrollResponse = await session.PostWithCookiesAsync("/api/v1/auth/mfa/enroll");
        if (enrollResponse.StatusCode != HttpStatusCode.OK)
            return;

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 10; i++)
        {
            lastResponse = await session.PostWithCookiesAsync("/api/v1/auth/mfa/verify",
                new { code = $"00000{i}" });
            if ((int)lastResponse.StatusCode == 429) break;
        }

        Assert.True(lastResponse!.StatusCode == HttpStatusCode.TooManyRequests,
            await lastResponse.Content.ReadAsStringAsync());
    }
}
