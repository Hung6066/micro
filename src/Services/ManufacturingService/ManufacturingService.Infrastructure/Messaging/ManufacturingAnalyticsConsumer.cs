using System.Text;
using System.Text.Json;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class ManufacturingAnalyticsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ManufacturingAnalyticsConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Consumers:AnalyticsEnabled", true))
        {
            logger.LogInformation("Manufacturing analytics consumer is disabled");
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = configuration["EventBus:HostName"] ?? "rabbitmq",
            Port = configuration.GetValue("EventBus:Port", 5672),
            UserName = configuration["EventBus:UserName"] ?? "admin",
            Password = configuration["EventBus:Password"] ?? "admin",
            DispatchConsumersAsync = false
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        const string exchange = "his-hope.manufacturing";
        const string queue = "manufacturing.analytics.v1";
        channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(queue, exchange, "Manufacturing.#");
        channel.BasicQos(0, 10, false);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (_, args) =>
        {
            try
            {
                var content = Encoding.UTF8.GetString(args.Body.ToArray());
                using var document = JsonDocument.Parse(content);
                var aggregate = document.RootElement.TryGetProperty("transformationId", out var transformation)
                    ? transformation
                    : document.RootElement.TryGetProperty("inspectionId", out var inspection)
                        ? inspection
                        : document.RootElement.TryGetProperty("lotId", out var lot)
                            ? lot
                            : default;
                if (aggregate.ValueKind == JsonValueKind.Undefined)
                    throw new ManufacturingEventValidationException("event_missing_aggregate_id");

                if (aggregate.ValueKind != JsonValueKind.String)
                    throw new ManufacturingEventValidationException("event_aggregate_id_not_string");

                var aggregateId = aggregate.GetString();
                if (string.IsNullOrWhiteSpace(aggregateId))
                    throw new ManufacturingEventValidationException("event_empty_aggregate_id");

                var tenantKey = document.RootElement.TryGetProperty("tenantKey", out var tenantElement) &&
                                tenantElement.ValueKind == JsonValueKind.String
                    ? tenantElement.GetString()
                    : null;

                using var scope = scopeFactory.CreateScope();
                var dbFactory = scope.ServiceProvider.GetRequiredService<IManufacturingDbContextFactory>();
                var eventType = args.BasicProperties.Type ?? "Manufacturing.Unknown";
                var persisted = PersistReceipt(dbFactory, tenantKey, eventType, aggregateId, content);
                if (persisted)
                    logger.LogInformation("Manufacturing event receipt persisted: {EventType}/{AggregateId}", eventType, aggregateId);

                channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Dropping malformed manufacturing event payload");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (ManufacturingEventValidationException ex)
            {
                logger.LogWarning(ex, "Dropping invalid manufacturing event payload: {Reason}", ex.Message);
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to consume manufacturing event");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
        };

        channel.BasicConsume(queue, autoAck: false, consumer);
        logger.LogInformation("Manufacturing analytics consumer listening on {Queue}", queue);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static bool PersistReceipt(
        IManufacturingDbContextFactory dbFactory,
        string? tenantKey,
        string eventType,
        string aggregateId,
        string content)
    {
        if (!string.IsNullOrWhiteSpace(tenantKey))
            return PersistReceiptForConnection(dbFactory.CreateDbContext(tenantKey), eventType, aggregateId, content);

        foreach (var connectionName in dbFactory.GetRegisteredConnectionNames())
        {
            if (PersistReceiptForConnection(dbFactory.CreateDbContextForConnection(connectionName), eventType, aggregateId, content))
                return true;
        }

        return false;
    }

    private static bool PersistReceiptForConnection(
        ManufacturingDbContext db,
        string eventType,
        string aggregateId,
        string content)
    {
        using (db)
        {
            if (db.EventReceipts.Any(x => x.EventType == eventType && x.AggregateId == aggregateId))
                return false;

            db.EventReceipts.Add(new ManufacturingEventReceiptEntity
            {
                Id = Guid.NewGuid(), EventType = eventType, AggregateId = aggregateId,
                Content = content, ReceivedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            return true;
        }
    }
}

public sealed class ManufacturingEventValidationException(string message) : Exception(message);
