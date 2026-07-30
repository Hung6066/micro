using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class VerificationPageTests
{
    private const string Password = "Test@123456";
    private const string PendingReturnUrl = "/connect/authorize?client_id=verification-page-client&redirect_uri=https%3A%2F%2Fapp.example%2Fcallback&response_type=code&scope=openid%20profile&state=verification-state&code_challenge=verification-challenge";
    private readonly IdentityServiceTestFixture _fixture;

    public VerificationPageTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Mfa_page_renders_mobile_primary_when_server_prefers_mobile_approval()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaPageUserOptions(
            HasPasskey: true,
            HasTotp: true,
            IsTrustedDevice: false));

        var response = await setup.Session.GetWithCookiesAsync("/Account/Mfa?error=invalid_code");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("data-mfa-methods-endpoint=\"/api/v1/auth/mfa/methods\"");
        body.Should().Contain("data-preferred-method=\"mobileApproval\"");
        body.Should().Contain("id=\"passkey-mfa\"");
        body.Should().Contain("id=\"native-passkey-mfa\"");
        body.Should().Contain("id=\"alternate-methods\"");
        body.Should().Contain("id=\"alternate-method-panel\" class=\"alternate-panel\" hidden");
        body.Should().Contain("id=\"totp-form\" class=\"totp-form\" method=\"post\" action=\"/Account/Mfa\" hidden");
        body.Should().Contain("The verification code is invalid or has expired.");
        body.Should().NotContain("verification-page-client");

        body.IndexOf("id=\"native-passkey-mfa\"", StringComparison.Ordinal).Should().BePositive();
        body.IndexOf("id=\"passkey-mfa\"", StringComparison.Ordinal)
            .Should().BeGreaterThan(body.IndexOf("id=\"native-passkey-mfa\"", StringComparison.Ordinal));
        body.IndexOf("id=\"alternate-method-panel\"", StringComparison.Ordinal)
            .Should().BeGreaterThan(body.IndexOf("id=\"passkey-mfa\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mfa_page_keeps_mobile_approval_inside_alternate_methods_when_passkey_is_preferred()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaPageUserOptions(
            HasPasskey: true,
            HasTotp: true,
            IsTrustedDevice: true));

        var response = await setup.Session.GetWithCookiesAsync("/Account/Mfa");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("data-preferred-method=\"passkey\"");
        body.Should().Contain("id=\"alternate-method-panel\" class=\"alternate-panel\" hidden");
        body.Should().Contain("id=\"totp-form\" class=\"totp-form\" method=\"post\" action=\"/Account/Mfa\" hidden");

        var alternatePanelIndex = body.IndexOf("id=\"alternate-method-panel\"", StringComparison.Ordinal);
        var nativeButtonIndex = body.IndexOf("id=\"native-passkey-mfa\"", StringComparison.Ordinal);
        var passkeyButtonIndex = body.IndexOf("id=\"passkey-mfa\"", StringComparison.Ordinal);

        passkeyButtonIndex.Should().BePositive();
        nativeButtonIndex.Should().BeGreaterThan(alternatePanelIndex);
        passkeyButtonIndex.Should().BeLessThan(alternatePanelIndex);
    }

    [Fact]
    public async Task Identity_login_script_uses_pending_session_bound_verification_endpoints()
    {
        var response = await _fixture.AnonymousClient.GetAsync("/api/v1/auth/identity-login.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("/api/v1/auth/mfa/methods");
        body.Should().Contain("/api/v1/auth/passkeys/mfa/options");
        body.Should().Contain("/api/v1/auth/passkeys/mfa/complete");
        body.Should().Contain("/api/v1/auth/passkeys/mfa/native/start");
        body.Should().Contain("/api/v1/auth/passkeys/mfa/native/poll");
        body.Should().Contain("/api/v1/auth/mfa/verify");

        Regex.IsMatch(
                body,
                @"passkeys\/mfa\/complete[\s\S]{0,500}JSON\.stringify\(\{\s*response:\s*serialize\(credential\)\s*\}\)",
                RegexOptions.CultureInvariant)
            .Should().BeTrue("the MFA passkey completion request must not submit a client user id");
        Regex.IsMatch(
                body,
                @"\/api\/v1\/auth\/mfa\/verify[\s\S]{0,300}JSON\.stringify\(\{\s*code\s*\}\)",
                RegexOptions.CultureInvariant)
            .Should().BeTrue("the pending-session TOTP verification request should send only the six-digit code");
    }

    private async Task<PendingMfaPageSessionSetup> CreatePendingMfaSessionAsync(PendingMfaPageUserOptions options)
    {
        var userId = Guid.NewGuid();
        var email = $"verification-page-{userId:N}@example.com";
        var trustedDeviceToken = options.IsTrustedDevice
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            : null;

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            var user = new User
            {
                Id = userId,
                UserName = $"verification-page-{userId:N}",
                Email = email,
                FirstName = "Verification",
                LastName = "Page",
                IsActive = true,
                EmailConfirmed = true,
                TwoFactorEnabled = true,
                TrustedDeviceToken = trustedDeviceToken,
                CreatedAt = DateTime.UtcNow
            };

            var create = await userManager.CreateAsync(user, Password);
            create.Succeeded.Should().BeTrue(string.Join(", ", create.Errors.Select(error => error.Description)));

            if (options.HasPasskey)
            {
                db.PasskeyCredentials.Add(new PasskeyCredential
                {
                    UserId = userId.ToString(),
                    CredentialId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                    PublicKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                    SignatureCounter = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                });
            }

            if (options.HasTotp)
            {
                db.UserMfas.Add(new UserMfa
                {
                    UserId = userId,
                    SecretKey = "integration-placeholder-secret",
                    IsEnabled = true,
                    EnrolledAt = DateTime.UtcNow,
                    RecoveryCodes = [],
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }

        var session = _fixture.CreateSessionClient();
        if (!string.IsNullOrWhiteSpace(trustedDeviceToken))
            session.SetCookieValue("hishop_trusted_device", trustedDeviceToken);

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = email,
                ["password"] = Password,
                ["returnUrl"] = PendingReturnUrl
            })
        };
        if (!string.IsNullOrWhiteSpace(trustedDeviceToken))
            loginRequest.Headers.TryAddWithoutValidation("Cookie", $"hishop_trusted_device={trustedDeviceToken}");
        var loginResponse = await session.InnerClient.SendAsync(loginRequest);
        session.ApplySetCookieHeaders(loginResponse);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect, await loginResponse.Content.ReadAsStringAsync());
        loginResponse.Headers.Location?.ToString().Should().StartWith("/Account/Mfa");
        session.GetCookieValue("hishop_oidc_mfa").Should().NotBeNullOrWhiteSpace();
        session.GetCookieValue("hishop_sid").Should().NotBeNullOrWhiteSpace();

        return new PendingMfaPageSessionSetup(session);
    }

    private sealed record PendingMfaPageUserOptions(
        bool HasPasskey,
        bool HasTotp,
        bool IsTrustedDevice);

    private sealed record PendingMfaPageSessionSetup(SessionClient Session);
}
