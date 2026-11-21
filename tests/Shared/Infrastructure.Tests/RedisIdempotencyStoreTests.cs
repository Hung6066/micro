using FluentAssertions;
using His.Hope.Messaging.Redis;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class RedisIdempotencyStoreTests
{
    [Fact]
    public async Task CompleteAsync_WhenKeyWasNotStarted_FailsWithoutCreatingARecord()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(RedisValue.Null);
        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        var store = new RedisIdempotencyStore(redis.Object);

        var action = () => store.CompleteAsync("missing-key", 200, "{}").AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not started*");
        database.Verify(
            x => x.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                false, When.Always, CommandFlags.None),
            Times.Never);
    }
}
