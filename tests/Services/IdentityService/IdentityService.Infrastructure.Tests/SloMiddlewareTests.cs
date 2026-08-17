using His.Hope.IdentityService.Api.Metrics;
using His.Hope.Contracts.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class SloMiddlewareTests
{
    [Theory]
    [InlineData(IdentityApiRoutes.OidcToken, 200)]
    [InlineData(IdentityApiRoutes.OidcToken, 400)]
    [InlineData(IdentityApiRoutes.OidcIntrospect, 200)]
    [InlineData(IdentityApiRoutes.Login, 200)]
    [InlineData(IdentityApiRoutes.Login, 401)]
    [InlineData("/api/v1/health", 200)]
    public async Task Invoke_routes_each_endpoint_family_and_always_calls_next(string path, int statusCode)
    {
        var called = false;
        var middleware = new SloMiddleware(context =>
        {
            called = true;
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }, NullLogger<SloMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(statusCode, context.Response.StatusCode);
    }
}
