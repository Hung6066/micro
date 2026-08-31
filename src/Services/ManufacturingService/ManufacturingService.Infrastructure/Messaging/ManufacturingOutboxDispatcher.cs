using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public sealed class ManufacturingOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ManufacturingOutboxDispatcher> logger) : BackgroundService
{
    private const string Exchange = His.Hope.Contracts.Commerce.CommerceMessagingContract.ManufacturingExchange;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Outbox:Enabled", true))
        {
            logger.LogInformation("Manufacturing outbox dispatcher is disabled by configuration.");
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
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Manufacturing outbox dispatch cycle failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<bool> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IManufacturingDbContextFactory>();
        var dispatchedAny = false;
        foreach (var connectionName in dbFactory.GetRegisteredConnectionNames())
        {
            if (await DispatchPendingForConnectionAsync(dbFactory, connectionName, cancellationToken))
                dispatchedAny = true;
        }

        return dispatchedAny;
    }

    private async Task<bool> DispatchPendingForConnectionAsync(
        IManufacturingDbContextFactory dbFactory,
        string connectionName,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextForConnectionAsync(connectionName, cancellationToken);
        var message = await db.OutboxMessages
            .Where(x => x.Status == ManufacturingStatusCodes.Pending)
            .OrderBy(x => x.OccurredOn)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null) return false;

        message.Status = "Processing";
        message.RetryCount++;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var factoryOptions = new ConnectionFactory
            {
                HostName = configuration.GetValue("EventBus:HostName", "rabbitmq"),
                Port = configuration.GetValue("EventBus:Port", 5672),
                UserName = configuration.GetValue("EventBus:UserName", "admin"),
                Password = EventBusSecurity.GetPassword(configuration),
                DispatchConsumersAsync = true
            };
            using var connection = factoryOptions.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = message.Type;
            channel.BasicPublish(Exchange, message.Type, properties, System.Text.Encoding.UTF8.GetBytes(message.Content));

            message.Status = ManufacturingStatusCodes.Completed;
            message.ProcessedOn = DateTime.UtcNow;
            message.Error = null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Published manufacturing outbox message {MessageId} as {Type} from {ConnectionName}.",
                message.Id,
                message.Type,
                connectionName);
        }
        catch (Exception ex)
        {
            message.Status = ManufacturingStatusCodes.Pending;
            message.Error = ex.Message[..Math.Min(ex.Message.Length, 1000)];
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                ex,
                "Could not publish manufacturing outbox message {MessageId} from {ConnectionName}; it remains pending.",
                message.Id,
                connectionName);
        }

        return true;
    }
}
