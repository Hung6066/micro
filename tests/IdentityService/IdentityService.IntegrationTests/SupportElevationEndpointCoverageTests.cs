using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>
/// Exercises the support-elevation endpoint handlers directly with the real EF model.
/// The handlers are private Minimal API delegates, so invoking their contract here keeps
/// validation, tenant ownership, claim resolution, persistence, and expiry filtering covered.
/// </summary>
public sealed class SupportElevationEndpointCoverageTests
{
    [Fact]
    public async Task Create_rejects_missing_target_or_short_reason()
    {
        await using var db = CreateDb();
        var registry = Registry(customer: true);
        var context = UserContext(Guid.NewGuid(), "operator-home");

        var result = await CreateAsync(new SupportElevationEndpoints.SupportElevationCreateRequest(" ", "too short"), db, registry.Object, context);

        result.Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await db.SupportElevations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_rejects_missing_source_customer_mismatch_and_unknown_operator()
    {
        await using var db = CreateDb();
        var registry = Registry(customer: false);

        var missingSource = new DefaultHttpContext { User = User(Guid.NewGuid(), null) };
        (await CreateAsync(new("customer-1", "valid reason here"), db, registry.Object, missingSource))
            .Should().BeOfType<ForbidHttpResult>();

        var nonCustomer = UserContext(Guid.NewGuid(), "operator-home");
        (await CreateAsync(new("internal-1", "valid reason here"), db, registry.Object, nonCustomer))
            .Should().BeOfType<ProblemHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        registry.Setup(x => x.IsCustomerTenant("customer-1")).Returns(true);
        registry.Setup(x => x.GetOperatorHome("customer-1")).Returns("other-home");
        var wrongHome = UserContext(Guid.NewGuid(), "operator-home");
        (await CreateAsync(new("customer-1", "valid reason here"), db, registry.Object, wrongHome))
            .Should().BeOfType<ForbidHttpResult>();

        var unknownUser = UserContext(Guid.Empty, "operator-home", subject: "not-a-guid");
        (await CreateAsync(new("customer-1", "valid reason here"), db, registry.Object, unknownUser))
            .Should().BeOfType<ForbidHttpResult>();
    }

    [Fact]
    public async Task Create_persists_deduplicated_permissions_and_clamps_duration()
    {
        await using var db = CreateDb();
        var registry = Registry(customer: true);
        registry.Setup(x => x.GetOperatorHome("customer-1")).Returns("operator-home");
        var operatorId = Guid.NewGuid();
        var context = UserContext(operatorId, "operator-home");

        var result = await CreateAsync(
            new(" customer-1 ", "  approved support reason  ", 500, ["Admin.Users.Write", "", "admin.users.write"]),
            db, registry.Object, context);

        result.GetType().Name.Should().Be("Created`1");
        var saved = await db.SupportElevations.SingleAsync();
        saved.OperatorUserId.Should().Be(operatorId);
        saved.TargetTenant.Should().Be("customer-1");
        saved.Reason.Should().Be("approved support reason");
        saved.PermissionsJson.Should().Contain("admin.users.write");
        saved.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(59));
        saved.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public async Task List_returns_only_active_elevations_for_current_operator()
    {
        await using var db = CreateDb();
        var operatorId = Guid.NewGuid();
        db.SupportElevations.AddRange(
            new SupportElevation { OperatorUserId = operatorId, SourceTenant = "operator-home", TargetTenant = "customer-1", Reason = "active", ExpiresAt = DateTime.UtcNow.AddMinutes(10) },
            new SupportElevation { OperatorUserId = operatorId, SourceTenant = "operator-home", TargetTenant = "customer-2", Reason = "expired", ExpiresAt = DateTime.UtcNow.AddMinutes(-1) },
            new SupportElevation { OperatorUserId = Guid.NewGuid(), SourceTenant = "operator-home", TargetTenant = "customer-3", Reason = "other operator", ExpiresAt = DateTime.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync();
        var context = UserContext(operatorId, "operator-home");
        IamTenantHttpContext.SetFilter(context, new IamTenantScopeFilter([], ["customer-1"], true, false));

        var result = await ListAsync(db, context);

        var value = result.GetType().GetProperty("Value")!.GetValue(result);
        value.Should().NotBeNull();
        value!.GetType().GetProperty("Count")?.GetValue(value).Should().Be(1);
    }

    private static async Task<IResult> CreateAsync(
        SupportElevationEndpoints.SupportElevationCreateRequest request,
        IdentityDbContext db,
        IConglomerateTenantRegistry registry,
        HttpContext http)
    {
        var method = typeof(SupportElevationEndpoints).GetMethod("CreateElevation", BindingFlags.Static | BindingFlags.NonPublic)!;
        var audit = new Mock<IAuditService>().Object;
        var task = (Task<IResult>)method.Invoke(null, [request, db, registry, http, audit, CancellationToken.None])!;
        return await task;
    }

    private static async Task<IResult> ListAsync(IdentityDbContext db, HttpContext http)
    {
        var method = typeof(SupportElevationEndpoints).GetMethod("ListElevations", BindingFlags.Static | BindingFlags.NonPublic)!;
        var task = (Task<IResult>)method.Invoke(null, [db, http, CancellationToken.None])!;
        return await task;
    }

    private static IdentityDbContext CreateDb() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase($"support-elevation-endpoints-{Guid.NewGuid():N}").Options);

    private static Mock<IConglomerateTenantRegistry> Registry(bool customer)
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.Setup(x => x.IsCustomerTenant(It.IsAny<string>())).Returns(customer);
        registry.Setup(x => x.GetOperatorHome(It.IsAny<string>())).Returns("operator-home");
        return registry;
    }

    private static DefaultHttpContext UserContext(Guid id, string? sourceTenant, string? subject = null) =>
        new() { User = User(id, sourceTenant, subject) };

    private static ClaimsPrincipal User(Guid id, string? sourceTenant, string? subject = null)
    {
        var claims = new List<Claim>();
        if (sourceTenant is not null)
            claims.Add(new Claim("tenant_id", sourceTenant));
        claims.Add(new Claim("sub", subject ?? id.ToString()));
        claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        claims.Add(new Claim("amr", "passkey"));
        claims.Add(new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
