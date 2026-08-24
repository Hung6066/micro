using System.Text;
using His.Hope.CommerceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace His.Hope.CommerceService.Infrastructure.Messaging;

public sealed class CommerceOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CommerceOutboxDispatcher> logger) : BackgroundService
{
    private const string Exchange = "his-hope.manufacturing";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Outbox:Enabled", false))
        {
            logger.LogInformation("Commerce outbox dispatcher is disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Commerce outbox dispatch cycle failed; retrying.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CommerceDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var staleProcessingBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var message = await db.OutboxMessages
            .Where(x => x.Status == "Pending" ||
                (x.Status == "Processing" && x.ProcessedOn == null && x.OccurredAt < staleProcessingBefore))
            .OrderBy(x => x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null) return;

        if (message.Status == "Processing")
            logger.LogWarning("Recovering stale Commerce outbox message {MessageId} after an interrupted publish.", message.Id);
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
                Password = configuration.GetValue("EventBus:Password", "admin"),
                DispatchConsumersAsync = true,
            };
            using var connection = connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = message.Type;
            properties.MessageId = message.Id.ToString();
            channel.BasicPublish(Exchange, message.Type, properties, Encoding.UTF8.GetBytes(message.Content));

            message.Status = "Completed";
            message.ProcessedOn = DateTimeOffset.UtcNow;
            message.Error = null;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Published Commerce outbox message {MessageId} as {Type}.", message.Id, message.Type);
        }
        catch (Exception ex)
        {
            message.Status = "Pending";
            message.Error = ex.Message[..Math.Min(ex.Message.Length, 1000)];
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Could not publish Commerce outbox message {MessageId}; it remains pending.", message.Id);
        }
    }
}
