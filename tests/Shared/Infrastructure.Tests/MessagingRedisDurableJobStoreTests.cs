using FluentAssertions;
using His.Hope.Messaging;
using His.Hope.Messaging.Redis;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

[Collection("shared-redis")]
public sealed class MessagingRedisDurableJobStoreTests(RedisTestFixture fixture)
{
    [Fact]
    public async Task Concurrent_claims_allow_only_one_worker_to_transition_a_job()
    {
        var store = new RedisDurableJobStore(fixture.Connection);
        var job = new DurableJob(Guid.NewGuid(), "test", "{}");

        (await store.EnqueueAsync(job)).Should().BeTrue();
        var claims = await Task.WhenAll(
            store.TryClaimAsync("worker-a", DateTimeOffset.UtcNow).AsTask(),
            store.TryClaimAsync("worker-b", DateTimeOffset.UtcNow).AsTask());

        claims.Count(claim => claim is not null).Should().Be(1);
        (await store.GetAsync(job.Id))!.Status.Should().Be(DurableJobStatus.Running);
    }

    [Fact]
    public async Task Enqueue_is_atomic_with_queue_visibility_and_duplicate_protection()
    {
        var store = new RedisDurableJobStore(fixture.Connection);
        var job = new DurableJob(Guid.NewGuid(), "test", "{}");

        var results = await Task.WhenAll(
            store.EnqueueAsync(job).AsTask(),
            store.EnqueueAsync(job).AsTask());

        results.Count(result => result).Should().Be(1);
        (await store.TryClaimAsync("worker-a", DateTimeOffset.UtcNow)).Should().NotBeNull();
    }
}
