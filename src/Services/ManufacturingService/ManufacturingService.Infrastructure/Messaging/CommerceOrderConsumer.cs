using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Commerce;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class CommerceOrderConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CommerceOrderConsumer> logger) : BackgroundService
{
    private const string Exchange = "his-hope.manufacturing";
    private const string Queue = "manufacturing.commerce-orders.v1";
    private const string RoutingKey = "Commerce.OrderPlaced.v1";
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
            Password = configuration["EventBus:Password"] ?? "admin",
            DispatchConsumersAsync = true,
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(Queue, Exchange, RoutingKey);
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            await Task.Yield();
            try
            {
                var content = Encoding.UTF8.GetString(args.Body.ToArray());
                var order = JsonSerializer.Deserialize<CommerceOrderPlacedV1>(content, JsonOptions)
                    ?? throw new InvalidOperationException("commerce_order_event_empty");
                if (order.EventId == Guid.Empty || order.OrderId == Guid.Empty)
                    throw new InvalidOperationException("commerce_order_event_identity_missing");

                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<ManufacturingReservationStore>();
                var result = store.AllocateCommerceOrder(order);
                if (result.Error is not null)
                {
                    logger.LogWarning("Commerce order {OrderId} was not allocated: {Error}", order.OrderId, result.Error);
                    channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                channel.BasicAck(args.DeliveryTag, multiple: false);
                logger.LogInformation("Commerce order {OrderId} allocated across {LineCount} lines", order.OrderId, result.Allocations.Count);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Dropping malformed Commerce order event");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Dropping invalid Commerce order event");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process Commerce order event");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
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
