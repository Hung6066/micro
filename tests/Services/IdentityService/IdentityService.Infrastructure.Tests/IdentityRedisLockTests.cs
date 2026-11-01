using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class IdentityRedisLockTests
{
    [Fact]
    public async Task TryAcquire_returns_lease_with_key_and_ttl_when_key_is_free()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringSetAsync(
                "hishop:retention", It.IsAny<RedisValue>(), TimeSpan.FromMinutes(2),
                When.NotExists))
            .ReturnsAsync(true);
        var redis = Redis(database);

        await using var lease = await new IdentityRedisLock(redis.Object)
            .TryAcquireAsync("hishop:retention", TimeSpan.FromMinutes(2));

        lease.Should().NotBeNull();
        database.Verify(x => x.StringSetAsync(
            "hishop:retention", It.Is<RedisValue>(value => !value.IsNullOrEmpty),
            TimeSpan.FromMinutes(2), When.NotExists), Times.Once);
    }

    [Fact]
    public async Task TryAcquire_returns_null_when_another_holder_owns_key()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                When.NotExists)).ReturnsAsync(false);

        var lease = await new IdentityRedisLock(Redis(database).Object)
            .TryAcquireAsync("hishop:retention", TimeSpan.FromMinutes(1));

        lease.Should().BeNull();
    }

    [Fact]
    public async Task Lease_dispose_executes_compare_and_delete_script_for_same_key()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                When.NotExists)).ReturnsAsync(true);
        database.Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), CommandFlags.None))
            .Returns(Task.FromResult(RedisResult.Create(Array.Empty<RedisValue>())));

        await using (await new IdentityRedisLock(Redis(database).Object)
            .TryAcquireAsync("hishop:retention", TimeSpan.FromMinutes(1))) { }

        database.Verify(x => x.ScriptEvaluateAsync(
            It.Is<string>(script => script.Contains("GET", StringComparison.Ordinal)
                && script.Contains("DEL", StringComparison.Ordinal)),
            It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == "hishop:retention"),
            It.Is<RedisValue[]>(values => values.Length == 1 && !values[0].IsNullOrEmpty)
            , CommandFlags.None
            ), Times.Once);
    }

    private static Mock<IConnectionMultiplexer> Redis(Mock<IDatabase> database)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        return redis;
    }
}
