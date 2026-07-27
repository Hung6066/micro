using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.EventBusRabbitMQ.Implementations;
using His.Hope.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace His.Hope.Messaging.RabbitMq;

public static class RabbitMqMessagingExtensions
{
    public static IServiceCollection AddHisHopeRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EventBusOptions>(configuration.GetSection("RabbitMQ"));
        // The legacy RabbitMQ connection consumes the concrete options instance,
        // while publishers use IOptions<T>. Register both views from one source.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EventBusOptions>>().Value);
        services.AddSingleton<RabbitMQConnection>();
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        return services;
    }
}

internal sealed class RabbitMqMessagePublisher(
    RabbitMQConnection connection,
    Microsoft.Extensions.Options.IOptions<EventBusOptions> options) : IMessagePublisher
{
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    public async ValueTask PublishAsync(EventEnvelope @event, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rabbitConnection = await connection.GetConnectionAsync();
        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            using var channel = rabbitConnection.CreateModel();
            var settings = options.Value;
            channel.ExchangeDeclare(settings.ExchangeName, settings.ExchangeType, durable: true, autoDelete: false);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = @event.EventType;
            properties.MessageId = @event.Id.ToString("D");
            properties.CorrelationId = @event.CorrelationId;
            properties.Headers = @event.Headers?.ToDictionary(x => x.Key, x => (object)x.Value);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
            channel.BasicPublish(settings.ExchangeName, @event.EventType, properties, body);
        }
        finally
        {
            _channelLock.Release();
        }
    }
}
