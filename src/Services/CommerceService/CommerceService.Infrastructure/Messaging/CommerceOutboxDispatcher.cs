using His.Hope.Contracts.Commerce;
using His.Hope.Contracts.Saga;
using His.Hope.CommerceService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace His.Hope.CommerceService.Infrastructure.Messaging;

public sealed partial class CommerceOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CommerceOutboxDispatcher> logger) : BackgroundService
{
    private const string Exchange = CommerceMessagingContract.ManufacturingExchange;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Outbox:Enabled", false))
        {
            LogDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var dispatched = await DispatchPendingAsync(stoppingToken);
                if (!dispatched)
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogCycleFailed(logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<bool> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<ICommerceDbContextFactory>();
        var dispatchedAny = false;
        foreach (var connectionName in dbFactory.GetRegisteredConnectionNames())
        {
            if (await DispatchPendingForConnectionAsync(dbFactory, connectionName, cancellationToken))
                dispatchedAny = true;
        }

        return dispatchedAny;
    }

    private async Task<bool> DispatchPendingForConnectionAsync(
        ICommerceDbContextFactory dbFactory,
        string connectionName,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextForConnectionAsync(connectionName, cancellationToken);
        var staleProcessingBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var message = await db.OutboxMessages
            .Where(x => x.Status == OutboxStatus.Pending ||
                (x.Status == "Processing" && x.ProcessedOn == null && x.OccurredAt < staleProcessingBefore))
            .OrderBy(x => x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null) return false;

        if (message.Status == "Processing")
            LogRecoveringStale(logger, message.Id);
        message.Status = "Processing";
        message.RetryCount++;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = configuration.GetValue("EventBus:HostName", "rabbitmq"),
                Port = configuration.GetValue("EventBus:Port", 5672),
                UserName = configuration.GetValue("EventBus:UserName", "admin"),
                Password = GetRequiredEventBusPassword(configuration),
                DispatchConsumersAsync = true,
            };
            using var connection = connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            channel.ExchangeDeclare(SagaMessagingContract.PaymentExchange, ExchangeType.Topic, durable: true, autoDelete: false);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = message.Type;
            properties.MessageId = message.Id.ToString();
            var targetExchange = message.Type is SagaMessagingContract.PaymentAuthorized or
                SagaMessagingContract.PaymentCaptured or SagaMessagingContract.PaymentRefunded
                ? SagaMessagingContract.PaymentExchange
                : message.Type is SagaMessagingContract.ShipmentCreated or SagaMessagingContract.ShipmentDispatched or SagaMessagingContract.ShipmentDelivered
                    ? SagaMessagingContract.ShipmentExchange
                    : Exchange;
            channel.BasicPublish(targetExchange, message.Type, properties, System.Text.Encoding.UTF8.GetBytes(message.Content));

            message.Status = OutboxStatus.Completed;
            message.ProcessedOn = DateTimeOffset.UtcNow;
            message.Error = null;
            await db.SaveChangesAsync(cancellationToken);
            LogPublished(logger, message.Id, message.Type);
            return true;
        }
        catch (Exception ex)
        {
            message.Status = OutboxStatus.Pending;
            message.Error = ex.Message[..Math.Min(ex.Message.Length, 1000)];
            await db.SaveChangesAsync(cancellationToken);
            LogPublishFailed(logger, ex, message.Id);
            return true;
        }
    }

    private static string GetRequiredEventBusPassword(IConfiguration configuration)
    {
        var password = configuration["EventBus:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "EventBus:Password must be supplied by the runtime secret provider.");
        }

        var environment = configuration["HIS_HOPE_ENVIRONMENT"];
        if (environment is "staging" or "production" &&
            string.Equals(password, "admin", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "EventBus:Password must not use the development default in staging or production.");
        }

        return password;
    }

    [LoggerMessage(EventId = 4401, Level = LogLevel.Information, Message = "Commerce outbox dispatcher is disabled by configuration.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 4402, Level = LogLevel.Warning, Message = "Commerce outbox dispatch cycle failed; retrying.")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4403, Level = LogLevel.Warning, Message = "Recovering stale Commerce outbox message {MessageId} after an interrupted publish.")]
    private static partial void LogRecoveringStale(ILogger logger, Guid messageId);

    [LoggerMessage(EventId = 4404, Level = LogLevel.Information, Message = "Published Commerce outbox message {MessageId} as {MessageType}.")]
    private static partial void LogPublished(ILogger logger, Guid messageId, string messageType);

    [LoggerMessage(EventId = 4405, Level = LogLevel.Warning, Message = "Could not publish Commerce outbox message {MessageId}; it remains pending.")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, Guid messageId);
}
