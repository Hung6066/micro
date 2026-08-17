using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AdaptiveMfaEndpointTests
{
    private const string Password = IdentityTestCredentials.Password;
    private const string PendingReturnUrl = IdentityApiRoutes.OidcAuthorize + "?client_id=adaptive-client&state=state-123&code_challenge=challenge-123";
    private readonly IdentityServiceTestFixture _fixture;

    public AdaptiveMfaEndpointTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Methods_returns_server_derived_model_for_pending_session()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: true,
            ReturnUrl: PendingReturnUrl));

        var response = await setup.Session.GetWithCookiesAsync(IdentityApiRoutes.MfaMethods);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("preferredMethod").GetString().Should().Be("mobileApproval");
        body.GetProperty("availableMethods").EnumerateArray().Select(item => item.GetString())
            .Should().BeEquivalentTo(["passkey", "mobileApproval", "totp"]);
        body.GetProperty("isUnfamiliarDevice").GetBoolean().Should().BeTrue();
        body.GetProperty("redirectHandle").GetString().Should().Be(IdentityApiRoutes.OidcAuthorize);
        body.TryGetProperty("userId", out _).Should().BeFalse();
        body.TryGetProperty("returnUrl", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Methods_ignores_client_supplied_user_id_query_parameter()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));

        var response = await setup.Session.GetWithCookiesAsync($"{IdentityApiRoutes.MfaMethods}?userId={Guid.NewGuid():D}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("preferredMethod").GetString().Should().Be("mobileApproval");
        body.GetProperty("availableMethods").EnumerateArray().Select(item => item.GetString())
            .Should().BeEquivalentTo(["passkey", "mobileApproval"]);
    }

    [Fact]
    public async Task Methods_returns_401_when_pending_session_binding_mismatches()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: true,
            ReturnUrl: PendingReturnUrl));
        setup.Session.SetCookieValue("hishop_sid", "mismatched-session");

        var response = await setup.Session.GetWithCookiesAsync(IdentityApiRoutes.MfaMethods);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Pending_totp_completion_preserves_original_return_url()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: false,
            HasTotp: true,
            ReturnUrl: PendingReturnUrl));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Mfa");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = GenerateCurrentTotp(setup.TotpSecret!)
        });
        request.Headers.TryAddWithoutValidation(
            "Cookie",
            $"hishop_oidc_mfa={setup.Session.GetCookieValue("hishop_oidc_mfa")}; hishop_sid={setup.Session.GetCookieValue("hishop_sid")}");

        var response = await setup.Session.InnerClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
        response.Headers.Location?.ToString().Should().Be(PendingReturnUrl);
    }

    [Fact]
    public async Task Pending_totp_completion_rejects_invalid_code_without_completing_session()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: false,
            HasTotp: true,
            ReturnUrl: PendingReturnUrl));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Mfa")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "000000"
            })
        };
        request.Headers.TryAddWithoutValidation(
            "Cookie",
            $"hishop_oidc_mfa={setup.Session.GetCookieValue("hishop_oidc_mfa")}; hishop_sid={setup.Session.GetCookieValue("hishop_sid")}");

        var response = await setup.Session.InnerClient.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.TooManyRequests);
        if (response.StatusCode == HttpStatusCode.Redirect)
            response.Headers.Location?.ToString().Should().Be("/Account/Mfa?error=invalid_code");
    }

    [Fact]
    public async Task Native_mfa_options_returns_401_when_pending_session_expires_before_mobile_completion()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));

        var startResponse = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await startResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("ticket")
            .GetString();
        ticket.Should().NotBeNullOrWhiteSpace();

        await DeleteRedisKeyAsync($"session:{setup.Session.GetCookieValue("hishop_sid")}");

        var response = await _fixture.AnonymousClient.PostAsJsonAsync(
            IdentityApiRoutes.NativeMfaOptions,
            new { ticket });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Passkey_mfa_options_returns_422_when_pending_user_has_no_passkey()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: false,
            HasTotp: true,
            ReturnUrl: PendingReturnUrl));

        var response = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyMfaOptions);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).ToLowerInvariant().Should().Contain("not enrolled");
    }

    [Fact]
    public async Task Native_mfa_options_returns_422_when_pending_user_has_no_passkey()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: false,
            HasTotp: true,
            ReturnUrl: PendingReturnUrl));
        var start = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart);
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticket").GetString();
        ticket.Should().NotBeNullOrWhiteSpace();

        var response = await setup.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaOptions, new { ticket });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).ToLowerInvariant().Should().Contain("not enrolled");
    }

    [Fact]
    public async Task Native_mfa_poll_returns_401_for_same_user_different_pending_session()
    {
        var first = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));
        var second = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            UserId: first.UserId,
            Email: first.Email,
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));

        var startResponse = await first.Session.PostWithCookiesAsync(IdentityApiRoutes.NativeMfaStart);
        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await startResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("ticket")
            .GetString();
        ticket.Should().NotBeNullOrWhiteSpace();
        await ApproveNativeTicketAsync(ticket!);

        var response = await second.Session.GetWithCookiesAsync(
            $"{IdentityApiRoutes.NativeMfaPoll}?ticket={Uri.EscapeDataString(ticket!)}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Passkey_mfa_complete_returns_409_when_client_user_id_disagrees_with_pending_session()
    {
        var setup = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));

        var response = await setup.Session.PostWithCookiesAsync(
            IdentityApiRoutes.PasskeyMfaComplete,
            CreatePasskeyAssertionRequest(Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Passkey_mfa_options_creates_distinct_challenges_for_concurrent_same_user_pending_sessions()
    {
        var first = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));
        var second = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            UserId: first.UserId,
            Email: first.Email,
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));

        var firstOptions = await first.Session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyMfaOptions);
        var secondOptions = await second.Session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyMfaOptions);

        firstOptions.StatusCode.Should().Be(HttpStatusCode.OK);
        secondOptions.StatusCode.Should().Be(HttpStatusCode.OK);
        var challengeKeys = await FindRedisKeysAsync($"hishop:passkey:mfa:assertion:{first.UserId:D}:*");
        challengeKeys.Should().HaveCount(2);
        (await RedisKeyExistsAsync($"hishop:passkey:mfa:assertion:{first.UserId:D}")).Should().BeFalse();
    }

    [Fact]
    public async Task Passkey_mfa_complete_returns_401_when_only_another_same_user_pending_session_started_options()
    {
        var first = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));
        var second = await CreatePendingMfaSessionAsync(new PendingMfaUserOptions(
            UserId: first.UserId,
            Email: first.Email,
            HasPasskey: true,
            HasTotp: false,
            ReturnUrl: PendingReturnUrl));

        var secondOptions = await second.Session.PostWithCookiesAsync(IdentityApiRoutes.PasskeyMfaOptions);
        secondOptions.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await first.Session.PostWithCookiesAsync(
            IdentityApiRoutes.PasskeyMfaComplete,
            CreatePasskeyAssertionRequest(first.UserId.ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<PendingMfaSessionSetup> CreatePendingMfaSessionAsync(PendingMfaUserOptions options)
    {
        var userId = options.UserId ?? Guid.NewGuid();
        var email = options.Email ?? $"adaptive-mfa-endpoint-{userId:N}@example.com";
        string? totpSecret = null;

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var encryptor = scope.ServiceProvider.GetRequiredService<IMfaSecretEncryptor>();
            var totpService = scope.ServiceProvider.GetRequiredService<TotpService>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                user = new User
                {
                    Id = userId,
                    UserName = $"adaptive-mfa-endpoint-{userId:N}",
                    Email = email,
                    FirstName = "Adaptive",
                    LastName = "Mfa",
                    IsActive = true,
                    EmailConfirmed = true,
                    TwoFactorEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };

                var create = await userManager.CreateAsync(user, Password);
                create.Succeeded.Should().BeTrue(string.Join(", ", create.Errors.Select(error => error.Description)));
            }
            else
            {
                user.TwoFactorEnabled = true;
                user.IsActive = true;
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }

            if (options.HasPasskey &&
                !await db.PasskeyCredentials.AnyAsync(item => item.UserId == userId.ToString()))
            {
                db.PasskeyCredentials.Add(new PasskeyCredential
                {
                    UserId = userId.ToString(),
                    CredentialId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"credential-{userId:N}")),
                    PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes($"public-key-{userId:N}")),
                    SignatureCounter = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                });
            }

            if (options.HasTotp)
            {
                var mfa = await db.UserMfas.SingleOrDefaultAsync(item => item.UserId == userId);
                totpSecret = totpService.GenerateSecret();
                var encryptedSecret = encryptor.Encrypt(totpSecret);

                if (mfa is null)
                {
                    db.UserMfas.Add(new UserMfa
                    {
                        UserId = userId,
                        SecretKey = encryptedSecret,
                        IsEnabled = true,
                        EnrolledAt = DateTime.UtcNow,
                        RecoveryCodes = [],
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    mfa.SecretKey = encryptedSecret;
                    mfa.IsEnabled = true;
                    mfa.EnrolledAt = DateTime.UtcNow;
                    mfa.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync();
        }

        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"adaptive-mfa-{userId:N}";
        var loginResponse = await session.InnerClient.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = email,
                ["password"] = Password,
                ["returnUrl"] = options.ReturnUrl
            }));
        session.ApplySetCookieHeaders(loginResponse);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginResponse.Headers.Location?.ToString().Should().StartWith("/Account/Mfa");
        session.GetCookieValue("hishop_oidc_mfa").Should().NotBeNullOrWhiteSpace();
        session.GetCookieValue("hishop_sid").Should().NotBeNullOrWhiteSpace();

        return new PendingMfaSessionSetup(session, userId, email, options.ReturnUrl, totpSecret);
    }

    private async Task DeleteRedisKeyAsync(string key)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        await redis.GetDatabase().KeyDeleteAsync(key);
    }

    private async Task<bool> RedisKeyExistsAsync(string key)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        return await redis.GetDatabase().KeyExistsAsync(key);
    }

    private async Task<string[]> FindRedisKeysAsync(string pattern)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var endpoint = redis.GetEndPoints().First();
        var server = redis.GetServer(endpoint);
        return server.Keys(pattern: pattern).Select(key => key.ToString()).Order().ToArray();
    }

    private async Task ApproveNativeTicketAsync(string ticket)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var key = $"hishop:passkey:mfa:native:{ticket}";
        var raw = await db.StringGetAsync(key);
        raw.HasValue.Should().BeTrue();

        using var document = JsonDocument.Parse(raw.ToString());
        var root = document.RootElement;
        var updated = JsonSerializer.Serialize(new
        {
            UserId = root.GetProperty("UserId").GetGuid(),
            PendingId = root.GetProperty("PendingId").GetString(),
            SessionId = root.GetProperty("SessionId").GetString(),
            Approved = true,
            CreatedAt = root.GetProperty("CreatedAt").GetDateTimeOffset()
        });

        await db.StringSetAsync(key, updated, TimeSpan.FromMinutes(5));
    }

    private static object CreatePasskeyAssertionRequest(string userId) =>
        new
        {
            userId,
            response = new
            {
                id = "credential-id",
                rawId = Convert.ToBase64String(Encoding.UTF8.GetBytes("credential-id")),
                type = "public-key",
                response = new
                {
                    authenticatorData = Convert.ToBase64String(Encoding.UTF8.GetBytes("authenticator-data")),
                    clientDataJson = Convert.ToBase64String(Encoding.UTF8.GetBytes("client-data-json")),
                    signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("signature")),
                    userHandle = Convert.ToBase64String(Encoding.UTF8.GetBytes(userId))
                }
            },
            returnUrl = PendingReturnUrl
        };

    private static string GenerateCurrentTotp(string secret)
    {
        var counter = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds / 30;
        var secretBytes = Base32Decode(secret);

        for (var i = -1; i <= 1; i++)
        {
            var code = GenerateTotp(secretBytes, counter + i);
            if (!string.IsNullOrWhiteSpace(code))
                return code;
        }

        throw new InvalidOperationException("Unable to generate a TOTP test code.");
    }

    private static string GenerateTotp(byte[] secret, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0f;
        var binaryCode = (hash[offset] & 0x7f) << 24
                       | (hash[offset + 1] & 0xff) << 16
                       | (hash[offset + 2] & 0xff) << 8
                       | (hash[offset + 3] & 0xff);

        var totp = binaryCode % 1_000_000;
        return totp.ToString("000000");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "").TrimEnd('=');
        var bytes = new List<byte>();
        var bits = 0;
        var bitCount = 0;

        foreach (var character in cleaned)
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
                continue;

            bits = (bits << 5) | index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                bytes.Add((byte)((bits >> bitCount) & 0xff));
            }
        }

        return [.. bytes];
    }

    private sealed record PendingMfaSessionSetup(
        SessionClient Session,
        Guid UserId,
        string Email,
        string ReturnUrl,
        string? TotpSecret);

    private sealed record PendingMfaUserOptions(
        Guid? UserId = null,
        string? Email = null,
        bool HasPasskey = false,
        bool HasTotp = false,
        string ReturnUrl = PendingReturnUrl);
}
