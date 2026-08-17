using FluentAssertions;
using His.Hope.Infrastructure.Jobs;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

 [Collection("shared-redis")]
public sealed class RedisDurableJobStoreTests(RedisTestFixture fixture)
{
    private readonly RedisDurableJobStore _store = new(fixture.Connection, new DurableJobOptions
    {
        StreamKey = $"tests:jobs:{Guid.NewGuid():N}",
        ConsumerGroup = "workers",
        VisibilityTimeout = TimeSpan.FromMinutes(1),
        MaxAttempts = 3
    });

    [Fact]
    public async Task Concurrent_claims_allow_only_one_worker_to_execute_a_delivery()
    {
        var state = new DurableJobState { JobId = "job-1", PayloadJson = "{}", Total = 10 };
        (await _store.EnqueueAsync(state, CancellationToken.None)).Should().BeTrue();

        var message = await _store.ReadNextAsync("worker-a", CancellationToken.None);
        message.Should().NotBeNull();

        var claims = await Task.WhenAll(
            _store.TryClaimAsync(message!, "worker-a", CancellationToken.None),
            _store.TryClaimAsync(message!, "worker-b", CancellationToken.None));

        claims.Count(claimed => claimed).Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_delivery_is_deduplicated_after_completion()
    {
        var state = new DurableJobState { JobId = "job-2", PayloadJson = "{}", Total = 1 };
        (await _store.EnqueueAsync(state, CancellationToken.None)).Should().BeTrue();
        (await _store.EnqueueAsync(state, CancellationToken.None)).Should().BeFalse();

        var message = await _store.ReadNextAsync("worker-a", CancellationToken.None);
        message.Should().NotBeNull();
        (await _store.TryClaimAsync(message!, "worker-a", CancellationToken.None)).Should().BeTrue();
        await _store.CompleteAsync(message!, CancellationToken.None);

        (await _store.TryClaimAsync(message!, "worker-b", CancellationToken.None)).Should().BeFalse();
        (await _store.GetAsync(state.JobId, CancellationToken.None))!.Status.Should().Be(DurableJobStatus.Completed);
    }
}
