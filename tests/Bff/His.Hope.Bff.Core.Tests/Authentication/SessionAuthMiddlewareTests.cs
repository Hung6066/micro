using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.Bff.Core.Tests.Authentication;

public class SessionAuthMiddlewareTests
{
    [Fact]
    public async Task ValidCookie_SetsSessionJwt_AndPermissions_InContextItems()
    {
        var sessionData = new SessionData
        {
            UserId = "usr_1",
            Jwt = "eyJhbGciOiJSUzI1NiIs...",
            Permissions = new[] { "patients.view" },
            CsrfToken = "csrf-token",
            UserAgentHash = SessionAuthMiddlewareTestsHelper.Hash("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(55)
        };

        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(
                It.Is<RedisKey>(k => k == "session:abc123"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)JsonSerializer.Serialize(sessionData));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);

        var options = new SessionCookieOptions();
        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, options, multiplexerMock.Object,
            Mock.Of<ILogger<SessionAuthMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "hishop_sid=abc123";
        context.Request.Headers["User-Agent"] = "test-agent";

        await middleware.InvokeAsync(context);

        Assert.Equal("eyJhbGciOiJSUzI1NiIs...", context.Items["SessionJwt"]);
        Assert.Equal(new[] { "patients.view" }, context.Items["Permissions"]);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task ExpiredCookie_Returns401()
    {
        var sessionData = new SessionData
        {
            UserId = "usr_1",
            Jwt = "eyJ...",
            Permissions = Array.Empty<string>(),
            CsrfToken = "csrf",
            UserAgentHash = SessionAuthMiddlewareTestsHelper.Hash("test-agent"),
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-120),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-60)
        };

        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)JsonSerializer.Serialize(sessionData));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);

        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, new SessionCookieOptions(), multiplexerMock.Object,
            Mock.Of<ILogger<SessionAuthMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "hishop_sid=expired";
        context.Request.Headers["User-Agent"] = "test-agent";

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task MissingCookie_PassesThrough_AndReturns401()
    {
        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, new SessionCookieOptions(),
            Mock.Of<IConnectionMultiplexer>(),
            Mock.Of<ILogger<SessionAuthMiddleware>>());

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task UserAgentMismatch_Returns401()
    {
        var sessionData = new SessionData
        {
            UserId = "usr_1",
            Jwt = "eyJ...",
            Permissions = Array.Empty<string>(),
            CsrfToken = "csrf",
            UserAgentHash = SessionAuthMiddlewareTestsHelper.Hash("original-agent"),
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(55)
        };

        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(
                It.Is<RedisKey>(k => k == "session:abc123"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)JsonSerializer.Serialize(sessionData));

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);

        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, new SessionCookieOptions(), multiplexerMock.Object,
            Mock.Of<ILogger<SessionAuthMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "hishop_sid=abc123";
        context.Request.Headers["User-Agent"] = "different-agent";

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task MissingSessionData_Returns401()
    {
        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);

        var middleware = new SessionAuthMiddleware(
            _ => Task.CompletedTask, new SessionCookieOptions(), multiplexerMock.Object,
            Mock.Of<ILogger<SessionAuthMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Headers["Cookie"] = "hishop_sid=nonexistent";

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
    }
}

internal static class SessionAuthMiddlewareTestsHelper
{
    public static string Hash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes);
    }
}
