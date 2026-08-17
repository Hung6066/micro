using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class VerificationPageTests
{
    private const string Password = IdentityTestCredentials.Password;
    private const string PendingReturnUrl = IdentityApiRoutes.OidcAuthorize + "?client_id=verification-page-client&redirect_uri=https%3A%2F%2Fapp.example%2Fcallback&response_type=code&scope=openid%20profile&state=verification-state&code_challenge=verification-challenge";
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

        body.Should().Contain($"data-mfa-methods-endpoint=\"{IdentityApiRoutes.MfaMethods}\"");
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
        var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.IdentityLoginScript);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(IdentityApiRoutes.MfaMethods);
        body.Should().Contain(IdentityApiRoutes.PasskeyMfaOptions);
        body.Should().Contain(IdentityApiRoutes.PasskeyMfaComplete);
        body.Should().Contain(IdentityApiRoutes.NativeMfaStart);
        body.Should().Contain(IdentityApiRoutes.NativeMfaPoll);
        body.Should().Contain(IdentityApiRoutes.MfaVerify);

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

    [Fact]
    public async Task Identity_login_script_preserves_click_gesture_for_native_launch_and_has_popup_blocked_fallback()
    {
        var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.IdentityLoginScript);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        var launchWindowIndex = body.IndexOf("const launchWindow = openNativeApprovalWindow();", StringComparison.Ordinal);
        var startFetchIndex = body.IndexOf($"const start = await fetch('{IdentityApiRoutes.NativeMfaStart}'", StringComparison.Ordinal);

        launchWindowIndex.Should().BePositive("the native launch window must be opened synchronously on click");
        startFetchIndex.Should().BeGreaterThan(launchWindowIndex, "the popup-preserving window open must happen before the async start fetch");
        body.Should().Contain("navigateNativeApprovalWindow(launchWindow, payload.deepLink);");
        body.Should().Contain("window.location.assign(deepLink);");
    }

    [Fact]
    public async Task Identity_login_script_uses_server_ticket_lifetime_for_native_polling_and_handles_terminal_states()
    {
        var response = await _fixture.AnonymousClient.GetAsync(IdentityApiRoutes.IdentityLoginScript);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("const DEFAULT_NATIVE_APPROVAL_TICKET_LIFETIME_MS = 5 * 60 * 1000;");
        body.Should().Contain("serverLifetimeMs - NATIVE_APPROVAL_CLIENT_BUFFER_MS");
        body.Should().Contain("pollNativeApproval(payload.ticket, getNativeApprovalPollTimeout(payload.expiresInMs))");
        body.Should().Contain("response.status === 202");
        body.Should().Contain("response.status === 409");
        body.Should().Contain("response.status === 410");
    }

    [Fact]
    public async Task Native_mfa_poll_returns_202_while_mobile_approval_is_still_pending()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaPageUserOptions(
            HasPasskey: true,
            HasTotp: false,
            IsTrustedDevice: false));

        var startResponse = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await startResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("ticket")
            .GetString();
        ticket.Should().NotBeNullOrWhiteSpace();

        var pollResponse = await setup.Session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.NativeMfaPoll}?ticket={Uri.EscapeDataString(ticket!)}");

        pollResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await pollResponse.Content.ReadAsStringAsync();
        body.Should().Contain("pending");
    }

    [Fact]
    public async Task Native_mfa_poll_returns_409_when_mobile_app_rejects_the_ticket()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaPageUserOptions(
            HasPasskey: true,
            HasTotp: false,
            IsTrustedDevice: false));

        var startResponse = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await startResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("ticket")
            .GetString();
        ticket.Should().NotBeNullOrWhiteSpace();
        await RejectNativeTicketAsync(ticket!);

        var pollResponse = await setup.Session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.NativeMfaPoll}?ticket={Uri.EscapeDataString(ticket!)}");

        pollResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await pollResponse.Content.ReadAsStringAsync()).Should().Contain("rejected");
    }

    [Fact]
    public async Task Native_mfa_poll_returns_410_when_mobile_ticket_has_expired()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaPageUserOptions(
            HasPasskey: true,
            HasTotp: false,
            IsTrustedDevice: false));

        var startResponse = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await startResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("ticket")
            .GetString();
        ticket.Should().NotBeNullOrWhiteSpace();
        await DeleteRedisKeyAsync(GetNativeTicketKey(ticket!));

        var pollResponse = await setup.Session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.NativeMfaPoll}?ticket={Uri.EscapeDataString(ticket!)}");

        pollResponse.StatusCode.Should().Be(HttpStatusCode.Gone);
        (await pollResponse.Content.ReadAsStringAsync()).Should().Contain("expired");
    }

    [Fact]
    public async Task Native_mfa_poll_returns_approved_redirect_when_mobile_ticket_is_approved()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaPageUserOptions(
            HasPasskey: true,
            HasTotp: false,
            IsTrustedDevice: false));

        var startResponse = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await startResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("ticket")
            .GetString();
        ticket.Should().NotBeNullOrWhiteSpace();
        await ApproveNativeTicketAsync(ticket!);

        var pollResponse = await setup.Session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.NativeMfaPoll}?ticket={Uri.EscapeDataString(ticket!)}");

        pollResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await pollResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("approved");
        body.GetProperty("redirectUrl").GetString().Should().Be(PendingReturnUrl);
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
        // Each pending MFA page is an independent client flow. Isolate its
        // rate-limit bucket so one test's native start/poll calls cannot make
        // another test observe a false 429.
        session.RateLimitKey = $"verification-mfa-{userId:N}";
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
        loginRequest.Headers.TryAddWithoutValidation("X-RateLimit-Key", session.RateLimitKey);
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

    private async Task DeleteRedisKeyAsync(string key)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        await redis.GetDatabase().KeyDeleteAsync(key);
    }

    private async Task RejectNativeTicketAsync(string ticket)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(GetNativeTicketKey(ticket));
        raw.HasValue.Should().BeTrue();

        using var document = JsonDocument.Parse(raw.ToString());
        var root = document.RootElement;
        var updated = JsonSerializer.Serialize(new
        {
            UserId = root.GetProperty("UserId").GetGuid(),
            PendingId = root.GetProperty("PendingId").GetString(),
            SessionId = root.GetProperty("SessionId").GetString(),
            Approved = false,
            Rejected = true,
            CreatedAt = root.GetProperty("CreatedAt").GetDateTimeOffset()
        });

        await db.StringSetAsync(GetNativeTicketKey(ticket), updated, TimeSpan.FromMinutes(5));
    }

    private async Task ApproveNativeTicketAsync(string ticket)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(GetNativeTicketKey(ticket));
        raw.HasValue.Should().BeTrue();

        using var document = JsonDocument.Parse(raw.ToString());
        var root = document.RootElement;
        var updated = JsonSerializer.Serialize(new
        {
            UserId = root.GetProperty("UserId").GetGuid(),
            PendingId = root.GetProperty("PendingId").GetString(),
            SessionId = root.GetProperty("SessionId").GetString(),
            Approved = true,
            Rejected = false,
            CreatedAt = root.GetProperty("CreatedAt").GetDateTimeOffset()
        });

        await db.StringSetAsync(GetNativeTicketKey(ticket), updated, TimeSpan.FromMinutes(5));
    }

    private static string GetNativeTicketKey(string ticket) => $"hishop:passkey:mfa:native:{ticket}";

    private sealed record PendingMfaPageUserOptions(
        bool HasPasskey,
        bool HasTotp,
        bool IsTrustedDevice);

    private sealed record PendingMfaPageSessionSetup(SessionClient Session);
}
