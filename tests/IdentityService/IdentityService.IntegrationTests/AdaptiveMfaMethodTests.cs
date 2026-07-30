using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Fido2NetLib.Objects;
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
using Microsoft.Data.Sqlite;
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
    public async Task Pending_context_without_trusted_device_binding_is_not_forced_to_unfamiliar()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "recognized-user",
            Email = "recognized@example.com",
            FirstName = "Recognized",
            LastName = "User",
            IsActive = true,
            TwoFactorEnabled = true
        };

        var start = harness.CreateContext("adaptive-mfa-tests/recognized");
        await harness.Service.CompletePrimaryAsync(start, user, "/connect/authorize", ["pwd"]);

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/recognized");
        var pending = harness.Service.TryGetPendingMfaContext(followUp);

        pending.Should().NotBeNull();
        pending!.IsUnfamiliarDevice.Should().BeFalse();
    }

    [Fact]
    public async Task Pending_context_returns_null_when_binding_cookie_mismatches()
    {
        await using var harness = await AdaptiveMfaServiceHarness.CreateAsync();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "binding-user",
            Email = "binding@example.com",
            FirstName = "Binding",
            LastName = "User",
            IsActive = true,
            TwoFactorEnabled = true
        };

        var start = harness.CreateContext("adaptive-mfa-tests/binding");
        await harness.Service.CompletePrimaryAsync(start, user, "/connect/authorize", ["pwd"]);

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/binding");
        followUp.Request.Headers.Cookie = "hishop_oidc_mfa_session=mismatched";

        harness.Service.TryGetPendingMfaContext(followUp).Should().BeNull();
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
        await harness.Service.CompletePrimaryAsync(start, user, "/connect/authorize", ["pwd"]);
        harness.Redis.MarkPendingRecordsExpired();

        var followUp = harness.CreateFollowUpContext(start, "adaptive-mfa-tests/expired");

        harness.Service.TryGetPendingMfaContext(followUp).Should().BeNull();
    }
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
        session.SetCookieValue("hishop_oidc_mfa_session", "mismatched-session");

        var response = await session.PostWithCookiesAsync(
            "/api/v1/auth/passkeys/mfa/complete",
            CreatePasskeyAssertionRequest(Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Passkey_mfa_complete_returns_409_when_client_user_id_disagrees_with_pending_session()
    {
        var (session, userId) = await CreatePendingMfaSessionAsyncWithUserIdAsync();

        var response = await session.PostWithCookiesAsync(
            "/api/v1/auth/passkeys/mfa/complete",
            CreatePasskeyAssertionRequest(Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        userId.Should().NotBe(Guid.Empty);
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
            var create = await userManager.CreateAsync(user, "Test@123456");
            create.Succeeded.Should().BeTrue(string.Join(", ", create.Errors.Select(error => error.Description)));
        }

        var session = _fixture.CreateSessionClient();
        var loginResponse = await session.InnerClient.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = $"adaptive-mfa-{userId:N}@example.com",
                ["password"] = "Test@123456",
                ["returnUrl"] = "/connect/authorize"
            }));
        session.ApplySetCookieHeaders(loginResponse);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginResponse.Headers.Location?.ToString().Should().StartWith("/Account/Mfa");
        session.GetCookieValue("hishop_oidc_mfa").Should().NotBeNullOrWhiteSpace();
        session.GetCookieValue("hishop_oidc_mfa_session").Should().NotBeNullOrWhiteSpace();

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
            returnUrl = "/connect/authorize"
        };
}

internal sealed class AdaptiveMfaServiceHarness : IAsyncDisposable
{
    private AdaptiveMfaServiceHarness(
        OidcLoginCompletionService service,
        TestRedisStore redis,
        SqliteConnection connection,
        IdentityDbContext db)
    {
        Service = service;
        Redis = redis;
        this.connection = connection;
        this.db = db;
    }

    public OidcLoginCompletionService Service { get; }
    public TestRedisStore Redis { get; }
    private readonly SqliteConnection connection;
    private readonly IdentityDbContext db;

    public static async Task<AdaptiveMfaServiceHarness> CreateAsync()
    {
        var redis = new TestRedisStore();
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new IdentityDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var service = new OidcLoginCompletionService(
            CreateSignInManager().Object,
            CreateUserManager().Object,
            db,
            Mock.Of<IMfaSecretEncryptor>(),
            new TotpService(),
            DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"adaptive-mfa-tests-{Guid.NewGuid():N}"))),
            redis.Connection.Object);

        return new(service, redis, connection, db);
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
            .Select(header => header.Split(';', 2)[0]));
        context.Request.Headers.Cookie = cookieHeader;
        return context;
    }

    public ValueTask DisposeAsync()
    {
        db.Dispose();
        connection.Dispose();
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
}
