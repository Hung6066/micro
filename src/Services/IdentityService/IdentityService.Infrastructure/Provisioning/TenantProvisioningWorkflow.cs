using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Saga;
using His.Hope.Infrastructure.Messaging;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace His.Hope.IdentityService.Infrastructure.Provisioning;

public static class TenantProvisioningStates
{
    public const string Completed = "completed";
}

public sealed class TenantProvisioningEntity
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataRegion { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string State { get; set; } = TenantProvisioningStates.Completed;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TenantProvisioningWorkflow(IdentityDbContext db)
{
    public async Task ProvisionAsync(TenantProvisioningRequestedV1 request, CancellationToken ct)
    {
        var key = request.TenantKey.Trim().ToLowerInvariant();
        if (key.Length is < 2 or > 100 || string.IsNullOrWhiteSpace(request.DataRegion))
            throw new InvalidOperationException("tenant_provisioning_request_invalid");

        if (await db.Set<TenantProvisioningEntity>().AnyAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct))
            return;

        var scope = await db.IamScopes.SingleOrDefaultAsync(x => x.Key == key && x.Kind == "tenant", ct);
        if (scope is null)
        {
            scope = new Domain.Entities.IamScope { Key = key, DisplayName = request.DisplayName.Trim(), Kind = "tenant", IsActive = true };
            db.IamScopes.Add(scope);
        }
        else if (!scope.IsActive)
        {
            scope.IsActive = true;
        }

        var now = DateTime.UtcNow;
        db.Set<TenantProvisioningEntity>().Add(new TenantProvisioningEntity
        {
            Id = Guid.NewGuid(), TenantKey = key, DisplayName = request.DisplayName.Trim(),
            DataRegion = request.DataRegion.Trim(), IdempotencyKey = request.IdempotencyKey,
            State = TenantProvisioningStates.Completed, CreatedAt = now, UpdatedAt = now
        });
        var message = new TenantProvisionedV1(Guid.NewGuid(), SagaMessagingContract.CurrentSchemaVersion,
            DateTimeOffset.UtcNow, key, scope.Id, request.DataRegion.Trim(), request.IdempotencyKey,
            request.CorrelationId, request.CausationId);
        db.Set<TenantProvisioningOutboxEntity>().Add(new TenantProvisioningOutboxEntity
        {
            Id = Guid.NewGuid(), Type = SagaMessagingContract.TenantProvisioned,
            Content = JsonSerializer.Serialize(message), OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class TenantProvisioningOutboxEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedOn { get; set; }
}

public sealed class TenantProvisioningConsumer(IServiceScopeFactory scopeFactory, IConfiguration configuration) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Consumers:TenantProvisioningEnabled", false)) return Task.CompletedTask;
        return Task.Run(() => ConsumeAsync(stoppingToken), stoppingToken);
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = configuration["EventBus:HostName"] ?? "rabbitmq", Port = configuration.GetValue("EventBus:Port", 5672), UserName = configuration["EventBus:UserName"] ?? "admin", Password = EventBusSecurity.GetPassword(configuration) };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(SagaMessagingContract.IdentityExchange, ExchangeType.Topic, true, false);
        channel.QueueDeclare("identity.tenant-provisioning.v1", true, false, false);
        channel.QueueBind("identity.tenant-provisioning.v1", SagaMessagingContract.IdentityExchange, SagaMessagingContract.TenantProvisioningRequested);
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var request = JsonSerializer.Deserialize<TenantProvisioningRequestedV1>(args.Body.Span)
                    ?? throw new InvalidOperationException("tenant_provisioning_payload_invalid");
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<TenantProvisioningWorkflow>().ProvisionAsync(request, ct);
                channel.BasicAck(args.DeliveryTag, false);
            }
            catch { channel.BasicNack(args.DeliveryTag, false, false); }
        };
        channel.BasicConsume("identity.tenant-provisioning.v1", false, consumer);
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

}

public sealed class TenantProvisioningOutboxDispatcher(IServiceScopeFactory scopeFactory, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Outbox:TenantProvisioningEnabled", false)) return;
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
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var message = await db.TenantProvisioningOutbox.Where(x => x.ProcessedOn == null).OrderBy(x => x.OccurredAt).FirstOrDefaultAsync(ct);
        if (message is null) return false;
        var factory = new ConnectionFactory { HostName = configuration["EventBus:HostName"] ?? "rabbitmq", Port = configuration.GetValue("EventBus:Port", 5672), UserName = configuration["EventBus:UserName"] ?? "admin", Password = EventBusSecurity.GetPassword(configuration) };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(SagaMessagingContract.IdentityExchange, ExchangeType.Topic, true, false);
        var properties = channel.CreateBasicProperties(); properties.Persistent = true; properties.ContentType = "application/json"; properties.Type = message.Type; properties.MessageId = message.Id.ToString();
        channel.BasicPublish(SagaMessagingContract.IdentityExchange, message.Type, properties, Encoding.UTF8.GetBytes(message.Content));
        message.ProcessedOn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
