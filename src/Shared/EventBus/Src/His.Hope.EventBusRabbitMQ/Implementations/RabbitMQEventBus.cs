using System.Text;
using System.Text.RegularExpressions;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace His.Hope.EventBusRabbitMQ.Implementations;

public partial class RabbitMQEventBus : IEventBus, IAsyncDisposable
{
    private readonly RabbitMQConnection _connection;
    private readonly EventBusOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private IModel? _consumerChannel;
    private readonly Dictionary<string, List<Type>> _eventHandlers = new();

    public RabbitMQEventBus(
        RabbitMQConnection connection,
        EventBusOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMQEventBus> logger)
    {
        _connection = connection;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retryPolicy = Policy.Handle<BrokerUnreachableException>()
            .WaitAndRetryAsync(options.RetryCount,
                retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));
    }

    public async Task PublishAsync<TIntegrationEvent>(TIntegrationEvent @event,
        CancellationToken cancellationToken = default)
        where TIntegrationEvent : IntegrationEvent
    {
        if (!_connection.IsConnected)
            await _connection.GetConnectionAsync();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var channel = (await _connection.GetConnectionAsync())
                .CreateModel();

            channel.ExchangeDeclare(_options.ExchangeName, _options.ExchangeType, durable: true);

            var eventName = GetEventName<TIntegrationEvent>();
            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = @event.Id.ToString();
            properties.Timestamp = new AmqpTimestamp(
                new DateTimeOffset(@event.CreationDate).ToUnixTimeSeconds());
            properties.Type = eventName;

            var routingKey = eventName;
            channel.BasicPublish(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published {EventName} {EventId}",
                eventName, @event.Id);
        });
    }

    public async Task SubscribeAsync<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler : IIntegrationEventHandler<TIntegrationEvent>
    {
        var eventName = GetEventName<TIntegrationEvent>();
        var handlerType = typeof(TIntegrationEventHandler);

        if (!_eventHandlers.ContainsKey(eventName))
            _eventHandlers[eventName] = [];

        if (!_eventHandlers[eventName].Contains(handlerType))
            _eventHandlers[eventName].Add(handlerType);

        await StartConsumerAsync();

        _logger.LogInformation("Subscribed {Handler} to {EventName}",
            handlerType.Name, eventName);
    }

    private async Task StartConsumerAsync()
    {
        if (_consumerChannel is { IsOpen: true })
            return;

        if (!_connection.IsConnected)
            await _connection.GetConnectionAsync();

        _consumerChannel = _connection.GetConnectionAsync()
            .GetAwaiter().GetResult()
            .CreateModel();

        _consumerChannel.ExchangeDeclare(_options.ExchangeName, _options.ExchangeType, durable: true);
        _consumerChannel.BasicQos(0, (ushort)_options.PrefetchCount, false);

        foreach (var eventName in _eventHandlers.Keys)
        {
            var queueName = $"{_options.ExchangeName}.{eventName}";
            _consumerChannel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
            _consumerChannel.QueueBind(queueName, _options.ExchangeName, eventName);
        }

        var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
        consumer.Received += OnMessageReceived;

        foreach (var eventName in _eventHandlers.Keys)
        {
            var queueName = $"{_options.ExchangeName}.{eventName}";
            _consumerChannel.BasicConsume(queueName, autoAck: false, consumer: consumer);
        }
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs args)
    {
        var eventName = args.RoutingKey;
        var message = Encoding.UTF8.GetString(args.Body.Span);

        try
        {
            if (!_eventHandlers.TryGetValue(eventName, out var handlerTypes))
            {
                _consumerChannel?.BasicNack(args.DeliveryTag, false, false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();

            foreach (var handlerType in handlerTypes)
            {
                var handler = scope.ServiceProvider.GetRequiredService(handlerType);
                var integrationEvent = JsonConvert.DeserializeObject(message, GetEventType(eventName));

                if (integrationEvent is null) continue;

                var handleMethod = handlerType.GetMethod("HandleAsync",
                    [integrationEvent.GetType(), typeof(CancellationToken)]);

                if (handleMethod is not null)
                {
                    await (Task)handleMethod.Invoke(handler,
                        [integrationEvent, CancellationToken.None])!;
                }
            }

            _consumerChannel?.BasicAck(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {EventName}: {Message}", eventName, message);
            _consumerChannel?.BasicNack(args.DeliveryTag, false, requeue: true);
        }
    }

    private static string GetEventName<T>() =>
        typeof(T).Name;

    private static Type GetEventType(string eventName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == eventName &&
                                 typeof(IntegrationEvent).IsAssignableFrom(t))
        ?? throw new InvalidOperationException($"Event type '{eventName}' not found");

    public async ValueTask DisposeAsync()
    {
        _consumerChannel?.Close();
        _consumerChannel?.Dispose();
        _consumerChannel = null;
        await _connection.DisposeAsync();
    }
}
