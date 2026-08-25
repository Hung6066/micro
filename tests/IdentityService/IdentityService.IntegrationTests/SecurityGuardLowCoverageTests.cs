using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using His.Hope.Authorization;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests.Security;

public sealed class SecurityGuardLowCoverageTests
{
    [Fact]
    public async Task Bff_guard_requires_cookie_before_considering_authentication()
    {
        var context = new DefaultHttpContext();
        context.User = AuthenticatedUser("user-1");

        var result = await InvokeBffGuardAsync(context, Mock.Of<IConnectionMultiplexer>(), CreateProtector());

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Bff_guard_rejects_anonymous_request_when_authentication_is_required()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "hishop_sid=session-1";

        var result = await InvokeBffGuardAsync(context, Mock.Of<IConnectionMultiplexer>(), CreateProtector());

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Bff_guard_rejects_missing_expired_and_tampered_sessions()
    {
        var protector = CreateProtector();
        var missing = ContextFor("missing", AuthenticatedUser("user-1"));
        var missingResult = await InvokeBffGuardAsync(missing, RedisWith(RedisValue.Null), protector);
        missingResult.Should().BeOfType<UnauthorizedHttpResult>();

        var expired = ContextFor("expired", AuthenticatedUser("user-1"));
        var expiredSession = Session("user-1", protector, DateTimeOffset.UtcNow.AddMinutes(-1));
        var expiredResult = await InvokeBffGuardAsync(expired, RedisWith(JsonSerializer.Serialize(expiredSession)), protector);
        expiredResult.Should().BeOfType<UnauthorizedHttpResult>();

        var tampered = ContextFor("tampered", AuthenticatedUser("user-1"));
        var tamperedSession = Session("user-1", protector) with { Jwt = "dp:v1:not-a-valid-token" };
        var tamperedResult = await InvokeBffGuardAsync(tampered, RedisWith(JsonSerializer.Serialize(tamperedSession)), protector);
        tamperedResult.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Bff_guard_enforces_csrf_user_agent_and_principal_binding()
    {
        var protector = CreateProtector();
        var session = Session("user-1", protector);

        var csrf = ContextFor("bound", AuthenticatedUser("user-1"));
        csrf.Request.Headers["X-CSRF-Token"] = "wrong";
        var csrfResult = await InvokeBffGuardAsync(csrf, RedisWith(JsonSerializer.Serialize(session)), protector);
        csrfResult.Should().BeOfType<ForbidHttpResult>();

        var userAgent = ContextFor("bound", AuthenticatedUser("user-1"));
        userAgent.Request.Headers.UserAgent = "different-agent";
        userAgent.Request.Headers["X-CSRF-Token"] = session.CsrfToken;
        var userAgentResult = await InvokeBffGuardAsync(userAgent, RedisWith(JsonSerializer.Serialize(session)), protector);
        userAgentResult.Should().BeOfType<ForbidHttpResult>();

        var principal = ContextFor("bound", AuthenticatedUser("other-user"));
        principal.Request.Headers["X-CSRF-Token"] = session.CsrfToken;
        var principalResult = await InvokeBffGuardAsync(principal, RedisWith(JsonSerializer.Serialize(session)), protector);
        principalResult.Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Bff_guard_accepts_valid_session_and_can_skip_principal_binding()
    {
        var protector = CreateProtector();
        var session = Session("user-1", protector);
        var context = ContextFor("valid", AuthenticatedUser("user-1"));
        context.Request.Headers["X-CSRF-Token"] = session.CsrfToken;

        var result = await InvokeBffGuardAsync(context, RedisWith(JsonSerializer.Serialize(session)), protector);

        result.Should().BeNull();
        context.Items["BffSessionId"].Should().Be("valid");
        context.Items["BffSession"].Should().BeOfType<SessionData>();
        var unprotected = (SessionData)context.Items["BffSession"]!;
        unprotected.Jwt.Should().Be("jwt-token");
        unprotected.RefreshToken.Should().Be("refresh-token");

        var noPrincipal = ContextFor("valid", new ClaimsPrincipal(new ClaimsIdentity()));
        noPrincipal.Request.Headers["X-CSRF-Token"] = session.CsrfToken;
        var noPrincipalResult = await InvokeBffGuardAsync(
            noPrincipal, RedisWith(JsonSerializer.Serialize(session)), protector, requireAuthenticatedPrincipal: false);
        noPrincipalResult.Should().BeNull();
    }

    [Fact]
    public async Task Support_elevation_guard_allows_membership_and_validates_approved_elevation()
    {
        await using var db = CreateDb();
        var operatorId = Guid.NewGuid();
        var filter = new IamTenantScopeFilter([Guid.NewGuid()], ["target"], true, false);
        var registry = EnabledRegistry();
        var policy = new ConfigurableCrossTenantAccessPolicy([
            new CrossTenantAllowedPair("source", "target", "support", ["admin.users.write"], RequiresJit: true)]);

        var memberContext = Context("source", operatorId, "target");
        memberContext.RequestServices = Services(policy);
        var memberResult = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            memberContext, db, registry.Object, filter, CancellationToken.None);
        memberResult.Should().BeNull();

        var noJitContext = Context("source", operatorId);
        noJitContext.RequestServices = Services(new ConfigurableCrossTenantAccessPolicy([
            new CrossTenantAllowedPair("source", "target", "support", ["admin.users.write"], RequiresJit: false)]));
        var noJitResult = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            noJitContext, db, registry.Object, filter, CancellationToken.None);
        noJitResult.Should().BeOfType<ForbidHttpResult>();

        var elevationId = Guid.NewGuid();
        db.SupportElevations.Add(new SupportElevation
        {
            Id = elevationId,
            OperatorUserId = operatorId,
            SourceTenant = "source",
            TargetTenant = "target",
            Status = "approved",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            PermissionsJson = "[\"admin.users.write\"]"
        });
        await db.SaveChangesAsync();

        var validContext = Context("source", operatorId);
        validContext.RequestServices = Services(policy);
        validContext.Request.Headers[ConglomerateConstants.SupportElevationHeader] = elevationId.ToString();
        var validResult = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            validContext, db, registry.Object, filter, CancellationToken.None);
        validResult.Should().BeNull();
        SupportElevationHttpContext.GetElevation(validContext)!.Id.Should().Be(elevationId);
    }

