using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Services;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class UserSessionTrackerTests
{
    [Fact]
    public async Task AddSession_stores_session_in_user_set_and_refreshes_seven_day_expiry()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database.Setup(x => x.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await new UserSessionTracker(Redis(database.Object).Object).AddSessionAsync("user-1", "session-1");

        database.Verify(x => x.SetAddAsync("HisHope:user_sessions:user-1", "session-1", It.IsAny<CommandFlags>()), Times.Once);
        database.Verify(x => x.KeyExpireAsync(
            "HisHope:user_sessions:user-1",
            It.Is<TimeSpan?>(expiry => expiry == TimeSpan.FromDays(7)),
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetUserSessions_returns_all_members_as_strings()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.SetMembersAsync("HisHope:user_sessions:user-1", It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("session-1"), new RedisValue("session-2")]);

        var sessions = await new UserSessionTracker(Redis(database.Object).Object)
            .GetUserSessionsAsync("user-1");

        sessions.Should().Equal("session-1", "session-2");
        database.Verify(x => x.SetMembersAsync("HisHope:user_sessions:user-1", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RemoveSession_removes_only_requested_session()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await new UserSessionTracker(Redis(database.Object).Object)
            .RemoveSessionAsync("user-1", "session-2");

        database.Verify(x => x.SetRemoveAsync("HisHope:user_sessions:user-1", "session-2", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ClearUserSessions_deletes_user_session_set()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await new UserSessionTracker(Redis(database.Object).Object)
            .ClearUserSessionsAsync("user-1");

        database.Verify(x => x.KeyDeleteAsync("HisHope:user_sessions:user-1", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Session_operations_use_distinct_keys_for_distinct_users()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);
        var tracker = new UserSessionTracker(Redis(database.Object).Object);

        await tracker.GetUserSessionsAsync("user-a");
        await tracker.GetUserSessionsAsync("user-b");

        database.Verify(x => x.SetMembersAsync("HisHope:user_sessions:user-a", It.IsAny<CommandFlags>()), Times.Once);
        database.Verify(x => x.SetMembersAsync("HisHope:user_sessions:user-b", It.IsAny<CommandFlags>()), Times.Once);
    }

    private static Mock<IConnectionMultiplexer> Redis(IDatabase database)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database);
        return redis;
    }
}
