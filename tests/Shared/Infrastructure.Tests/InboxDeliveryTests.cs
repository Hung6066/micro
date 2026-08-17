using His.Hope.Messaging;

namespace His.Hope.Infrastructure.Tests;

public sealed class InboxDeliveryTests
{
    [Fact]
    public async Task Duplicate_delivery_is_skipped_until_failed_delivery_is_released()
    {
        var store = new TestInboxStore();
        var eventId = Guid.NewGuid();
        const string consumer = "patient-projector";

        (await store.TryBeginAsync(eventId, consumer)).Should().BeTrue();
        (await store.TryBeginAsync(eventId, consumer)).Should().BeFalse();

        await store.ReleaseAsync(eventId, consumer);

        (await store.TryBeginAsync(eventId, consumer)).Should().BeTrue();
        await store.MarkCompletedAsync(eventId, consumer);
    }

    private sealed class TestInboxStore : IInboxStore
    {
        private readonly HashSet<(Guid EventId, string Consumer)> _started = [];

        public ValueTask<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_started.Add((eventId, consumer)));

        public ValueTask MarkCompletedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask ReleaseAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
        {
            _started.Remove((eventId, consumer));
            return ValueTask.CompletedTask;
        }
    }
}
