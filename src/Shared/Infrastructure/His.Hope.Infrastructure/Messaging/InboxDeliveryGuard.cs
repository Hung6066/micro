using His.Hope.Messaging;

namespace His.Hope.Infrastructure.Messaging;

/// <summary>
/// Owns an inbox claim for a direct RabbitMQ consumer. A failed side effect
/// releases the claim so a later delivery can retry; a completed side effect
/// keeps the durable receipt.
/// </summary>
public sealed class InboxDeliveryGuard : IAsyncDisposable
{
    private readonly IInboxStore _store;
    private readonly Guid _eventId;
    private readonly string _consumer;
    private bool _completed;

    private InboxDeliveryGuard(IInboxStore store, Guid eventId, string consumer)
    {
        _store = store;
        _eventId = eventId;
        _consumer = consumer;
    }

    public static async ValueTask<InboxDeliveryGuard?> TryBeginAsync(
        IInboxStore store,
        Guid eventId,
        string consumer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);
        return await store.TryBeginAsync(eventId, consumer, cancellationToken)
            ? new InboxDeliveryGuard(store, eventId, consumer)
            : null;
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (_completed) return;
        await _store.MarkCompletedAsync(_eventId, _consumer, cancellationToken);
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed)
            await _store.ReleaseAsync(_eventId, _consumer);
    }
}
