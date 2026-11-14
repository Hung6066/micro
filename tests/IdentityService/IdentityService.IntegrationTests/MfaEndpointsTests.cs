using System.Net;
using System.Net.Http.Json;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using His.Hope.Contracts.Identity;
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
        var response = await _fixture.AnonymousClient.PostAsync(IdentityApiRoutes.MfaEnroll, null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Verify_WithoutAuthAndNoPendingContext_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.MfaVerify)
        {
            Content = JsonContent.Create(new { code = "123456" })
        };
        request.Headers.Add("X-RateLimit-Key", $"mfa-anonymous-{Guid.NewGuid():N}");
        var response = await _fixture.AnonymousClient.SendAsync(request);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests });
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
        var create = await userManager.CreateAsync(user, IdentityTestCredentials.Password);
        Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(error => error.Description)));

        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.InnerClient.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = user.Email!,
                ["password"] = IdentityTestCredentials.Password,
                ["returnUrl"] = "/"
            }));
        session.ApplySetCookieHeaders(loginResponse);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        session.SetCookieValue("hishop_oidc_mfa", "invalid-pending-state");

        using var request = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.MfaVerify)
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
        var response = await _fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.MfaRecover,
            new { recoveryCode = "xxxx-xxxx" });
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests });
    }

    [Fact]
    public async Task Enroll_WithValidSession_ReturnsSecretAndQrUri()
    {
        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"mfa-enroll-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.Mfa + "/enroll");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("secretKey", out _));
        Assert.True(body.TryGetProperty("qrCodeUri", out _));
        Assert.True(body.TryGetProperty("recoveryCodes", out _));
    }

    [Fact]
    public async Task Enroll_ExistingPendingEnrollment_ReplacesSecret()
    {
        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"mfa-already-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var firstEnroll = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaEnroll);
        Assert.Equal(HttpStatusCode.OK, firstEnroll.StatusCode);

        var secondEnroll = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaEnroll);
        Assert.Equal(HttpStatusCode.OK, secondEnroll.StatusCode);
    }

    [Fact]
    public async Task Verify_WithInvalidCode_Returns400()
    {
        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"mfa-invalid-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var enrollResponse = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaEnroll);
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);

        var verifyResponse = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaVerify,
            new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);
    }

    [Fact]
    public async Task Recover_WithInvalidCode_Returns400()
    {
        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"mfa-recover-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var recoverResponse = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaRecover,
            new { recoveryCode = "invalid-code-12345" });
        Assert.Equal(HttpStatusCode.Forbidden, recoverResponse.StatusCode);
    }

    [Fact]
    public async Task Verify_WithRateLimitExceeded_Returns429()
    {
        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"mfa-rate-{Guid.NewGuid():N}";
        var loginResponse = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var enrollResponse = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaEnroll);
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 10; i++)
        {
            lastResponse = await session.PostWithCookiesAsync(IdentityApiRoutes.MfaVerify,
                new { code = $"00000{i}" });
            if ((int)lastResponse.StatusCode == 429) break;
        }

        Assert.True(lastResponse!.StatusCode == HttpStatusCode.TooManyRequests,
            await lastResponse.Content.ReadAsStringAsync());
    }
}
