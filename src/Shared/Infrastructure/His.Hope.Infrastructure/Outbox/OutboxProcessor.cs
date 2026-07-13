using System.Data;
using His.Hope.EventBus.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace His.Hope.Infrastructure.Outbox;

public class OutboxProcessor<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor<TDbContext>> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly int _batchSize = 50;
    private readonly int _maxRetries = 3;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started for {DbContext}", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxStatus.Pending &&
                       (m.LockExpiresAt == null || m.LockExpiresAt < DateTime.UtcNow))
            .OrderBy(m => m.OccurredOn)
            .Take(_batchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            message.Status = OutboxStatus.Processing;
            message.LockExpiresAt = DateTime.UtcNow.AddMinutes(1);
        }

        await context.SaveChangesAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type);
                if (eventType is null)
                {
                    message.Status = OutboxStatus.Skipped;
                    message.Error = $"Type '{message.Type}' not found";
                    continue;
                }

                var @event = JsonConvert.DeserializeObject(
                    message.Content, eventType,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All,
                    }) as IntegrationEvent;

                if (@event is null)
                {
                    message.Status = OutboxStatus.Skipped;
                    message.Error = "Deserialized event is null";
                    continue;
                }

                var publishMethod = typeof(IEventBus).GetMethod("PublishAsync")!
                    .MakeGenericMethod(eventType);

                await (Task)publishMethod.Invoke(eventBus, [@event, ct])!;

                message.Status = OutboxStatus.Completed;
                message.ProcessedOn = DateTime.UtcNow;

                _logger.LogDebug("Outbox message {Id} processed: {Type}", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastRetryOn = DateTime.UtcNow;
                message.Error = ex.ToString();

                if (message.RetryCount >= _maxRetries)
                {
                    message.Status = OutboxStatus.Failed;
                    _logger.LogError(ex, "Outbox message {Id} failed after {Retries} retries",
                        message.Id, _maxRetries);
                }
                else
                {
                    message.Status = OutboxStatus.Pending;
                    message.LockExpiresAt = null;
                    _logger.LogWarning(ex, "Outbox message {Id} retry {Retry}/{MaxRetries}",
                        message.Id, message.RetryCount, _maxRetries);
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
