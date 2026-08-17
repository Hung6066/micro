using FluentAssertions;
using His.Hope.IdentityService.Api.Endpoints;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class HrWebhookReplayStoreTests
{
    [Fact]
    public async Task In_memory_store_accepts_once_and_rejects_replay()
    {
        var store = new BoundedInMemoryHrWebhookReplayStore(10);

        (await store.TryMarkSeenAsync("event-1", TimeSpan.FromMinutes(5), CancellationToken.None)).Should().BeTrue();
        (await store.TryMarkSeenAsync("event-1", TimeSpan.FromMinutes(5), CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task In_memory_store_prunes_expired_entries()
    {
        var store = new BoundedInMemoryHrWebhookReplayStore(10);

        (await store.TryMarkSeenAsync("event-expired", TimeSpan.Zero, CancellationToken.None)).Should().BeTrue();
        (await store.TryMarkSeenAsync("event-expired", TimeSpan.FromMinutes(5), CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task In_memory_store_bounds_oldest_entries()
    {
        var store = new BoundedInMemoryHrWebhookReplayStore(2);

        (await store.TryMarkSeenAsync("event-1", TimeSpan.FromMinutes(5), CancellationToken.None)).Should().BeTrue();
        (await store.TryMarkSeenAsync("event-2", TimeSpan.FromMinutes(5), CancellationToken.None)).Should().BeTrue();
        (await store.TryMarkSeenAsync("event-3", TimeSpan.FromMinutes(5), CancellationToken.None)).Should().BeTrue();
        (await store.TryMarkSeenAsync("event-1", TimeSpan.FromMinutes(5), CancellationToken.None)).Should().BeTrue();
    }
}
