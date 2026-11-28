using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Saga;
using His.Hope.Contracts.Messaging;
using His.Hope.Infrastructure.Messaging;
using His.Hope.Messaging;
using His.Hope.SharedKernel.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace His.Hope.BillingService.Infrastructure.CommercePayments;

/// <summary>Consumes the capture/refund commands that complete or compensate payment.</summary>
public sealed class CommercePaymentCommandConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CommercePaymentCommandConsumer> logger) : BackgroundService
{
    private const string Queue = "billing.commerce-payment-commands.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Consumers:CommercePaymentEnabled", false)) return;

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
        channel.ExchangeDeclare(HisHopeProtocolConstants.Messaging.DeadLetterExchange, ExchangeType.Topic, true, false);
        var queueExists = true;
        try { channel.QueueDeclarePassive(Queue); }
        catch (OperationInterruptedException) { queueExists = false; }
        if (queueExists)
            channel.QueueDeclare(Queue, true, false, false);
        else
            channel.QueueDeclare(Queue, true, false, false, new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = HisHopeProtocolConstants.Messaging.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = "dlq.billing.commerce-payment-commands.v1"
            });
        channel.QueueBind(Queue, SagaMessagingContract.PaymentExchange, SagaMessagingContract.PaymentCaptureRequested);
        channel.QueueBind(Queue, SagaMessagingContract.PaymentExchange, SagaMessagingContract.PaymentRefundRequested);
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            InboxDeliveryGuard? delivery = null;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var workflow = scope.ServiceProvider.GetRequiredService<CommercePaymentWorkflow>();
                var json = Encoding.UTF8.GetString(args.Body.ToArray());
                IntegrationEventTransportHeaders.Validate(args.BasicProperties.Headers, args.RoutingKey);
                if (args.RoutingKey == SagaMessagingContract.PaymentCaptureRequested)
                {
                    var request = JsonSerializer.Deserialize<PaymentCaptureRequestedV1>(json, JsonOptions)
                        ?? throw new InvalidOperationException("payment_capture_request_empty");
                    delivery = await InboxDeliveryGuard.TryBeginAsync(
                        scope.ServiceProvider.GetRequiredService<IInboxStore>(), request.EventId, Queue, stoppingToken);
                    if (delivery is null)
                    {
                        channel.BasicAck(args.DeliveryTag, false);
                        return;
                    }
                    await workflow.CaptureAsync(new PaymentResultV1(request.EventId, request.SchemaVersion,
                        request.OccurredAt, request.OrderId, request.TenantKey, request.PaymentId,
                        request.Amount, request.Currency, request.IdempotencyKey,
                        CorrelationId: request.CorrelationId, CausationId: request.CausationId), stoppingToken);
                }
                else
                {
                    var request = JsonSerializer.Deserialize<PaymentRefundRequestedV1>(json, JsonOptions)
                        ?? throw new InvalidOperationException("payment_refund_request_empty");
                    delivery = await InboxDeliveryGuard.TryBeginAsync(
                        scope.ServiceProvider.GetRequiredService<IInboxStore>(), request.EventId, Queue, stoppingToken);
                    if (delivery is null)
                    {
                        channel.BasicAck(args.DeliveryTag, false);
                        return;
                    }
                    await workflow.RefundAsync(new PaymentResultV1(request.EventId, request.SchemaVersion,
                        request.OccurredAt, request.OrderId, request.TenantKey, request.PaymentId,
                        request.Amount, request.Currency, request.IdempotencyKey,
                        CorrelationId: request.CorrelationId, CausationId: request.CausationId), stoppingToken);
                }
                await delivery.CompleteAsync(stoppingToken);
                channel.BasicAck(args.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                if (delivery is not null)
                    await delivery.DisposeAsync();
                logger.LogError(ex, "Commerce payment command failed for routing key {RoutingKey}", args.RoutingKey);
                channel.BasicNack(args.DeliveryTag, false, false);
            }
        };
        channel.BasicConsume(Queue, false, consumer);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
