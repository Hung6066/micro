using FluentAssertions;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Infrastructure.Services;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class RedisWorkloadSessionStoreTests
{
    [Fact]
    public async Task Register_ignores_already_expired_session()
    {
        var database = new Mock<IDatabase>();
        var redis = Redis(database);
        await new RedisWorkloadSessionStore(redis.Object).RegisterAsync(
            new WorkloadSessionRecord("expired", "client", "role", DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(-1)));

        database.Verify(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task Register_and_list_returns_live_sessions_in_issue_order()
    {
        var database = new Mock<IDatabase>();
        var redis = Redis(database);
        var issued = DateTime.UtcNow.AddMinutes(-2);
        var first = new WorkloadSessionRecord("s1", "client", "role", issued, DateTime.UtcNow.AddMinutes(5));
        var second = first with { SessionId = "s2", IssuedAt = issued.AddMinutes(1) };
        database.Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("s1"), new RedisValue("s2")]);
        database.Setup(x => x.StringGetAsync(It.Is<RedisKey>(key => key.ToString().EndsWith(":s1")), It.IsAny<CommandFlags>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize<WorkloadSessionRecord>(first));
        database.Setup(x => x.StringGetAsync(It.Is<RedisKey>(key => key.ToString().EndsWith(":s2")), It.IsAny<CommandFlags>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize<WorkloadSessionRecord>(second));

        var store = new RedisWorkloadSessionStore(redis.Object);
        var result = await store.ListAsync("client");

        result.Select(item => item.SessionId).Should().Equal("s2", "s1");
    }

    [Fact]
    public async Task List_removes_expired_and_malformed_sessions()
    {
        var database = new Mock<IDatabase>();
        var redis = Redis(database);
        var expired = new WorkloadSessionRecord("expired", "client", "role", DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-1));
        database.Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("expired"), new RedisValue("bad")]);
        database.Setup(x => x.StringGetAsync(It.Is<RedisKey>(key => key.ToString().EndsWith(":expired")), It.IsAny<CommandFlags>()))
            .ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(expired));
        database.Setup(x => x.StringGetAsync(It.Is<RedisKey>(key => key.ToString().EndsWith(":bad")), It.IsAny<CommandFlags>()))
            .ReturnsAsync("not-json");
        database.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        database.Setup(x => x.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

        var result = await new RedisWorkloadSessionStore(redis.Object).ListAsync("client");

        result.Should().BeEmpty();
        database.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    [Fact]
    public async Task List_removes_index_entry_when_session_payload_is_missing()
    {
        var database = new Mock<IDatabase>();
        var redis = Redis(database);
        database.Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("missing")]);
        database.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await new RedisWorkloadSessionStore(redis.Object).ListAsync("client");

        result.Should().BeEmpty();
        database.Verify(x => x.SetRemoveAsync(
            It.Is<RedisKey>(key => key.ToString() == "HisHope:workload_sessions:client"),
            It.Is<RedisValue>(value => value.ToString() == "missing"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_all_deletes_each_session_and_index()
    {
        var database = new Mock<IDatabase>();
        var redis = Redis(database);
        database.Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("s1"), new RedisValue("s2")]);
        database.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

        var count = await new RedisWorkloadSessionStore(redis.Object).RevokeAllAsync("client");

        count.Should().Be(2);
        database.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Revoke_returns_false_when_session_key_was_absent()
    {
        var database = new Mock<IDatabase>();
        var redis = Redis(database);
        database.Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);

        var revoked = await new RedisWorkloadSessionStore(redis.Object).RevokeAsync("client", "missing");

        revoked.Should().BeFalse();
        database.Verify(x => x.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_all_honors_cancellation_between_members()
    {
        var database = new Mock<IDatabase>();
        var redis = Redis(database);
        database.Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([new RedisValue("s1")]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await FluentActions.Invoking(() => new RedisWorkloadSessionStore(redis.Object)
                .RevokeAllAsync("client", cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        database.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task Cancellation_is_honored_before_redis_access()
    {
        var redis = Redis(new Mock<IDatabase>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await FluentActions.Invoking(() => new RedisWorkloadSessionStore(redis.Object).ListAsync("client", cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    private static Mock<IConnectionMultiplexer> Redis(Mock<IDatabase> database)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        return redis;
    }
}
