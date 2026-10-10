using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using His.Hope.Contracts.Identity;
using Fido2NetLib.Objects;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class AdaptiveMfaMethodTests
{
    [Fact]
    public void Recognized_device_with_passkey_prefers_passkey()
    {
        var result = AdaptiveMfaMethodPolicy.Resolve(
            hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: false);

        result.PreferredMethod.Should().Be("passkey");
        result.AvailableMethods.Should().BeEquivalentTo("passkey", "mobileApproval", "totp");
    }

    [Fact]
    public void Unfamiliar_device_prefers_mobile_approval()
    {
        var result = AdaptiveMfaMethodPolicy.Resolve(
            hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: true);

        result.PreferredMethod.Should().Be("mobileApproval");
    }

    [Fact]
    public void Totp_is_available_only_when_enrolled()
    {
        var result = AdaptiveMfaMethodPolicy.Resolve(
            hasPasskey: false, hasMobileApproval: false, hasTotp: false, unfamiliarDevice: false);

        result.AvailableMethods.Should().BeEmpty();
    }

    [Fact]
    public async Task CompletePrimaryAsync_creates_pending_context_with_live_hishop_sid_session()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "live-session-user",
            Email = "live-session@example.com",
            FirstName = "Live",
            LastName = "User",
            IsActive = true,
            TwoFactorEnabled = true
        };

        var start = harness.CreateContext("adaptive-mfa-tests/live-session");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var hishopSessionId = GetSetCookieValue(start, "hishop_sid");
        hishopSessionId.Should().NotBeNullOrWhiteSpace();
        harness.Redis.ContainsKey($"session:{hishopSessionId}").Should().BeTrue();

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/live-session");
        var pending = harness.Service.TryGetPendingMfaContext(followUp);

        pending.Should().NotBeNull();
        pending!.SessionId.Should().Be(hishopSessionId);
    }

    [Fact]
    public async Task Pending_context_rejects_replayed_pending_cookies_without_hishop_sid()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "replay-user",
            Email = "replay@example.com",
            FirstName = "Replay",
            LastName = "User",
            IsActive = true,
            TwoFactorEnabled = true
        };

        var start = harness.CreateContext("adaptive-mfa-tests/replay");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var replay = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/replay");
        replay.Request.Headers.Cookie = BuildCookieHeader(start, name => name != "hishop_sid");

        harness.Service.TryGetPendingMfaContext(replay).Should().BeNull();
    }

    [Fact]
    public async Task Pending_context_returns_null_when_hishop_sid_server_session_is_missing()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "missing-session-user",
            Email = "missing-session@example.com",
            FirstName = "Missing",
            LastName = "Session",
            IsActive = true,
            TwoFactorEnabled = true
        };

        var start = harness.CreateContext("adaptive-mfa-tests/missing-session");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var hishopSessionId = GetSetCookieValue(start, "hishop_sid")
            ?? GetSetCookieValue(start, "hishop_oidc_mfa_session");
        hishopSessionId.Should().NotBeNullOrWhiteSpace();
        harness.Redis.RemoveKey($"session:{hishopSessionId}");

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/missing-session");
        followUp.Request.Headers.Cookie = BuildCookieHeader(start, _ => true, ("hishop_sid", hishopSessionId!));

        harness.Service.TryGetPendingMfaContext(followUp).Should().BeNull();
    }

    [Fact]
    public async Task Pending_context_returns_null_when_hishop_sid_server_session_is_expired()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = CreateMfaUser("expired-session");
        var start = harness.CreateContext("adaptive-mfa-tests/expired-session");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var sessionId = GetSetCookieValue(start, "hishop_sid");
        sessionId.Should().NotBeNullOrWhiteSpace();
        harness.Redis.UpdateSession(sessionId!, session => session with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1)
        });

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/expired-session");

        harness.Service.TryGetPendingMfaContext(followUp).Should().BeNull();
    }

    [Fact]
    public async Task Pending_context_returns_null_when_hishop_sid_server_session_has_different_user()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = CreateMfaUser("different-user");
        var start = harness.CreateContext("adaptive-mfa-tests/different-user");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var sessionId = GetSetCookieValue(start, "hishop_sid");
        sessionId.Should().NotBeNullOrWhiteSpace();
        harness.Redis.UpdateSession(sessionId!, session => session with
        {
            UserId = Guid.NewGuid().ToString()
        });

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/different-user");

        harness.Service.TryGetPendingMfaContext(followUp).Should().BeNull();
    }

    [Fact]
    public async Task Pending_context_returns_null_when_hishop_sid_server_session_has_different_user_agent()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = CreateMfaUser("different-user-agent");
        var start = harness.CreateContext("adaptive-mfa-tests/different-user-agent");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var sessionId = GetSetCookieValue(start, "hishop_sid");
        sessionId.Should().NotBeNullOrWhiteSpace();
        harness.Redis.UpdateSession(sessionId!, session => session with
        {
            UserAgentHash = ComputeSha256("another-user-agent")
        });

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/different-user-agent");

        harness.Service.TryGetPendingMfaContext(followUp).Should().BeNull();
    }

    [Fact]
    public async Task CompletePrimaryAsync_rotates_stale_hishop_sid_and_keeps_fresh_browser_unfamiliar()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = CreateMfaUser("stale-session");
        var start = harness.CreateContext("adaptive-mfa-tests/stale-session");
        start.Request.Headers.Cookie = "hishop_sid=stale-session-id";

        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var replacementSessionId = GetSetCookieValue(start, "hishop_sid");
        replacementSessionId.Should().NotBeNullOrWhiteSpace();
        replacementSessionId.Should().NotBe("stale-session-id");
        harness.Redis.ContainsKey($"session:{replacementSessionId}").Should().BeTrue();

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/stale-session");
        var pending = harness.Service.TryGetPendingMfaContext(followUp);

        pending.Should().NotBeNull();
        pending!.SessionId.Should().Be(replacementSessionId);
        pending.IsUnfamiliarDevice.Should().BeTrue();
    }

    [Fact]
    public async Task CompletePrimaryAsync_reuses_matching_live_hishop_sid_as_recognized_browser()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = CreateMfaUser("recognized-session");
        const string userAgent = "adaptive-mfa-tests/recognized-session";
        const string sessionId = "recognized-session-id";
        harness.Redis.StoreSession(sessionId, user.Id, userAgent);
        var start = harness.CreateContext(userAgent);
        start.Request.Headers.Cookie = $"hishop_sid={sessionId}";

        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        GetSetCookieValue(start, "hishop_sid").Should().BeNull();
        var followUp = harness.CreateFollowUpContext(start, userAgent);
        followUp.Request.Headers.Cookie = BuildCookieHeader(
            start,
            _ => true,
            ("hishop_sid", sessionId));
        var pending = harness.Service.TryGetPendingMfaContext(followUp);

        pending.Should().NotBeNull();
        pending!.SessionId.Should().Be(sessionId);
        pending.IsUnfamiliarDevice.Should().BeFalse();
    }

    [Fact]
    public async Task Fresh_browser_without_trusted_device_or_live_session_is_unfamiliar_and_mobile_first()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "fresh-browser-user",
            Email = "fresh-browser@example.com",
            FirstName = "Fresh",
            LastName = "Browser",
            IsActive = true,
            TwoFactorEnabled = true
        };

        var start = harness.CreateContext("adaptive-mfa-tests/fresh-browser");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/fresh-browser");
        var pending = harness.Service.TryGetPendingMfaContext(followUp);

        pending.Should().NotBeNull();
        pending!.IsUnfamiliarDevice.Should().BeTrue();
        AdaptiveMfaMethodPolicy.Resolve(
                hasPasskey: true,
                hasMobileApproval: true,
                hasTotp: true,
                unfamiliarDevice: pending.IsUnfamiliarDevice)
            .PreferredMethod.Should().Be("mobileApproval");
    }

    [Fact]
    public async Task Pending_context_returns_null_when_server_record_is_expired()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "expired-user",
            Email = "expired@example.com",
            FirstName = "Expired",
            LastName = "User",
            IsActive = true,
            TwoFactorEnabled = true
        };

        var start = harness.CreateContext("adaptive-mfa-tests/expired");
        await harness.Service.CompletePrimaryAsync(start, user, IdentityApiRoutes.OidcAuthorize, ["pwd"]);
        harness.Redis.MarkPendingRecordsExpired();

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/expired");

        harness.Service.TryGetPendingMfaContext(followUp).Should().BeNull();
    }

    private static string? GetSetCookieValue(DefaultHttpContext context, string name)
    {
        return context.Response.Headers["Set-Cookie"]
            .Select(header => header?.Split(';', 2)[0].Split('=', 2) ?? [])
            .Where(parts => parts.Length == 2)
            .Where(parts => string.Equals(parts[0], name, StringComparison.Ordinal))
            .Select(parts => parts[1])
            .FirstOrDefault();
    }

    private static string BuildCookieHeader(
        DefaultHttpContext source,
        Func<string, bool> include,
        params (string Name, string Value)[] overrides)
    {
        var cookies = source.Response.Headers["Set-Cookie"]
            .Select(header => header?.Split(';', 2)[0].Split('=', 2) ?? [])
            .Where(parts => parts.Length == 2)
            .Where(parts => include(parts[0]))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        foreach (var (name, value) in overrides)
            cookies[name] = value;

        return string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}"));
    }

    private static User CreateMfaUser(string name) => new()
    {
        Id = Guid.NewGuid(),
        UserName = name,
        Email = $"{name}@example.com",
        FirstName = "Adaptive",
        LastName = "Mfa",
        IsActive = true,
        TwoFactorEnabled = true
    };

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

