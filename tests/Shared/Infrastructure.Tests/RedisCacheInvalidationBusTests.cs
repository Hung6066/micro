using FluentAssertions;
using His.Hope.Infrastructure.Caching;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

[Collection("shared-redis")]
public sealed class RedisCacheInvalidationBusTests(RedisTestFixture fixture)
{
    [Fact]
    public async Task Prefix_invalidation_is_delivered_to_other_replicas_without_payload_data()
    {
        var bus = new RedisCacheInvalidationBus(fixture.Connection);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        bus.Subscribe(prefix =>
        {
            received.TrySetResult(prefix);
            return Task.CompletedTask;
        });

        await bus.PublishPrefixAsync("patients:search:");

        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        delivered.Should().Be("patients:search:");
    }
}
