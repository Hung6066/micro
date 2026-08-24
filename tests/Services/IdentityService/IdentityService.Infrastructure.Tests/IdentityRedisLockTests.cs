using His.Hope.IdentityService.Infrastructure.Services;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityRedisLockTests
{
    [Fact]
    public async Task Try_acquire_returns_lease_when_key_is_new()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        database.Setup(value => value.StringSetAsync(
                It.Is<RedisKey>(key => key.ToString() == "lock-key"),
                It.IsAny<RedisValue>(),
                It.Is<TimeSpan?>(ttl => ttl == TimeSpan.FromMinutes(1)),
                When.NotExists))
            .ReturnsAsync(true);
        database.Setup(value => value.ScriptEvaluateAsync(
                It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns(Task.FromResult(default(RedisResult)));
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        redis.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);

        await using var lease = await new IdentityRedisLock(redis.Object)
            .TryAcquireAsync("lock-key", TimeSpan.FromMinutes(1));

        Assert.NotNull(lease);
        database.Verify(value => value.StringSetAsync(
            It.Is<RedisKey>(key => key.ToString() == "lock-key"),
            It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists), Times.Once);
    }

    [Fact]
    public async Task Try_acquire_returns_null_when_key_is_already_held()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        database.Setup(value => value.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                When.NotExists)).ReturnsAsync(false);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        redis.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);

        var lease = await new IdentityRedisLock(redis.Object).TryAcquireAsync("lock-key", TimeSpan.FromMinutes(1));

        Assert.Null(lease);
    }

    [Fact]
    public async Task Lease_dispose_releases_only_its_own_token()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        database.Setup(value => value.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                When.NotExists)).ReturnsAsync(true);
        database.Setup(value => value.ScriptEvaluateAsync(
                It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns(Task.FromResult(default(RedisResult)));
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        redis.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);

        await using var lease = await new IdentityRedisLock(redis.Object)
            .TryAcquireAsync("lock-key", TimeSpan.FromMinutes(1));
        Assert.NotNull(lease);
    }
}
