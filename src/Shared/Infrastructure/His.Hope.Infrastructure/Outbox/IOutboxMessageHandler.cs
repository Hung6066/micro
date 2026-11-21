namespace His.Hope.Infrastructure.Outbox;

/// <summary>
/// Allows a bounded context to publish transport-native outbox contracts
/// without teaching the shared processor about service-specific schemas.
/// </summary>
public interface IOutboxMessageHandler
{
    bool CanHandle(string messageType);
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