[Collection("IdentityServiceIntegration")]
public sealed class AdaptiveMfaMethodEndpointTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public AdaptiveMfaMethodEndpointTests(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Passkey_mfa_complete_returns_401_when_pending_session_binding_mismatches()
    {
        var session = await CreatePendingMfaSessionAsync();
        session.SetCookieValue("hishop_sid", "mismatched-session");

        var response = await session.PostWithCookiesAsync(
            IdentityApiRoutes.PasskeyMfaComplete,
            CreatePasskeyAssertionRequest(Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Passkey_mfa_complete_returns_409_when_client_user_id_disagrees_with_pending_session()
    {
        var (session, userId) = await CreatePendingMfaSessionAsyncWithUserIdAsync();

        var response = await session.PostWithCookiesAsync(
            IdentityApiRoutes.PasskeyMfaComplete,
            CreatePasskeyAssertionRequest(Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        userId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Pre_mfa_hishop_sid_does_not_authenticate_user()
    {
        var session = await CreatePendingMfaSessionAsync();

        var response = await session.GetWithCookiesAsync(IdentityApiRoutes.Me);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<SessionClient> CreatePendingMfaSessionAsync()
    {
        var (session, _) = await CreatePendingMfaSessionAsyncWithUserIdAsync();
        return session;
    }

    private async Task<(SessionClient Session, Guid UserId)> CreatePendingMfaSessionAsyncWithUserIdAsync()
    {
        var userId = Guid.NewGuid();
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var existing = await userManager.FindByIdAsync(userId.ToString());
        if (existing is null)
        {
            var user = new User
            {
                Id = userId,
                UserName = $"adaptive-mfa-{userId:N}",
                Email = $"adaptive-mfa-{userId:N}@example.com",
                FirstName = "Adaptive",
                LastName = "Mfa",
                IsActive = true,
                EmailConfirmed = true,
                TwoFactorEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            var create = await userManager.CreateAsync(user, IdentityTestCredentials.Password);
            create.Succeeded.Should().BeTrue(string.Join(", ", create.Errors.Select(error => error.Description)));
        }

        var session = _fixture.CreateSessionClient();
        session.RateLimitKey = $"adaptive-mfa-{userId:N}";
        var loginResponse = await session.InnerClient.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = $"adaptive-mfa-{userId:N}@example.com",
                ["password"] = IdentityTestCredentials.Password,
                ["returnUrl"] = IdentityApiRoutes.OidcAuthorize
            }));
        session.ApplySetCookieHeaders(loginResponse);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginResponse.Headers.Location?.ToString().Should().StartWith("/Account/Mfa");
        session.GetCookieValue("hishop_oidc_mfa").Should().NotBeNullOrWhiteSpace();
        session.GetCookieValue("hishop_sid").Should().NotBeNullOrWhiteSpace();
        session.GetCookieValue("hishop_oidc_mfa_session").Should().BeNull();

        return (session, userId);
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
            returnUrl = IdentityApiRoutes.OidcAuthorize
        };
}

internal sealed class AdaptiveMfaServiceHarness : IAsyncDisposable
{
    private AdaptiveMfaServiceHarness(
        OidcLoginCompletionService service,
        TestRedisStore redis,
        IdentityDbContext db)
    {
        Service = service;
        Redis = redis;
        this.db = db;
    }

    public OidcLoginCompletionService Service { get; }
    public TestRedisStore Redis { get; }
    private readonly IdentityDbContext db;

    public static async Task<AdaptiveMfaServiceHarness> CreateAsync()
    {
        var redis = new TestRedisStore();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"adaptive-mfa-tests-{Guid.NewGuid():N}")
            .Options;
        var db = new IdentityDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var service = new OidcLoginCompletionService(
            CreateSignInManager().Object,
            CreateUserManager().Object,
            db,
            Mock.Of<IIdentityService>(),
            Mock.Of<IMfaSecretEncryptor>(),
            new TotpService(),
            DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"adaptive-mfa-tests-{Guid.NewGuid():N}"))),
            redis.Connection.Object,
            new ConfigurationBuilder().Build());

        return new(service, redis, db);
    }

    public DefaultHttpContext CreateContext(string userAgent)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = userAgent;
        return context;
    }

    public DefaultHttpContext CreateFollowUpContext(DefaultHttpContext source, string userAgent)
    {
        var context = CreateContext(userAgent);
        var cookieHeader = string.Join("; ", source.Response.Headers["Set-Cookie"]
            .Select(header => header!.Split(';', 2)[0]));
        context.Request.Headers.Cookie = cookieHeader;
        return context;
    }

    public ValueTask DisposeAsync()
    {
        db.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private static Mock<UserManager<User>> CreateUserManager()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    private static Mock<SignInManager<User>> CreateSignInManager()
    {
        var userManager = CreateUserManager().Object;
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
        return new Mock<SignInManager<User>>(
            userManager,
            contextAccessor.Object,
            claimsFactory.Object,
            null!,
            null!,
            null!,
            null!);
    }
}

internal sealed class TestRedisStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public TestRedisStore()
    {
        Database = new Mock<IDatabase>(MockBehavior.Strict);
        Connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);

        Database
            .Setup(database => database.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true)
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, _, _, _, _) =>
            {
                _values[key.ToString()] = value.ToString();
            });

        Database
            .Setup(database => database.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true)
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((key, value, _, _, _) =>
            {
                _values[key.ToString()] = value.ToString();
            });

        Database
            .Setup(database => database.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                _values.TryGetValue(key.ToString(), out var value)
                    ? (RedisValue)value
                    : RedisValue.Null);

        Database
            .Setup(database => database.StringGet(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) =>
                _values.TryGetValue(key.ToString(), out var value)
                    ? (RedisValue)value
                    : RedisValue.Null);

        Database
            .Setup(database => database.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) => _values.Remove(key.ToString()));

        Connection
            .Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(Database.Object);
    }

    public Mock<IConnectionMultiplexer> Connection { get; }
    public Mock<IDatabase> Database { get; }

    public void MarkPendingRecordsExpired()
    {
        foreach (var key in _values.Keys.Where(key => key.StartsWith("hishop:oidc-mfa:pending:", StringComparison.Ordinal)).ToArray())
        {
            var pendingRecord = PendingMfaSessionRecord.FromJson(_values[key]);
            _values[key] = (pendingRecord with { CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) }).ToJson();
        }
    }

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public bool RemoveKey(string key) => _values.Remove(key);

    public void StoreSession(string sessionId, Guid userId, string userAgent)
    {
        _values[$"session:{sessionId}"] = JsonSerializer.Serialize(new SessionData
        {
            UserId = userId.ToString(),
            Jwt = "existing-session-token",
            RefreshToken = "existing-refresh-token",
            Permissions = [],
            CsrfToken = "existing-csrf-token",
            UserAgentHash = ComputeSha256(userAgent),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
    }

    public void UpdateSession(string sessionId, Func<SessionData, SessionData> update)
    {
        var key = $"session:{sessionId}";
        _values[key] = JsonSerializer.Serialize(update(
            JsonSerializer.Deserialize<SessionData>(_values[key])!));
    }

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
