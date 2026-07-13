using His.Hope.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.EventBus.InMemory;

public static class EventBusServiceExtensions
{
    public static IServiceCollection AddInMemoryEventBus(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        return services;
    }
}
