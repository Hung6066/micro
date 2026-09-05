using System.Text;
using His.Hope.Contracts.Saga;
using His.Hope.Contracts.Messaging;
using His.Hope.Infrastructure.Messaging;
using His.Hope.ContentService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace His.Hope.ContentService.Infrastructure.Messaging;

public sealed class ContentPublishingOutboxDispatcher(IServiceScopeFactory scopeFactory, IConfiguration configuration) : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Outbox:ContentPublishingEnabled", false)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { if (!await DispatchOneAsync(stoppingToken)) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
        }
    }

    private async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IContentDbContextFactory>();
        foreach (var connectionName in factory.GetRegisteredConnectionNames())
        {
            await using var db = await factory.CreateDbContextForConnectionAsync(connectionName, ct);
            var message = await ContentPublishingOutboxClaiming.ClaimAsync(db, ct);
            if (message is null) continue;
        try
        {
            var connection = new ConnectionFactory { HostName = configuration["EventBus:HostName"] ?? "rabbitmq", Port = configuration.GetValue("EventBus:Port", 5672), UserName = configuration["EventBus:UserName"] ?? "admin", Password = EventBusSecurity.GetPassword(configuration) }.CreateConnection();
            using (connection)
            using (var channel = connection.CreateModel())
            {
                channel.ExchangeDeclare(SagaMessagingContract.ContentExchange, ExchangeType.Topic, true, false);
                var properties = channel.CreateBasicProperties(); properties.Persistent = true; properties.ContentType = "application/json"; properties.Type = message.Type; properties.MessageId = message.Id.ToString();
                properties.Headers = IntegrationEventTransportHeaders.Create(message.Type, message.Content, audience: "content");
                channel.BasicPublish(SagaMessagingContract.ContentExchange, message.Type, properties, Encoding.UTF8.GetBytes(message.Content));
            }
        }
        catch (Exception exception)
        {
            message.LeaseUntil = null;
            message.LastError = exception.Message[..Math.Min(exception.Message.Length, 2000)];
            await db.SaveChangesAsync(ct);
            throw;
        }
            message.ProcessedOn = DateTimeOffset.UtcNow;
            message.LeaseUntil = null;
            await db.SaveChangesAsync(ct);
            return true;
        }
        return false;
    }
}
