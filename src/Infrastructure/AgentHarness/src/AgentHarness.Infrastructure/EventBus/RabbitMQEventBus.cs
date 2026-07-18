using System.Text;
using System.Text.Json;
using His.Hope.AgentHarness.Core.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace His.Hope.AgentHarness.Infrastructure.EventBus;

public class RabbitMQEventBus : IEventBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName = "agent-harness-events";
    private bool _disposed;

    public RabbitMQEventBus(string connectionString)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
    }

    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
    {
        var eventType = typeof(T).Name;
        var routingKey = $"harness.{eventType}";

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(string queueName, Func<T, CancellationToken, Task> handler,
        CancellationToken ct = default) where T : class
    {
        var queue = _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        var eventType = typeof(T).Name;
        var routingKey = $"harness.{eventType}";

        _channel.QueueBind(
            queue: queue,
            exchange: _exchangeName,
            routingKey: routingKey);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(args.Body.Span);
                var @event = JsonSerializer.Deserialize<T>(body);
                if (@event is not null)
                {
                    await handler(@event, ct);
                }

                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch
            {
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: queue,
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
