using His.Hope.EventBus.Abstractions;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace His.Hope.Infrastructure.Events;

public interface IIntegrationEventMapper
{
    IntegrationEvent? Map(IDomainEvent domainEvent);
}

public interface IIntegrationEventMapping
{
    bool CanMap(Type domainEventType);
    IntegrationEvent Map(IDomainEvent domainEvent);
}

public sealed class IntegrationEventMapping<TDomainEvent, TIntegrationEvent>(Func<TDomainEvent, TIntegrationEvent> factory)
    : IIntegrationEventMapping
    where TDomainEvent : IDomainEvent
    where TIntegrationEvent : IntegrationEvent
{
    public bool CanMap(Type domainEventType) => domainEventType == typeof(TDomainEvent);

    public IntegrationEvent Map(IDomainEvent domainEvent) => factory((TDomainEvent)domainEvent);
}

public sealed class IntegrationEventMapper(IEnumerable<IIntegrationEventMapping> mappings)
    : IIntegrationEventMapper
{
    private readonly IReadOnlyList<IIntegrationEventMapping> _mappings = mappings.ToArray();

    public IntegrationEvent? Map(IDomainEvent domainEvent)
    {
        var mapping = _mappings.FirstOrDefault(x => x.CanMap(domainEvent.GetType()));
        return mapping?.Map(domainEvent);
    }
}

public static class IntegrationEventMappingServiceExtensions
{
    public static IServiceCollection AddIntegrationEventMapping<TDomainEvent, TIntegrationEvent>(
        this IServiceCollection services,
        Func<TDomainEvent, TIntegrationEvent> factory)
        where TDomainEvent : IDomainEvent
        where TIntegrationEvent : IntegrationEvent
    {
        services.AddSingleton<IIntegrationEventMapping>(
            new IntegrationEventMapping<TDomainEvent, TIntegrationEvent>(factory));
        services.TryAddSingleton<IIntegrationEventMapper, IntegrationEventMapper>();
        return services;
    }
}
