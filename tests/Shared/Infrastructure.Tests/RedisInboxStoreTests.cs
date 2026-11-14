using FluentAssertions;
using His.Hope.Messaging;
using His.Hope.Messaging.Redis;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

[Collection("shared-redis")]
public sealed class RedisInboxStoreTests(RedisTestFixture fixture)
{
    [Fact]
    public async Task Duplicate_delivery_is_deduplicated_and_failed_delivery_can_be_released()
    {
        var store = new RedisInboxStore(fixture.Connection);
        var eventId = Guid.NewGuid();
        const string consumer = "patient-projector";

        (await store.TryBeginAsync(eventId, consumer)).Should().BeTrue();
        (await store.TryBeginAsync(eventId, consumer)).Should().BeFalse();

        await store.ReleaseAsync(eventId, consumer);
        (await store.TryBeginAsync(eventId, consumer)).Should().BeTrue();

        await store.MarkCompletedAsync(eventId, consumer);
        (await store.TryBeginAsync(eventId, consumer)).Should().BeFalse();
    }

    [Fact]
    public async Task Event_receipts_are_scoped_by_consumer()
    {
        var store = new RedisInboxStore(fixture.Connection);
        var eventId = Guid.NewGuid();

        (await store.TryBeginAsync(eventId, "consumer-a")).Should().BeTrue();
        (await store.TryBeginAsync(eventId, "consumer-b")).Should().BeTrue();
    }

}
