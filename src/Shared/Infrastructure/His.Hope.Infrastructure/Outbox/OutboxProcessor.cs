using System.Data;
using His.Hope.EventBus.Abstractions;
using His.Hope.Infrastructure.Events;
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
    private readonly EventTypeRegistry _eventTypeRegistry;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly int _batchSize = 50;
    private readonly int _maxRetries = 3;
    private readonly string _workerId = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor<TDbContext>> logger,
        EventTypeRegistry eventTypeRegistry)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _eventTypeRegistry = eventTypeRegistry;
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

        var candidates = await context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxStatus.Pending &&
                       (m.LockExpiresAt == null || m.LockExpiresAt < DateTime.UtcNow) &&
                       (m.NextAttemptAt == null || m.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(m => m.OccurredOn)
            .Take(_batchSize)
            .ToListAsync(ct);

        var messages = new List<OutboxMessage>();
        foreach (var candidate in candidates)
        {
            var claimedUntil = DateTime.UtcNow.AddMinutes(1);
            var claimed = await context.Set<OutboxMessage>()
                .Where(m => m.Id == candidate.Id &&
                            m.Status == OutboxStatus.Pending &&
                            (m.LockExpiresAt == null || m.LockExpiresAt < DateTime.UtcNow) &&
                            (m.NextAttemptAt == null || m.NextAttemptAt <= DateTime.UtcNow))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, OutboxStatus.Processing)
                    .SetProperty(m => m.LockExpiresAt, claimedUntil)
                    .SetProperty(m => m.ClaimedBy, _workerId), ct);
            if (claimed == 1)
            {
                candidate.Status = OutboxStatus.Processing;
                candidate.LockExpiresAt = claimedUntil;
                candidate.ClaimedBy = _workerId;
                messages.Add(candidate);
            }
        }

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var eventType = _eventTypeRegistry.Resolve(message.Type);
                if (eventType is null)
                {
                    message.Status = OutboxStatus.Skipped;
                    message.Error = $"Type '{message.Type}' not found by EventTypeRegistry";
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
                    message.Status = OutboxStatus.DeadLetter;
                    message.DeadLetteredOn = DateTime.UtcNow;
                    _logger.LogError(ex, "Outbox message {Id} failed after {Retries} retries",
                        message.Id, _maxRetries);
                }
                else
                {
                    message.Status = OutboxStatus.Pending;
                    message.LockExpiresAt = null;
                    message.ClaimedBy = null;
                    message.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, message.RetryCount));
                    _logger.LogWarning(ex, "Outbox message {Id} retry {Retry}/{MaxRetries}",
                        message.Id, message.RetryCount, _maxRetries);
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }

    public static Task<int> RedriveAsync(TDbContext context, Guid messageId, CancellationToken ct = default)
    {
        return context.Set<OutboxMessage>()
            .Where(m => m.Id == messageId && m.Status == OutboxStatus.DeadLetter)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxStatus.Pending)
                .SetProperty(m => m.RetryCount, 0)
                .SetProperty(m => m.Error, (string?)null)
                .SetProperty(m => m.DeadLetteredOn, (DateTime?)null)
                .SetProperty(m => m.LockExpiresAt, (DateTime?)null)
                .SetProperty(m => m.ClaimedBy, (string?)null)
                .SetProperty(m => m.NextAttemptAt, (DateTime?)null), ct);
    }
}
