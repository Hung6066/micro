using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class PasskeyEndpointTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public async Task Passkey_status_and_registration_options_require_authentication()
    {
        var status = await fixture.AnonymousClient.GetAsync(IdentityApiRoutes.PasskeyStatus);
        var options = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.PasskeyRegisterOptions, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, options.StatusCode);
    }

    [Fact]
    public async Task Passkey_authentication_options_reject_unknown_account_without_leaking_details()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.PasskeyAuthenticateOptions, new
        {
            userName = "missing-passkey@example.test"
        });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Passkey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missing-passkey@example.test", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Passkey_authentication_options_require_an_account_identifier()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.PasskeyAuthenticateOptions, new { });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("account", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Passkey_authentication_options_return_challenge_for_enrolled_account()
    {
        var userId = Guid.NewGuid();
        var email = $"passkey-options-{userId:N}@example.test";
        var credentialId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = new User
            {
                Id = userId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                IsActive = true,
                FirstName = "Passkey",
                LastName = "Options",
                CreatedAt = DateTime.UtcNow
            };
            var create = await users.CreateAsync(user, IdentityTestCredentials.Password);
            Assert.True(create.Succeeded, string.Join(", ", create.Errors.Select(error => error.Description)));

            db.PasskeyCredentials.Add(new PasskeyCredential
            {
                UserId = userId.ToString(),
                CredentialId = credentialId,
                PublicKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                SignatureCounter = 0,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await fixture.AnonymousClient.PostAsJsonAsync(
            IdentityApiRoutes.PasskeyAuthenticateOptions,
            new { userName = email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId.ToString(), body.GetProperty("userId").GetString());
        Assert.True(body.GetProperty("options").GetProperty("challenge").GetString()?.Length > 0);
    }

    [Fact]
    public async Task Passkey_authentication_options_do_not_fallback_from_unknown_user_id_to_known_username()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync(
            IdentityApiRoutes.PasskeyAuthenticateOptions,
            new
            {
                userId = Guid.NewGuid().ToString("D"),
                userName = IdentityTestCredentials.Email
            });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("\"errorCode\":\"passkey_not_enrolled\"", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Passkey_authentication_complete_fails_closed_without_pending_challenge()
    {
        var response = await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.PasskeyAuthenticateComplete, new
        {
            userId = Guid.NewGuid().ToString("D"),
            response = new { }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_passkey_status_and_options_are_available_without_enrollment()
    {
        using var session = await LoginAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await session.GetWithCookiesAsync(IdentityApiRoutes.PasskeyStatus)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyRegisterOptions, new { })).StatusCode);
        Assert.Equal((HttpStatusCode)422,
            (await fixture.AnonymousClient.PostAsJsonAsync(IdentityApiRoutes.PasskeyAuthenticateOptions, new
            {
                userName = IdentityTestCredentials.Email
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyRegisterComplete, new { })).StatusCode);
    }

    [Fact]
    public async Task Authenticated_passkey_status_reports_registered_credentials_and_count()
    {
        var userId = Guid.NewGuid();
        var email = $"passkey-status-{userId:N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = new User
            {
                Id = userId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                IsActive = true,
                FirstName = "Passkey",
                LastName = "Status",
                CreatedAt = DateTime.UtcNow
            };
            var result = await users.CreateAsync(user, IdentityTestCredentials.Password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
            db.PasskeyCredentials.AddRange(
                new PasskeyCredential { UserId = userId.ToString(), CredentialId = Convert.ToBase64String(Guid.NewGuid().ToByteArray()), PublicKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()), CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
                new PasskeyCredential { UserId = userId.ToString(), CredentialId = Convert.ToBase64String(Guid.NewGuid().ToByteArray()), PublicKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var session = fixture.CreateSessionClient();
        var login = await session.LoginAsync(email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var response = await session.GetWithCookiesAsync(IdentityApiRoutes.PasskeyStatus);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("registered").GetBoolean());
        Assert.Equal(2, body.GetProperty("count").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("createdAt").ValueKind);
    }

    [Fact]
    public async Task Passkey_register_complete_without_challenge_returns_expired_challenge_problem()
    {
        using var session = await LoginAsync();
        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyRegisterComplete, new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Passkey_register_complete_rejects_invalid_attestation_after_challenge()
    {
        using var session = await LoginAsync();
        var options = await session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyRegisterOptions, new { });
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);

        var response = await session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyRegisterComplete, new
        {
            id = "invalid-credential",
            rawId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("invalid-credential")),
            type = "public-key",
            response = new
            {
                clientDataJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("invalid-client-data")),
                attestationObject = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("invalid-attestation"))
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Native_and_mfa_passkey_flows_fail_closed_without_pending_context()
    {
        using var session = await LoginAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyMfaOptions, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await session.GetWithCookiesAsync($"{IdentityApiRoutes.NativeMfaPoll}?ticket=missing-ticket")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaOptions, new { ticket = "missing-ticket" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaReject, new { ticket = "missing-ticket" })).StatusCode);
    }

    private async Task<SessionClient> LoginAsync()
    {
        var session = fixture.CreateSessionClient();
        var response = await session.LoginAsync(IdentityTestCredentials.Email, IdentityTestCredentials.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }

}
