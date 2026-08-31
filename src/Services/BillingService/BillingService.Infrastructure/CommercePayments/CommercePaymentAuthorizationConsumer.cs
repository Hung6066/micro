using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Saga;
using His.Hope.Infrastructure.Messaging;
using His.Hope.SharedKernel.Protocol;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.BillingService.Infrastructure.CommercePayments;

public sealed class CommercePaymentAuthorizationConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CommercePaymentAuthorizationConsumer> logger) : BackgroundService
{
    private const string Queue = "billing.commerce-payment-authorization.v1";
    private const string DeadLetterExchange = HisHopeProtocolConstants.Messaging.DeadLetterExchange;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Consumers:CommercePaymentEnabled", false))
        {
            logger.LogInformation("Commerce payment consumer is disabled");
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
        using var probeChannel = connection.CreateModel();
        var queueExists = true;
        try { probeChannel.QueueDeclarePassive(Queue); }
        catch (OperationInterruptedException) { queueExists = false; }

        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(SagaMessagingContract.PaymentExchange, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false);
        if (queueExists)
            channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false);
        else
            channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = $"dlq.{SagaMessagingContract.PaymentAuthorizationRequested}",
                });
        channel.QueueBind(Queue, SagaMessagingContract.PaymentExchange, SagaMessagingContract.PaymentAuthorizationRequested);
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var request = JsonSerializer.Deserialize<PaymentAuthorizationRequestedV1>(
                    Encoding.UTF8.GetString(args.Body.ToArray()), JsonOptions)
                    ?? throw new InvalidOperationException("payment_authorization_request_empty");
                if (request.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(request.TenantKey) ||
                    string.IsNullOrWhiteSpace(request.IdempotencyKey))
                    throw new InvalidOperationException("payment_authorization_request_identity_missing");

                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CommercePaymentWorkflow>()
                    .AuthorizeAsync(request, stoppingToken);
                channel.BasicAck(args.DeliveryTag, multiple: false);
                logger.LogInformation("Commerce payment authorization accepted for order {OrderId}", request.OrderId);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Dropping malformed Commerce payment request");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Rejecting Commerce payment request");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Commerce payment request failed");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(Queue, autoAck: false, consumer);
        logger.LogInformation("Commerce payment consumer listening on {Queue}", Queue);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
