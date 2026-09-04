using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Commerce;
using His.Hope.Contracts.Messaging;
using His.Hope.Infrastructure.Messaging;
using His.Hope.Infrastructure.Saga;
using His.Hope.Messaging;
using His.Hope.ManufacturingService.Infrastructure.Saga;
using His.Hope.SharedKernel.Protocol;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

public sealed class CommerceOrderConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CommerceOrderConsumer> logger) : BackgroundService
{
    private const string Exchange = CommerceMessagingContract.ManufacturingExchange;
    private const string DeadLetterExchange = HisHopeProtocolConstants.Messaging.DeadLetterExchange;
    private const string Queue = "manufacturing.commerce-orders.v1";
    private const string RoutingKey = CommerceMessagingContract.OrderPlacedRoutingKey;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Consumers:CommerceOrdersEnabled", false))
        {
            logger.LogInformation("Commerce order consumer is disabled");
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = configuration["EventBus:HostName"] ?? "rabbitmq",
            Port = configuration.GetValue("EventBus:Port", 5672),
            UserName = configuration["EventBus:UserName"] ?? "admin",
            Password = EventBusSecurity.GetPassword(configuration),
            DispatchConsumersAsync = true,
        };

        using var connection = factory.CreateConnection();
        using var probeChannel = connection.CreateModel();
        var queueExists = true;
        try
        {
            probeChannel.QueueDeclarePassive(Queue);
        }
        catch (OperationInterruptedException)
        {
            queueExists = false;
        }

        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false);
        if (queueExists)
        {
            // Preserve an existing queue during rolling deployment; its DLX is
            // migrated by broker policy without deleting messages.
            channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false);
            logger.LogInformation("Existing queue {Queue} preserved during rolling deployment; broker DLX policy must remain configured", Queue);
        }
        else
        {
            channel.QueueDeclare(
                Queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = $"dlq.{RoutingKey}",
                });
        }
        channel.QueueBind(Queue, Exchange, RoutingKey);
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            await Task.Yield();
            InboxDeliveryGuard? delivery = null;
            try
            {
                var content = Encoding.UTF8.GetString(args.Body.ToArray());
                IntegrationEventTransportHeaders.Validate(
                    args.BasicProperties.Headers,
                    CommerceMessagingContract.OrderPlacedRoutingKey);
                var order = JsonSerializer.Deserialize<CommerceOrderPlacedV1>(content, JsonOptions)
                    ?? throw new InvalidOperationException("commerce_order_event_empty");
                if (order.EventId == Guid.Empty || order.OrderId == Guid.Empty)
                    throw new InvalidOperationException("commerce_order_event_identity_missing");

                using var scope = scopeFactory.CreateScope();
                delivery = await InboxDeliveryGuard.TryBeginAsync(
                    scope.ServiceProvider.GetRequiredService<IInboxStore>(), order.EventId, Queue, stoppingToken);
                if (delivery is null)
                {
                    channel.BasicAck(args.DeliveryTag, multiple: false);
                    return;
                }
                var saga = scope.ServiceProvider
                    .GetRequiredService<PersistentSagaOrchestrator<CommerceOrderFulfillmentSagaData>>();
                await saga.ExecuteAsync(
                    order.OrderId,
                    new CommerceOrderFulfillmentSagaData(order),
                    new SagaExecutionMetadata(
                        order.TenantKey,
                        order.CorrelationId ?? order.EventId.ToString("D"),
                        order.CausationId,
                        $"{CommerceMessagingContract.OrderPlacedRoutingKey}:{order.OrderId:D}"),
                    stoppingToken);

                await delivery.CompleteAsync(stoppingToken);
                channel.BasicAck(args.DeliveryTag, multiple: false);
                logger.LogInformation("Commerce order {OrderId} fulfillment saga accepted", order.OrderId);
            }
            catch (JsonException ex)
            {
                if (delivery is not null)
                    await delivery.DisposeAsync();
                logger.LogWarning(ex, "Dropping malformed Commerce order event");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (InvalidOperationException ex)
            {
                if (delivery is not null)
                    await delivery.DisposeAsync();
                logger.LogWarning(ex, "Dropping invalid Commerce order event");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                if (delivery is not null)
                    await delivery.DisposeAsync();
                logger.LogError(ex, "Failed to process Commerce order event");
                // The queue is DLX-backed; never requeue indefinitely and create a hot loop.
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(Queue, autoAck: false, consumer);
        logger.LogInformation("Commerce order consumer listening on {Queue}", Queue);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
