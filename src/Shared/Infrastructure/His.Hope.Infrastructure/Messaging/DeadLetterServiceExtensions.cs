using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Messaging;

public static class DeadLetterServiceExtensions
{
    /// <summary>
    /// Registers the Dead Letter Queue consumer as a hosted background service.
    /// Reads RabbitMQ connection settings from the "EventBus" configuration section.
    /// </summary>
    /// <typeparam name="TDbContext">
    /// The service's DbContext used to persist dead letter messages.
    /// </typeparam>
    public static IServiceCollection AddDeadLetterConsumer<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : DbContext
    {
        var hostName = configuration.GetValue("EventBus:HostName", "localhost")!;
        var port = configuration.GetValue("EventBus:Port", 5672);
        var userName = configuration.GetValue("EventBus:UserName", "admin")!;
        var password = configuration.GetValue("EventBus:Password", "admin")!;
        var virtualHost = configuration.GetValue("EventBus:VirtualHost", "/")!;

        services.AddHostedService<DeadLetterConsumer<TDbContext>>(sp =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var logger = sp.GetRequiredService<ILogger<DeadLetterConsumer<TDbContext>>>();
            return new DeadLetterConsumer<TDbContext>(
                scopeFactory, logger, hostName, port, userName, password, virtualHost);
        });

        return services;
    }
}
