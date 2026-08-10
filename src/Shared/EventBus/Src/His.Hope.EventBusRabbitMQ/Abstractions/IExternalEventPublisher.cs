using His.Hope.EventBus.Abstractions;

namespace His.Hope.EventBusRabbitMQ.Abstractions;

/// Publishes integration events to the isolated external-integration exchange.
/// External consumers must treat delivery as at-least-once and deduplicate by event id.
public interface IExternalEventPublisher
{
    Task PublishAsync<TIntegrationEvent>(
        TIntegrationEvent @event,
        string provider,
        CancellationToken cancellationToken = default)
        where TIntegrationEvent : IntegrationEvent;
}
