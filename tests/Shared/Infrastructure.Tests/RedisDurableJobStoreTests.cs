using FluentAssertions;
using His.Hope.Infrastructure.Jobs;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class RedisDurableJobStoreTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private IConnectionMultiplexer _connection = null!;
    private RedisDurableJobStore _store = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _store = new RedisDurableJobStore(_connection, new DurableJobOptions
        {
            StreamKey = $"tests:jobs:{Guid.NewGuid():N}",
            ConsumerGroup = "workers",
            VisibilityTimeout = TimeSpan.FromMinutes(1),
            MaxAttempts = 3
        });
    }

    public async Task DisposeAsync()
    {
        await _connection.CloseAsync();
        await _redis.DisposeAsync();
    }

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
