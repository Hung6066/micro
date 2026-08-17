using His.Hope.IdentityService.Infrastructure.Services;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class RedisDpopReplayCacheTests
{
    [Fact]
    public void Expired_proof_is_rejected_without_writing_to_redis()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        redis.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        var cache = new RedisDpopReplayCache(redis.Object);

        var result = cache.TryRegister("expired", DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.False(result);
        database.Verify(value => value.StringSet(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
            When.NotExists), Times.Never);
    }

    [Fact]
    public void Live_proof_uses_not_exists_semantics_and_returns_redis_result()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        database.Setup(value => value.StringSet(
                It.Is<RedisKey>(key => key.ToString() == "hishop:dpop:jti:live"),
                "1", It.Is<TimeSpan?>(ttl => ttl > TimeSpan.Zero),
                When.NotExists))
            .Returns(true);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        redis.Setup(value => value.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        var cache = new RedisDpopReplayCache(redis.Object);

        var result = cache.TryRegister("live", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.True(result);
        database.VerifyAll();
    }
}
