using His.Hope.Infrastructure.Messaging;
using His.Hope.Messaging;

namespace His.Hope.Infrastructure.Tests;

public sealed class InboxDeliveryGuardTests
{
    [Fact]
    public async Task Guard_releases_claim_when_side_effect_fails()
    {
        var store = new FakeInboxStore();
        var eventId = Guid.NewGuid();

        await using (var guard = await InboxDeliveryGuard.TryBeginAsync(store, eventId, "consumer"))
        {
            guard.Should().NotBeNull();
        }

        var retried = await InboxDeliveryGuard.TryBeginAsync(store, eventId, "consumer");

        retried.Should().NotBeNull();
    }

    [Fact]
    public async Task Guard_returns_null_for_duplicate_claim()
    {
        var store = new FakeInboxStore();
        var eventId = Guid.NewGuid();
        await using var first = await InboxDeliveryGuard.TryBeginAsync(store, eventId, "consumer");

        var duplicate = await InboxDeliveryGuard.TryBeginAsync(store, eventId, "consumer");

        duplicate.Should().BeNull();
    }

    private sealed class FakeInboxStore : IInboxStore
    {
        private readonly HashSet<(Guid EventId, string Consumer)> _started = [];
        private readonly HashSet<(Guid EventId, string Consumer)> _completed = [];

        public ValueTask<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_started.Add((eventId, consumer)));
        }

        public ValueTask MarkCompletedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _completed.Add((eventId, consumer));
            return ValueTask.CompletedTask;
        }

        public ValueTask ReleaseAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _started.Remove((eventId, consumer));
            return ValueTask.CompletedTask;
        }
    }
}
