using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Saga;
using His.Hope.Contracts.Messaging;
using His.Hope.Infrastructure.Messaging;
using His.Hope.Messaging;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
namespace His.Hope.CommerceService.Infrastructure.Messaging;

public sealed partial class PaymentCapturedShipmentConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PaymentCapturedShipmentConsumer> logger) : BackgroundService
{
    private const string Queue = "commerce.payment-captured-shipment.v1";
    private const string DeadLetterExchange = HisHopeProtocolConstants.Messaging.DeadLetterExchange;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Consumers:PaymentCapturedShipmentEnabled", false))
        {
            LogDisabled(logger);
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = configuration.GetValue("EventBus:HostName", "rabbitmq"),
            Port = configuration.GetValue("EventBus:Port", 5672),
            UserName = configuration.GetValue("EventBus:UserName", "admin"),
            Password = EventBusSecurity.GetPassword(configuration),
            DispatchConsumersAsync = true,
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(SagaMessagingContract.PaymentExchange, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = $"dlq.{SagaMessagingContract.PaymentCaptured}",
        });
        channel.QueueBind(Queue, SagaMessagingContract.PaymentExchange, SagaMessagingContract.PaymentCaptured);
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            InboxDeliveryGuard? delivery = null;
            try
            {
                IntegrationEventTransportHeaders.Validate(
                    args.BasicProperties.Headers,
                    SagaMessagingContract.PaymentCaptured);
                var payment = JsonSerializer.Deserialize<PaymentResultV1>(Encoding.UTF8.GetString(args.Body.ToArray()), JsonOptions)
                    ?? throw new InvalidOperationException("payment_captured_event_empty");
                if (payment.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(payment.TenantKey) || string.IsNullOrWhiteSpace(payment.PaymentId))
                    throw new InvalidOperationException("payment_captured_event_identity_missing");

                var request = new ShipmentRequestedV1(
                    Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion, DateTimeOffset.UtcNow,
                    payment.OrderId, payment.TenantKey, string.Empty,
                    $"commerce-shipment:{payment.OrderId:D}", payment.CorrelationId, payment.CausationId);
                using var scope = scopeFactory.CreateScope();
                delivery = await InboxDeliveryGuard.TryBeginAsync(
                    scope.ServiceProvider.GetRequiredService<IInboxStore>(), payment.EventId, Queue, stoppingToken);
                if (delivery is null)
                {
                    channel.BasicAck(args.DeliveryTag, multiple: false);
                    return;
                }
                var workflow = scope.ServiceProvider.GetRequiredService<CommerceShipmentWorkflow>();
                var shipment = await workflow.CreateAsync(request, stoppingToken);
                await workflow.DispatchAsync(request with { ShipmentId = shipment.ProviderShipmentId ?? string.Empty }, stoppingToken);
                await delivery.CompleteAsync(stoppingToken);
                channel.BasicAck(args.DeliveryTag, multiple: false);
                LogAccepted(logger, payment.OrderId);
            }
            catch (JsonException ex)
            {
                if (delivery is not null)
                    await delivery.DisposeAsync();
                LogMalformed(logger, ex);
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (InvalidOperationException ex)
            {
                if (delivery is not null)
                    await delivery.DisposeAsync();
                LogRejected(logger, ex);
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                if (delivery is not null)
                    await delivery.DisposeAsync();
                LogFailed(logger, ex);
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
        };
        channel.BasicConsume(Queue, autoAck: false, consumer);
        LogListening(logger, Queue);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    [LoggerMessage(EventId = 4501, Level = LogLevel.Information, Message = "Payment captured shipment consumer is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 4502, Level = LogLevel.Information, Message = "Shipment workflow accepted for order {OrderId}.")]
    private static partial void LogAccepted(ILogger logger, Guid orderId);

    [LoggerMessage(EventId = 4503, Level = LogLevel.Warning, Message = "Dropping malformed PaymentCaptured event.")]
    private static partial void LogMalformed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4504, Level = LogLevel.Warning, Message = "Rejecting PaymentCaptured event for shipment workflow.")]
    private static partial void LogRejected(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4505, Level = LogLevel.Error, Message = "PaymentCaptured shipment workflow failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4506, Level = LogLevel.Information, Message = "Payment captured shipment consumer listening on {Queue}.")]
    private static partial void LogListening(ILogger logger, string queue);
}
