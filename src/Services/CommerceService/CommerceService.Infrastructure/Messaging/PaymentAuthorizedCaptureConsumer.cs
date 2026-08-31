using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Saga;
using His.Hope.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace His.Hope.CommerceService.Infrastructure.Messaging;

/// <summary>Advances the fulfillment payment after Billing authorization.</summary>
public sealed class PaymentAuthorizedCaptureConsumer(
    IConfiguration configuration,
    ILogger<PaymentAuthorizedCaptureConsumer> logger) : BackgroundService
{
    private const string Queue = "commerce.payment-authorized-capture.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Consumers:PaymentAuthorizedCaptureEnabled", false)) return;

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
        channel.ExchangeDeclare(SagaMessagingContract.PaymentExchange, ExchangeType.Topic, true, false);
        channel.QueueDeclare(Queue, true, false, false);
        channel.QueueBind(Queue, SagaMessagingContract.PaymentExchange, SagaMessagingContract.PaymentAuthorized);
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            await Task.Yield();
            try
            {
                var authorized = JsonSerializer.Deserialize<PaymentResultV1>(
                    Encoding.UTF8.GetString(args.Body.ToArray()), JsonOptions)
                    ?? throw new InvalidOperationException("payment_authorized_event_empty");
                if (authorized.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(authorized.TenantKey) ||
                    string.IsNullOrWhiteSpace(authorized.PaymentId))
                    throw new InvalidOperationException("payment_authorized_event_identity_missing");

                var capture = new PaymentCaptureRequestedV1(
                    Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion, DateTimeOffset.UtcNow,
                    authorized.OrderId, authorized.TenantKey, authorized.PaymentId, authorized.Amount,
                    authorized.Currency, $"commerce-capture:{authorized.OrderId:D}",
                    authorized.CorrelationId, authorized.CausationId ?? authorized.EventId.ToString("D"));
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.Type = SagaMessagingContract.PaymentCaptureRequested;
                properties.MessageId = capture.EventId.ToString("D");
                channel.BasicPublish(SagaMessagingContract.PaymentExchange,
                    SagaMessagingContract.PaymentCaptureRequested, properties,
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(capture)));
                channel.BasicAck(args.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not advance authorized payment to capture");
                channel.BasicNack(args.DeliveryTag, false, false);
            }
        };
        channel.BasicConsume(Queue, false, consumer);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
