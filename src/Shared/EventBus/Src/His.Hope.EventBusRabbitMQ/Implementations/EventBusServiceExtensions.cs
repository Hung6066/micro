using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.EventBusRabbitMQ.Implementations;

public static class EventBusServiceExtensions
{
    public static IServiceCollection AddRabbitMQEventBus(
        this IServiceCollection services,
        Action<EventBusOptions> configureOptions)
    {
        var options = new EventBusOptions();
        configureOptions(options);
        services.AddSingleton(options);

        services.AddSingleton<RabbitMQConnection>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RabbitMQConnection>>();
            return new RabbitMQConnection(options, logger);
        });

        services.AddSingleton<IEventBus, RabbitMQEventBus>(sp =>
        {
            var connection = sp.GetRequiredService<RabbitMQConnection>();
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<RabbitMQEventBus>>();
            return new RabbitMQEventBus(connection, options, scopeFactory, logger);
        });

        return services;
    }
}
