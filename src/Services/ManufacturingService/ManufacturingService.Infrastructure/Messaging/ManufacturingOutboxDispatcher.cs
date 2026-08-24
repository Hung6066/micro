using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public sealed class ManufacturingOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ManufacturingOutboxDispatcher> logger) : BackgroundService
{
    private const string Exchange = "his-hope.manufacturing";

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
                await DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Manufacturing outbox dispatch cycle failed; retrying.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var message = await db.OutboxMessages
            .Where(x => x.Status == "Pending")
            .OrderBy(x => x.OccurredOn)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null) return;

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
                Password = configuration.GetValue("EventBus:Password", "admin"),
                DispatchConsumersAsync = true
            };
            using var connection = factoryOptions.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = message.Type;
            channel.BasicPublish(Exchange, message.Type, properties, Encoding.UTF8.GetBytes(message.Content));

            message.Status = "Completed";
            message.ProcessedOn = DateTime.UtcNow;
            message.Error = null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Published manufacturing outbox message {MessageId} as {Type}.", message.Id, message.Type);
        }
        catch (Exception ex)
        {
            message.Status = "Pending";
            message.Error = ex.Message[..Math.Min(ex.Message.Length, 1000)];
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Could not publish manufacturing outbox message {MessageId}; it remains pending.", message.Id);
        }
    }
}
