using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Api.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class StepUpAuthenticationGuardTests
{
    [Theory]
    [InlineData("mfa")]
    [InlineData("totp")]
    [InlineData("passkey")]
    [InlineData("webauthn")]
    public void Fresh_mfa_accepts_supported_authentication_method(string method)
    {
        var principal = Principal(method, DateTimeOffset.UtcNow);

        StepUpAuthenticationGuard.HasFreshMfa(principal).Should().BeTrue();
    }

    [Fact]
    public void Fresh_mfa_rejects_password_only_and_stale_authentication()
    {
        StepUpAuthenticationGuard.HasFreshMfa(
            Principal("pwd", DateTimeOffset.UtcNow)).Should().BeFalse();
        StepUpAuthenticationGuard.HasFreshMfa(
            Principal("mfa", DateTimeOffset.UtcNow.AddMinutes(-16))).Should().BeFalse();
    }

    [Fact]
    public void Empty_elevation_permission_set_is_not_treated_as_all_permissions()
    {
        var elevation = new Domain.Entities.SupportElevation { PermissionsJson = "[]" };

        SupportElevationPermissions.Allows(elevation, "admin.users.write").Should().BeFalse();
    }

    [Fact]
    public async Task Mutation_filter_blocks_stale_mfa_before_handler_runs()
    {
        var context = new DefaultHttpContext { User = Principal("mfa", DateTimeOffset.UtcNow.AddMinutes(-16)) };
        context.Request.Method = HttpMethods.Post;
        var handlerCalled = false;

        var result = await StepUpAuthenticationGuard.RequireFreshMfaForMutationFilter(
            new DefaultEndpointFilterInvocationContext(context),
            _ =>
            {
                handlerCalled = true;
                return ValueTask.FromResult<object?>(Results.Ok());
            });

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Mutation_filter_does_not_step_up_read_operations()
    {
        var context = new DefaultHttpContext { User = Principal("pwd", DateTimeOffset.UtcNow.AddMinutes(-16)) };
        context.Request.Method = HttpMethods.Get;

        var result = await StepUpAuthenticationGuard.RequireFreshMfaForMutationFilter(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Mutation_filter_does_not_step_up_read_only_post_analyzer()
    {
        var context = new DefaultHttpContext { User = Principal("pwd", DateTimeOffset.UtcNow.AddMinutes(-16)) };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/admin/iam/analyzer";

        var result = await StepUpAuthenticationGuard.RequireFreshMfaForMutationFilter(
            new DefaultEndpointFilterInvocationContext(context),
            _ => ValueTask.FromResult<object?>(Results.Ok()));

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    private static ClaimsPrincipal Principal(string method, DateTimeOffset authenticatedAt) =>
        new(new ClaimsIdentity(
        [
            new Claim("amr", method),
            new Claim("auth_time", authenticatedAt.ToUnixTimeSeconds().ToString()),
        ], "test"));

    private sealed class DefaultEndpointFilterInvocationContext(HttpContext httpContext)
        : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;
        public override IList<object?> Arguments { get; } = [];
        public override T GetArgument<T>(int index) => (T)Arguments[index]!;
    }
}
