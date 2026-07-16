using System.Collections.Concurrent;
using His.Hope.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.EventBus.InMemory;

public class InMemoryEventBus : IEventBus
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ConcurrentDictionary<Type, List<Type>> _handlers = new();

    public InMemoryEventBus(IServiceScopeFactory serviceScopeFactory) =>
        _serviceScopeFactory = serviceScopeFactory;

    public async Task PublishAsync<TIntegrationEvent>(TIntegrationEvent @event,
        CancellationToken cancellationToken = default)
        where TIntegrationEvent : IntegrationEvent
    {
        var eventType = typeof(TIntegrationEvent);

        if (!_handlers.TryGetValue(eventType, out var handlerTypes))
            return;

        using var scope = _serviceScopeFactory.CreateScope();

        foreach (var handlerType in handlerTypes)
        {
            var handler = (IIntegrationEventHandler<TIntegrationEvent>)
                scope.ServiceProvider.GetRequiredService(handlerType);

            await handler.HandleAsync(@event, cancellationToken);
        }
    }

    public Task SubscribeAsync<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler : IIntegrationEventHandler<TIntegrationEvent>
    {
        var eventType = typeof(TIntegrationEvent);
        var handlerType = typeof(TIntegrationEventHandler);

        _handlers.AddOrUpdate(
            eventType,
            _ => [handlerType],
            (_, existing) =>
            {
                if (!existing.Contains(handlerType))
                    existing.Add(handlerType);
                return existing;
            });

        return Task.CompletedTask;
    }
}
