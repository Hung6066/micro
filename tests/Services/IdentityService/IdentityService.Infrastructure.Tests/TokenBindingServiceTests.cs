using FluentAssertions;
using His.Hope.IdentityService.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class TokenBindingServiceTests
{
    [Fact]
    public async Task ValidateBindingAsync_WhenNoBindingExists_AllowsToken()
    {
        var database = CreateDatabase(RedisValue.Null);
        var service = CreateService(database);

        (await service.ValidateBindingAsync("jti", "user", "10.0.0.1")).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateBindingAsync_WhenBindingMatchesUserAndIp_AllowsToken()
    {
        var value = $"user:{Hash("10.0.0.1")}:client";
        var database = CreateDatabase(value);
        var service = CreateService(database);

        (await service.ValidateBindingAsync("jti", "user", "10.0.0.1")).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateBindingAsync_WhenUserOrIpDiffers_RejectsToken()
    {
        var value = $"user:{Hash("10.0.0.1")}:client";
        var database = CreateDatabase(value);
        var service = CreateService(database);

        (await service.ValidateBindingAsync("jti", "other-user", "10.0.0.1")).Should().BeFalse();
        (await service.ValidateBindingAsync("jti", "user", "10.0.0.2")).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateBindingAsync_WhenStoredValueIsMalformed_RejectsToken()
    {
        var database = CreateDatabase("user-only");
        var service = CreateService(database);

        (await service.ValidateBindingAsync("jti", "user", "10.0.0.1")).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateBindingAsync_WhenIpIsNull_UsesUnknownIpBinding()
    {
        var database = CreateDatabase($"user:{Hash(null)}:client");
        var service = CreateService(database);

        (await service.ValidateBindingAsync("jti", "user", null!)).Should().BeTrue();
    }

    [Fact]
    public async Task BindTokenAsync_StoresUserIpHashAndClientWithRequestedTtl()
    {
        var database = CreateDatabase(RedisValue.Null);
        var service = CreateService(database);

        await service.BindTokenAsync("jti", "user", "10.0.0.1", "client", TimeSpan.FromMinutes(5));

        database.Verify(x => x.StringSetAsync(
            (RedisKey)"token_binding:jti",
            It.Is<RedisValue>(value => value.ToString() == $"user:{Hash("10.0.0.1")}:client"),
            TimeSpan.FromMinutes(5),
            false,
            When.Always,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task BindTokenAsync_UsesOneHourDefaultTtlWhenNotProvided()
    {
        var database = CreateDatabase(RedisValue.Null);
        var service = CreateService(database);

        await service.BindTokenAsync("jti", "user", "10.0.0.1", "client");

        database.Verify(x => x.StringSetAsync(
            (RedisKey)"token_binding:jti",
            It.IsAny<RedisValue>(),
            TimeSpan.FromHours(1),
            false,
            When.Always,
            CommandFlags.None), Times.Once);
    }

    private static Mock<IDatabase> CreateDatabase(RedisValue value)
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringGetAsync(
                It.IsAny<RedisKey>(),
                CommandFlags.None))
            .ReturnsAsync(value);
        database.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                false,
                When.Always, CommandFlags.None))
            .ReturnsAsync(true);
        return database;
    }

    private static TokenBindingService CreateService(Mock<IDatabase> database)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        return new TokenBindingService(redis.Object, NullLogger<TokenBindingService>.Instance);
    }

    private static string Hash(string? ip) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(ip ?? "unknown")))[..12];
}
