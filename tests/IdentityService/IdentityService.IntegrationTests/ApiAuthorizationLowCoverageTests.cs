using System.Security.Claims;
using FluentAssertions;
using His.Hope.Authorization;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Api.Middleware;
using His.Hope.IdentityService.Application.Assurance;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests.Authorization;

public sealed class ApiAuthorizationLowCoverageTests
{
    [Fact]
    public async Task Support_elevation_guard_skips_disabled_or_same_tenant_requests()
    {
        await using var db = CreateDb();
        var http = Context("source", Guid.NewGuid());
        var sameTenant = new IamTenantScopeFilter([Guid.NewGuid()], ["source"], true, false);

        var disabled = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            http, db, new DisabledConglomerateTenantRegistry(), sameTenant, CancellationToken.None);
        disabled.Should().BeNull();

        var registry = EnabledRegistry(customer: false);
        var same = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            http, db, registry.Object, sameTenant, CancellationToken.None);
        same.Should().BeNull();
    }

    [Fact]
    public async Task Support_elevation_guard_forbids_non_customer_and_missing_policy()
    {
        await using var db = CreateDb();
        var http = Context("source", Guid.NewGuid());
        var filter = new IamTenantScopeFilter([Guid.NewGuid()], ["target"], true, false);

        var registry = EnabledRegistry(customer: false);
        var nonCustomer = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            http, db, registry.Object, filter, CancellationToken.None);
        nonCustomer.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();

        registry = EnabledRegistry(customer: true);
        var missingPolicy = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            http, db, registry.Object, filter, CancellationToken.None);
        missingPolicy.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();
    }

    [Fact]
    public async Task Support_elevation_guard_requires_jit_pair_and_valid_header()
    {
        await using var db = CreateDb();
        var operatorId = Guid.NewGuid();
        var http = Context("source", operatorId);
        var filter = new IamTenantScopeFilter([Guid.NewGuid()], ["target"], true, false);
        var registry = EnabledRegistry(customer: true);
        var policy = new ConfigurableCrossTenantAccessPolicy([
            new CrossTenantAllowedPair("source", "target", "support", ["admin.users.write"], RequiresJit: true)]);
        http.RequestServices = new ServiceCollection()
            .AddSingleton<ICrossTenantAccessPolicy>(policy)
            .BuildServiceProvider();

        var noHeader = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            http, db, registry.Object, filter, CancellationToken.None);
        noHeader.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        http.Request.Headers[ConglomerateConstants.SupportElevationHeader] = "not-a-guid";
        var invalidHeader = await SupportElevationGuard.EnsureCrossTenantMutationAllowedAsync(
            http, db, registry.Object, filter, CancellationToken.None);
        invalidHeader.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>();
    }

    [Fact]
    public void Support_elevation_permissions_allow_empty_or_matching_and_reject_other_actions()
    {
        var empty = new SupportElevation { PermissionsJson = "[]" };
        SupportElevationPermissions.Allows(empty, "admin.users.write").Should().BeTrue();
        var scoped = new SupportElevation { PermissionsJson = "[\"admin.users.write\"]" };
        SupportElevationPermissions.Allows(scoped, "ADMIN.USERS.WRITE").Should().BeTrue();
        SupportElevationPermissions.Allows(scoped, "admin.roles.write").Should().BeFalse();
        SupportElevationHttpContext.GetElevation(new DefaultHttpContext()).Should().BeNull();
    }

    [Fact]
    public async Task Assurance_middleware_bypasses_testing_and_non_mutating_requests()
    {
        var calls = 0;
        var next = new RequestDelegate(_ => { calls++; return Task.CompletedTask; });
        var policy = new AssurancePolicyService(Policy());
        var testing = new AssurancePolicyMiddleware(next, Environment("Testing"));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/admin/users";
        context.User = Authenticated("pwd");
        await testing.InvokeAsync(context, policy);
        calls.Should().Be(1);

        var production = new AssurancePolicyMiddleware(next, Environment("Production"));
        context.Request.Method = HttpMethods.Get;
        await production.InvokeAsync(context, policy);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Assurance_middleware_denies_low_assurance_admin_and_allows_mfa()
    {
        var calls = 0;
        var next = new RequestDelegate(_ => { calls++; return Task.CompletedTask; });
        var middleware = new AssurancePolicyMiddleware(next, Environment("Production"));
        var policy = new AssurancePolicyService(Policy());
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/admin/users";
        context.User = Authenticated("pwd");

        await middleware.InvokeAsync(context, policy);
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        calls.Should().Be(0);

        context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/admin/users";
        context.User = Authenticated("mfa");
        await middleware.InvokeAsync(context, policy);
        calls.Should().Be(1);
    }

    private static IdentityDbContext CreateDb() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase($"support-elevation-{Guid.NewGuid():N}").Options);

    private static DefaultHttpContext Context(string tenant, Guid userId)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tenant_id", tenant),
            new Claim("tenant_membership", "source"),
            new Claim("sub", userId.ToString())], "test"));
        return context;
    }

    private static Mock<IConglomerateTenantRegistry> EnabledRegistry(bool customer)
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.SetupGet(x => x.IsEnabled).Returns(true);
        registry.Setup(x => x.IsCustomerTenant("target")).Returns(customer);
        registry.Setup(x => x.GetClientIdsForTenant(It.IsAny<string>())).Returns([]);
        return registry;
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(name);
        environment.SetupGet(item => item.ContentRootPath).Returns(AppContext.BaseDirectory);
        environment.SetupGet(item => item.ApplicationName).Returns("IdentityService.Tests");
        return environment.Object;
    }

    private static ClaimsPrincipal Authenticated(string amr) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim("amr", amr)], "test"));

    private static AssurancePolicyDocument Policy() => new(
        "test", ["test"],
        [new AssuranceJourneyPolicy("admin-write", "aal2", ["mfa"], false)],
        new AssuranceRecoveryPolicy(false, [], []));
}
