using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Facility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentitySecurityBoundaryTests
{
    [Fact]
    public async Task FacilityHandler_denies_request_for_an_unassigned_facility()
    {
        var (handler, request) = CreateHandler(new FacilityContext { FacilityId = "facility-a" });
        request.Request.RouteValues["facilityId"] = "facility-b";
        var authorization = new AuthorizationHandlerContext(
            [new FacilityRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            request);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeFalse();
        authorization.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task FacilityHandler_allows_a_facility_from_the_authorized_facility_list()
    {
        var (handler, request) = CreateHandler(new FacilityContext
        {
            FacilityId = "facility-a",
            AuthorizedFacilities = ["facility-b"]
        });
        request.Request.RouteValues["facilityId"] = "facility-b";
        var authorization = new AuthorizationHandlerContext(
            [new FacilityRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            request);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
        authorization.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task FacilityHandler_does_not_allow_cross_facility_access_in_strict_mode()
    {
        var (handler, request) = CreateHandler(new FacilityContext { IsCrossFacility = true });
        request.Request.RouteValues["facilityId"] = "facility-b";
        var authorization = new AuthorizationHandlerContext(
            [new FacilityRequirement(strictMode: true)],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            request);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeFalse();
        authorization.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task FacilityResolution_marks_admin_claim_as_cross_facility()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Admin")], "test"));
        var facility = new FacilityContext();
        var nextCalled = false;
        var middleware = new FacilityResolutionMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<FacilityResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(context, facility);

        facility.IsCrossFacility.Should().BeTrue();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task FacilityHandler_fails_closed_without_http_context()
    {
        var accessor = new HttpContextAccessor();
        var handler = new FacilityAuthorizationHandler(accessor, NullLogger<FacilityAuthorizationHandler>.Instance);
        var authorization = new AuthorizationHandlerContext(
            [new FacilityRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            resource: null);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeFalse();
        authorization.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task FacilityHandler_denies_when_user_has_no_facility()
    {
        var (handler, request) = CreateHandler(new FacilityContext());
        request.Request.RouteValues["facilityId"] = "facility-a";
        var authorization = new AuthorizationHandlerContext(
            [new FacilityRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            request);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeFalse();
        authorization.HasFailed.Should().BeTrue();
    }

    [Theory]
    [InlineData("query")]
    [InlineData("header")]
    public async Task FacilityHandler_resolves_target_facility_from_query_or_header(string source)
    {
        var (handler, request) = CreateHandler(new FacilityContext { FacilityId = "facility-a" });
        if (source == "query")
            request.Request.QueryString = new QueryString("?facilityId=facility-a");
        else
            request.Request.Headers["X-Facility-Id"] = "facility-a";

        var authorization = new AuthorizationHandlerContext(
            [new FacilityRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            request);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
        authorization.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task FacilityHandler_allows_case_insensitive_match_for_current_facility()
    {
        var (handler, request) = CreateHandler(new FacilityContext { FacilityId = "Facility-A" });
        request.Request.RouteValues["facilityId"] = "facility-a";
        var authorization = new AuthorizationHandlerContext(
            [new FacilityRequirement()],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            request);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
    }

    private static (FacilityAuthorizationHandler Handler, HttpContext Request) CreateHandler(FacilityContext facility)
    {
        var context = new DefaultHttpContext();
        var services = new ServiceCollection()
            .AddSingleton(facility)
            .BuildServiceProvider();
        context.RequestServices = services;
        var accessor = new HttpContextAccessor { HttpContext = context };
        return (
            new FacilityAuthorizationHandler(accessor, NullLogger<FacilityAuthorizationHandler>.Instance),
            context);
    }
}
