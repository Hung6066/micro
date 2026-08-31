using System.Text;
using His.Hope.Contracts.Saga;
using His.Hope.Infrastructure.Messaging;
using His.Hope.Infrastructure.Outbox;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace His.Hope.BillingService.Infrastructure.CommercePayments;

public sealed class CommercePaymentOutboxHandler(IConfiguration configuration) : IOutboxMessageHandler
{
    public bool CanHandle(string messageType) =>
        messageType is SagaMessagingContract.PaymentAuthorized or
            SagaMessagingContract.PaymentCaptured or
            SagaMessagingContract.PaymentRefunded;

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = message.Type;
        properties.MessageId = message.Id.ToString("D");
        channel.BasicPublish(SagaMessagingContract.PaymentExchange, message.Type, properties,
            Encoding.UTF8.GetBytes(message.Content));
        return Task.CompletedTask;
    }
}
