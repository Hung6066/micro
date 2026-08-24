using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class AuditLogEndpointTests : IAsyncLifetime
{
    private readonly string _databaseName = $"audit-log-tests-{Guid.NewGuid():N}";
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    private WebApplication? _app;
    private HttpClient? _client;

    [Fact]
    public async Task AuditEvents_AuthenticatedRequest_UsesServerActorAndRequestMetadata()
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var clientTimestamp = DateTimeOffset.UtcNow.AddYears(-10).ToUnixTimeMilliseconds();
        var before = DateTime.UtcNow;

        var response = await _client!.PostAsJsonAsync(IdentityApiRoutes.AuditEvents, new
        {
            events = new[]
            {
                new
                {
                    action = "READ_PATIENT",
                    timestamp = clientTimestamp,
                    userId = "spoofed-user",
                    userAgent = "spoofed-agent",
                    correlationId = "corr-1",
                    details = new { patientId = "patient-123", password = "do-not-store", nested = new { accessToken = "secret-token" } }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var log = await db.AuditLogs.SingleAsync();
        log.UserId.ShouldBe(TestAuthHandler.UserId);
        log.UserName.ShouldBe(TestAuthHandler.UserName);
        log.UserAgent.ShouldBe("server-agent");
        log.IpAddress.ShouldNotBe("203.0.113.99");
        log.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
        log.ResourceId.ShouldBe("patient-123");
        Assert.DoesNotContain("do-not-store", log.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", log.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditEvents_LargeBatch_PersistsOnlyBoundedBatch()
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var events = Enumerable.Range(0, 125)
            .Select(i => new
            {
                action = "READ_PATIENT",
                timestamp = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds(),
                userId = $"spoofed-{i}",
                userAgent = "spoofed-agent",
                correlationId = $"corr-{i}",
                details = new { patientId = $"patient-{i}" }
            });

        var response = await _client!.PostAsJsonAsync(IdentityApiRoutes.AuditEvents, new { events });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<AuditEventsResponse>();
        body.ShouldBe(new AuditEventsResponse(100, 25));
        (await db.AuditLogs.CountAsync()).ShouldBe(100);
    }

    [Fact]
    public async Task AuditEvents_EmptyOrNullBatch_is_accepted_without_persisting_events()
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var empty = await _client!.PostAsJsonAsync(IdentityApiRoutes.AuditEvents, new { events = Array.Empty<object>() });
        var nullBatch = await _client.PostAsJsonAsync(IdentityApiRoutes.AuditEvents, new { events = (object?)null });

        Assert.Equal(HttpStatusCode.Accepted, empty.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, nullBatch.StatusCode);
        Assert.Equal(0, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task AuditEvents_Rejects_unknown_actions_before_authorization_or_persistence()
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var response = await _client!.PostAsJsonAsync(IdentityApiRoutes.AuditEvents, new
        {
            events = new[]
            {
                new
                {
                    action = "not-a-supported-action",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    userId = "spoofed",
                    details = new { resourceId = "should-not-persist" }
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task AuditLog_CannotBeModifiedOrDeleted()
    {
        await using var scope = _app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var log = new His.Hope.IdentityService.Domain.Entities.AuditLog { UserId = "u", Action = "READ", ResourceType = "Patient" };
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();

        log.Action = "DELETE";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName, DatabaseRoot));
        builder.Services.AddAuthentication(TestAuthHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicyNames.Permissions.PatientsView, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(AuthorizationPolicyNames.Permissions.AdminAuditRead, policy => policy.RequireAuthenticatedUser());

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapGroup("/api/v1").MapAuditLogEndpoints();

        await using var scope = _app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();

        await _app.StartAsync();
        _client = _app.GetTestClient();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("server-agent");
        _client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.99");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();

    }

    private sealed record AuditEventsResponse(int Accepted, int Dropped);

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string Scheme = "Test";
        public const string UserId = "server-user";
        public const string UserName = "Server User";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Name, UserName),
                new Claim("sub", UserId),
                new Claim("permissions", "patients.view,admin.audit.read")
            };
            var identity = new ClaimsIdentity(claims, Scheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme)));
        }
    }
}

internal static class AuditAssertions
{
    public static void ShouldBe<T>(this T actual, T expected)
    {
        Assert.Equal(expected, actual);
    }

    public static void ShouldNotBe<T>(this T actual, T expected)
    {
        Assert.NotEqual(expected, actual);
    }

    public static void ShouldBeGreaterThanOrEqualTo(this DateTime actual, DateTime expected)
    {
        Assert.True(actual >= expected, $"Expected {actual:o} to be >= {expected:o}.");
    }
}
