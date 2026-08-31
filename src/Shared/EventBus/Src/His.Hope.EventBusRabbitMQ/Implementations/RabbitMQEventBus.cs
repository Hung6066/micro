using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using His.Hope.EventBus.Abstractions;
using His.Hope.EventBusRabbitMQ.Abstractions;
using His.Hope.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using His.Hope.SharedKernel.Protocol;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace His.Hope.EventBusRabbitMQ.Implementations;

public partial class RabbitMQEventBus : IEventBus, IExternalEventPublisher, IAsyncDisposable
{
    private readonly RabbitMQConnection _connection;
    private readonly EventBusOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly EventDeliveryPolicy _deliveryPolicy = EventDeliveryPolicy.Default;
    private readonly ConcurrentBag<IModel> _publisherChannels = new();
    private readonly SemaphoreSlim _publisherSlots;
    private IModel? _consumerChannel;
    private readonly Dictionary<string, List<Type>> _eventHandlers = new();
    private const string DlxExchangeName = HisHopeProtocolConstants.Messaging.DeadLetterExchange;
    private const int MaxRetryCount = 3;

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
        _publisherSlots = new SemaphoreSlim(
            Math.Clamp(options.PublisherChannelPoolSize, 1, 64));
        _retryPolicy = Policy.Handle<BrokerUnreachableException>()
            .WaitAndRetryAsync(options.RetryCount,
                retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));
    }

    public async Task PublishAsync<TIntegrationEvent>(TIntegrationEvent @event,
        CancellationToken cancellationToken = default)
        where TIntegrationEvent : IntegrationEvent
    {
        await PublishToExchangeAsync(
            @event,
            _options.ExchangeName,
            _options.ExchangeType,
            GetEventName<TIntegrationEvent>(),
            cancellationToken);
    }

    public Task PublishAsync<TIntegrationEvent>(
        TIntegrationEvent @event,
        string provider,
        CancellationToken cancellationToken = default)
        where TIntegrationEvent : IntegrationEvent
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("External provider is required.", nameof(provider));

        var providerKey = Regex.Replace(provider.Trim().ToLowerInvariant(), "[^a-z0-9._-]", "-");
        return PublishToExchangeAsync(
            @event,
            _options.ExternalExchangeName,
            _options.ExternalExchangeType,
            $"{providerKey}.{GetEventName<TIntegrationEvent>()}",
            cancellationToken);
    }

    private async Task PublishToExchangeAsync<TIntegrationEvent>(
        TIntegrationEvent @event,
        string exchangeName,
        string exchangeType,
        string routingKey,
        CancellationToken cancellationToken)
        where TIntegrationEvent : IntegrationEvent
    {
        if (@event.Id == Guid.Empty)
            throw new ArgumentException("Integration event id is required.", nameof(@event));
        if (@event.SchemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(@event), "Integration event schema version must be positive.");
        var serializedEvent = JsonConvert.SerializeObject(@event);
        if (Encoding.UTF8.GetByteCount(serializedEvent) > _deliveryPolicy.MaximumPayloadBytes)
            throw new ArgumentException("Integration event payload exceeds the configured limit.", nameof(@event));

        if (!_connection.IsConnected)
            await _connection.GetConnectionAsync();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            await _publisherSlots.WaitAsync(cancellationToken);
            IModel? channel = null;
            var reusable = false;
            try
            {
                if (!_publisherChannels.TryTake(out channel) || !channel.IsOpen)
                {
                    channel?.Dispose();
                    channel = (await _connection.GetConnectionAsync()).CreateModel();
                    channel.ConfirmSelect();
                }

                channel.ExchangeDeclare(exchangeName, exchangeType, durable: true);

            var eventName = GetEventName<TIntegrationEvent>();
            var message = serializedEvent;
            var body = Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = @event.Id.ToString();
            properties.Timestamp = new AmqpTimestamp(
                new DateTimeOffset(@event.CreationDate).ToUnixTimeSeconds());
            properties.Type = eventName;
            properties.Headers = new Dictionary<string, object>
            {
                [EventEnvelopeHeaders.SchemaVersion] = @event.SchemaVersion
            };
            if (!string.IsNullOrWhiteSpace(@event.CorrelationId))
                properties.Headers[EventEnvelopeHeaders.CorrelationId] = @event.CorrelationId;
            if (!string.IsNullOrWhiteSpace(@event.CausationId))
                properties.Headers[EventEnvelopeHeaders.CausationId] = @event.CausationId;
            if (@event.Headers is not null)
            {
                foreach (var header in @event.Headers)
                    if (!properties.Headers.ContainsKey(header.Key))
                        properties.Headers[header.Key] = header.Value;
            }

            var routingKey = eventName;
                channel.BasicPublish(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);
                channel.WaitForConfirmsOrDie(
                    TimeSpan.FromMilliseconds(Math.Clamp(
                        _options.PublisherConfirmTimeoutMilliseconds, 100, 60_000)));
                reusable = true;

                _logger.LogInformation("Published {EventName} {EventId}",
                    eventName, @event.Id);
            }
            finally
            {
                if (reusable && channel is { IsOpen: true })
                    _publisherChannels.Add(channel);
                else
                    channel?.Dispose();
                _publisherSlots.Release();
            }
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
            var queueArgs = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = DlxExchangeName,
                ["x-dead-letter-routing-key"] = $"dlq.{eventName}"
            };
            _consumerChannel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
            _consumerChannel.QueueBind(queueName, _options.ExchangeName, eventName);
        }

        // Declare DLX exchange (if not already declared by DeadLetterConsumer in another service)
        _consumerChannel.ExchangeDeclare(DlxExchangeName, "topic", durable: true);

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
                var integrationEvent = JsonConvert.DeserializeObject(message, GetEventType(eventName)) as IntegrationEvent;

                if (integrationEvent is null) continue;

                var inbox = scope.ServiceProvider.GetService<IInboxStore>();
                var consumer = handlerType.FullName ?? handlerType.Name;
                if (inbox is not null && !await inbox.TryBeginAsync(
                        integrationEvent.Id,
                        consumer,
                        CancellationToken.None))
                {
                    _logger.LogInformation(
                        "Skipping duplicate {EventName} {Consumer} {MessageId}",
                        eventName, consumer, args.BasicProperties.MessageId);
                    continue;
                }

                var handleMethod = handlerType.GetMethod("HandleAsync",
                    [integrationEvent.GetType(), typeof(CancellationToken)]);

                try
                {
                    if (handleMethod is not null)
                    {
                        await (Task)handleMethod.Invoke(handler,
                            [integrationEvent, CancellationToken.None])!;
                    }

                    if (inbox is not null)
                    {
                        await inbox.MarkCompletedAsync(
                            integrationEvent.Id,
                            consumer,
                            CancellationToken.None);
                    }
                }
                catch
                {
                    if (inbox is not null)
                    {
                        await inbox.ReleaseAsync(
                            integrationEvent.Id,
                            consumer,
                            CancellationToken.None);
                    }

                    throw;
                }
            }

            _consumerChannel?.BasicAck(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {EventName}: {Message}", eventName, message);

            var retryCount = 0;
            var existingHeaders = args.BasicProperties.Headers;
            if (existingHeaders is { } &&
                existingHeaders.TryGetValue("x-retry-count", out var val))
            {
                retryCount = Convert.ToInt32(val);
            }

            if (retryCount < MaxRetryCount)
            {
                // Republish message with incremented retry count, then ack original
                try
                {
                    using var channel = (await _connection.GetConnectionAsync()).CreateModel();

                    var newProps = channel.CreateBasicProperties();
                    newProps.Persistent = true;
                    newProps.MessageId = args.BasicProperties.MessageId;
                    newProps.Timestamp = args.BasicProperties.Timestamp;
                    newProps.Type = args.BasicProperties.Type;

                    // Copy existing headers and set/update retry count
                    newProps.Headers = new Dictionary<string, object>(existingHeaders ?? new Dictionary<string, object>())
                    {
                        ["x-retry-count"] = retryCount + 1
                    };

                    channel.BasicPublish(
                        exchange: args.Exchange,
                        routingKey: args.RoutingKey,
                        mandatory: true,
                        basicProperties: newProps,
                        body: args.Body);

                    _consumerChannel?.BasicAck(args.DeliveryTag, false);

                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetryCount} for {EventName} {MessageId}",
                        retryCount + 1, MaxRetryCount, eventName, args.BasicProperties.MessageId);
                }
                catch (Exception pubEx)
                {
                    _logger.LogError(pubEx,
                        "Failed to republish {EventName} for retry, sending to DLQ", eventName);
                    _consumerChannel?.BasicNack(args.DeliveryTag, false, requeue: false);
                }
            }
            else
            {
                _logger.LogError(
                    "Message {EventName} {MessageId} failed after {MaxRetryCount} retries, sending to DLQ",
                    eventName, args.BasicProperties.MessageId, MaxRetryCount);
                _consumerChannel?.BasicNack(args.DeliveryTag, false, requeue: false);
            }
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
        while (_publisherChannels.TryTake(out var channel))
            channel.Dispose();
        _publisherSlots.Dispose();
        await _connection.DisposeAsync();
    }
}
