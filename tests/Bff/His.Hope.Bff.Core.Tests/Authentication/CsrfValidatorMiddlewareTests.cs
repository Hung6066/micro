using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.Bff.Core.Tests.Authentication;

public class CsrfValidatorMiddlewareTests
{
    [Fact]
    public async Task GET_Request_SkipsCsrfCheck()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "csrf-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Items["SessionId"] = "test-sid";

        await middleware.InvokeAsync(context);

        Assert.NotEqual(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task POST_WithoutCsrfToken_Returns403()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "csrf-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Items["SessionId"] = "test-sid";

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task POST_WithMatchingCsrfToken_Passes()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "csrf-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers["X-CSRF-Token"] = "csrf-token";
        context.Items["SessionId"] = "test-sid";

        await middleware.InvokeAsync(context);

        Assert.NotEqual(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task POST_WithMismatchedCsrfToken_Returns403()
    {
        var redisMock = CreateRedisMockWithSession("test-sid", "correct-token");
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask, redisMock.Object,
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers["X-CSRF-Token"] = "wrong-token";
        context.Items["SessionId"] = "test-sid";

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task POST_WithoutSessionId_Returns403()
    {
        var middleware = new CsrfValidatorMiddleware(
            _ => Task.CompletedTask,
            Mock.Of<IConnectionMultiplexer>(),
            Mock.Of<ILogger<CsrfValidatorMiddleware>>());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";

        await middleware.InvokeAsync(context);

        Assert.Equal(403, context.Response.StatusCode);
    }

    private static Mock<IConnectionMultiplexer> CreateRedisMockWithSession(string sid, string csrfToken)
    {
        var session = new SessionData
        {
            UserId = "usr_1", Jwt = "jwt", Permissions = Array.Empty<string>(),
            CsrfToken = csrfToken, UserAgentHash = "hash",
            IssuedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        var redisMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.StringGetAsync(
                It.Is<RedisKey>(k => k == $"session:{sid}"), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)JsonSerializer.Serialize(session));
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisMock.Object);
        return multiplexerMock;
    }
}
