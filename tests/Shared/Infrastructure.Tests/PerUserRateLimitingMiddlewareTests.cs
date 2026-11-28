using System.Net;
using His.Hope.Infrastructure.Abuse;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace His.Hope.Infrastructure.Tests;

public sealed class PerUserRateLimitingMiddlewareTests : IClassFixture<RedisTestFixture>
{
    private readonly RedisTestFixture _fixture;

    public PerUserRateLimitingMiddlewareTests(RedisTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Redis_unavailable_keeps_fallback_counters_isolated_per_client()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "127.0.0.1:1,abortConnect=false",
                ["RateLimiting:MaxRequestsPerIp"] = "1",
                ["RateLimiting:WindowMinutes"] = "1"
            })
            .Build();
        var nextCalls = 0;
        var middleware = new PerUserRateLimitingMiddleware(
            _ =>
            {
                nextCalls++;
                return Task.CompletedTask;
            },
            NullLogger<PerUserRateLimitingMiddleware>.Instance,
            configuration);

        var firstClient = CreateContext("192.0.2.10");
        await middleware.InvokeAsync(firstClient);
        var firstClientRetry = CreateContext("192.0.2.10");
        await middleware.InvokeAsync(firstClientRetry);
        var secondClient = CreateContext("192.0.2.11");
        await middleware.InvokeAsync(secondClient);

        firstClientRetry.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
        secondClient.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        nextCalls.Should().Be(2);
    }

    [Fact]
    public async Task Redis_outage_after_connecting_keeps_fallback_counters_isolated_per_client()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = _fixture.ConnectionString,
                ["RateLimiting:MaxRequestsPerIp"] = "1",
                ["RateLimiting:WindowMinutes"] = "1"
            })
            .Build();
        var nextCalls = 0;
        var middleware = new PerUserRateLimitingMiddleware(
            _ =>
            {
                nextCalls++;
                return Task.CompletedTask;
            },
            NullLogger<PerUserRateLimitingMiddleware>.Instance,
            configuration);

        await _fixture.StopRedisAsync();

        var firstClient = CreateContext("192.0.2.20");
        await middleware.InvokeAsync(firstClient);
        var firstClientRetry = CreateContext("192.0.2.20");
        await middleware.InvokeAsync(firstClientRetry);
        var secondClient = CreateContext("192.0.2.21");
        await middleware.InvokeAsync(secondClient);

        firstClientRetry.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
        secondClient.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        nextCalls.Should().Be(2);
    }

    private static DefaultHttpContext CreateContext(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }
}