    [Fact]
    public async Task Support_elevation_guard_skips_denied_scope_missing_source_and_ambiguous_target()
    {
        await using var db = CreateDb();
        var registry = EnabledRegistry();
        var policy = new ConfigurableCrossTenantAccessPolicy([
            new CrossTenantAllowedPair("source", "target", "support", ["admin.users.write"], RequiresJit: true)]);

        var denied = Context("source", Guid.NewGuid());
        denied.RequestServices = Services(policy);
        var deniedResult = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            denied, db, registry.Object, new IamTenantScopeFilter([Guid.NewGuid()], ["target"], true, true), CancellationToken.None);
        deniedResult.Should().BeNull();

        var missingSource = new DefaultHttpContext { RequestServices = Services(policy) };
        var missingSourceResult = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            missingSource, db, registry.Object, new IamTenantScopeFilter([Guid.NewGuid()], ["target"], true, false), CancellationToken.None);
        missingSourceResult.Should().BeNull();

        var ambiguousTarget = Context("source", Guid.NewGuid());
        ambiguousTarget.RequestServices = Services(policy);
        var ambiguousResult = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            ambiguousTarget, db, registry.Object,
            new IamTenantScopeFilter([Guid.NewGuid()], ["target", "another-target"], true, false), CancellationToken.None);
        ambiguousResult.Should().BeNull();

        var missingHeader = Context("source", Guid.NewGuid());
        missingHeader.RequestServices = Services(policy);
        var missingHeaderResult = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            missingHeader, db, registry.Object,
            new IamTenantScopeFilter([Guid.NewGuid()], ["target"], true, false), CancellationToken.None);
        missingHeaderResult.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Support_elevation_guard_rejects_expired_or_mismatched_elevation()
    {
        await using var db = CreateDb();
        var operatorId = Guid.NewGuid();
        var filter = new IamTenantScopeFilter([Guid.NewGuid()], ["target"], true, false);
        var registry = EnabledRegistry();
        var policy = new ConfigurableCrossTenantAccessPolicy([
            new CrossTenantAllowedPair("source", "target", "support", ["admin.users.write"], RequiresJit: true)]);
        var expiredId = Guid.NewGuid();
        var mismatchedId = Guid.NewGuid();
        db.SupportElevations.AddRange(
            new SupportElevation
            {
                Id = expiredId,
                OperatorUserId = operatorId,
                SourceTenant = "source",
                TargetTenant = "target",
                Status = "approved",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                PermissionsJson = "[]"
            },
            new SupportElevation
            {
                Id = mismatchedId,
                OperatorUserId = operatorId,
                SourceTenant = "other",
                TargetTenant = "target",
                Status = "approved",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                PermissionsJson = "[]"
            });
        await db.SaveChangesAsync();

        foreach (var elevationId in new[] { expiredId, mismatchedId })
        {
            var context = Context("source", operatorId);
            context.RequestServices = Services(policy);
            context.Request.Headers[ConglomerateConstants.SupportElevationHeader] = elevationId.ToString();
            var result = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
                context, db, registry.Object, filter, CancellationToken.None);
            if (elevationId == expiredId)
                result.Should().BeOfType<ProblemHttpResult>();
            else
                result.Should().BeOfType<ForbidHttpResult>();
        }
    }

    private static async Task<IResult?> InvokeBffGuardAsync(
        HttpContext context,
        IConnectionMultiplexer redis,
        SessionTokenProtector protector,
        bool requireAuthenticatedPrincipal = true)
    {
        var guardType = typeof(SupportElevationGuard).Assembly.GetType(
            "His.Hope.IdentityService.Api.Security.BffSessionGuard", throwOnError: true)!;
        var method = guardType.GetMethod("ValidateMutatingSessionAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var task = (Task<IResult?>)method.Invoke(null, [context, redis, protector, requireAuthenticatedPrincipal])!;
        return await task;
    }

    private static DefaultHttpContext ContextFor(string sessionId, ClaimsPrincipal user)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"hishop_sid={sessionId}";
        context.Request.Headers.UserAgent = "test-agent";
        context.Request.Headers["X-CSRF-Token"] = "csrf-token";
        context.User = user;
        return context;
    }

    private static ClaimsPrincipal AuthenticatedUser(string userId) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, userId)], "test"));

    private static SessionData Session(string userId, SessionTokenProtector protector, DateTimeOffset? expiresAt = null) => new()
    {
        UserId = userId,
        Jwt = protector.Protect("jwt-token"),
        RefreshToken = protector.Protect("refresh-token"),
        Permissions = [],
        CsrfToken = "csrf-token",
        UserAgentHash = Sha256("test-agent"),
        IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(10)
    };

    private static SessionTokenProtector CreateProtector()
    {
        var path = Path.Combine(Path.GetTempPath(), "identity-session-tests", Guid.NewGuid().ToString("N"));
        return new SessionTokenProtector(DataProtectionProvider.Create(new DirectoryInfo(path)));
    }

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static IConnectionMultiplexer RedisWith(RedisValue value)
    {
        var database = new Mock<IDatabase>();
        database.Setup(item => item.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(value);
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(item => item.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        return redis.Object;
    }

    private static IdentityDbContext CreateDb() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase($"support-elevation-guards-{Guid.NewGuid():N}").Options);

    private static Mock<IConglomerateTenantRegistry> EnabledRegistry()
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.SetupGet(item => item.IsEnabled).Returns(true);
        registry.Setup(item => item.IsCustomerTenant("target")).Returns(true);
        return registry;
    }

    private static DefaultHttpContext Context(string sourceTenant, Guid operatorId, params string[] memberships)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tenant_id", sourceTenant),
            new Claim("sub", operatorId.ToString()),
            .. memberships.Select(item => new Claim("tenant_membership", item))], "test"));
        return context;
    }

    private static IServiceProvider Services(ICrossTenantAccessPolicy policy) => new ServiceCollection()
        .AddSingleton(policy)
        .BuildServiceProvider();
}
