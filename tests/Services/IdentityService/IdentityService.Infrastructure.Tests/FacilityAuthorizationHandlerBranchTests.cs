using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Facility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class FacilityAuthorizationHandlerBranchTests
{
    [Fact]
    public async Task Cross_facility_user_succeeds_in_default_mode_without_target_facility()
    {
        var (handler, request) = CreateHandler(new FacilityContext { IsCrossFacility = true });
        var authorization = Context(request, strict: false);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
        authorization.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task Assigned_user_fails_when_target_facility_is_missing()
    {
        var (handler, request) = CreateHandler(new FacilityContext { FacilityId = "facility-a" });
        var authorization = Context(request, strict: false);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeFalse();
        authorization.HasFailed.Should().BeTrue();
        authorization.FailureReasons.Should().ContainSingle(reason =>
            reason.Message == "A target facility is required for facility authorization.");
    }

    [Fact]
    public async Task Non_string_route_value_falls_back_to_query_facility()
    {
        var (handler, request) = CreateHandler(new FacilityContext { FacilityId = "facility-a" });
        request.Request.RouteValues["facilityId"] = 42;
        request.Request.QueryString = new QueryString("?facilityId=facility-a");
        var authorization = Context(request, strict: false);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Authorized_facility_list_is_case_insensitive()
    {
        var (handler, request) = CreateHandler(new FacilityContext
        {
            FacilityId = "facility-a",
            AuthorizedFacilities = ["Facility-B"]
        });
        request.Request.RouteValues["facilityId"] = "facility-b";
        var authorization = Context(request, strict: false);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Strict_mode_allows_cross_facility_user_when_explicit_primary_facility_matches()
    {
        var (handler, request) = CreateHandler(new FacilityContext
        {
            IsCrossFacility = true,
            FacilityId = "facility-a"
        });
        request.Request.RouteValues["facilityId"] = "facility-a";
        var authorization = Context(request, strict: true);

        await handler.HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
        authorization.HasFailed.Should().BeFalse();
    }

    private static AuthorizationHandlerContext Context(HttpContext request, bool strict) =>
        new(
            [new FacilityRequirement(strict)],
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            request);

    private static (FacilityAuthorizationHandler Handler, HttpContext Request) CreateHandler(FacilityContext facility)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection()
            .AddSingleton(facility)
            .BuildServiceProvider();
        var accessor = new HttpContextAccessor { HttpContext = context };
        return (
            new FacilityAuthorizationHandler(accessor, NullLogger<FacilityAuthorizationHandler>.Instance),
            context);
    }
}
