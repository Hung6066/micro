using System.Data;
using System.Diagnostics;
using His.Hope.EventBus.Abstractions;
using His.Hope.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Diagnostics.Metrics;

namespace His.Hope.Infrastructure.Outbox;

public class OutboxProcessor<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor<TDbContext>> _logger;
    private readonly EventTypeRegistry _eventTypeRegistry;
    private readonly OutboxOptions _options;
    private readonly IConfiguration _configuration;
    private readonly string _workerId = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
    private int _pendingIndexInitialized;
    private static readonly Meter Meter = new("His.Hope.Outbox", "1.0.0");
    private static readonly Counter<long> ClaimedCounter = Meter.CreateCounter<long>("his_hope_outbox_claimed");
    private static readonly Counter<long> CompletedCounter = Meter.CreateCounter<long>("his_hope_outbox_completed");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("his_hope_outbox_failed");
    private static readonly Histogram<double> PublishDuration = Meter.CreateHistogram<double>("his_hope_outbox_publish_duration_ms");

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor<TDbContext>> logger,
        EventTypeRegistry eventTypeRegistry,
        IOptions<OutboxOptions> options,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _eventTypeRegistry = eventTypeRegistry;
        _options = options.Value;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started for {DbContext}", typeof(TDbContext).Name);

        var workers = Enumerable.Range(0, _options.WorkerCount)
            .Select(index => WorkerLoopAsync(index, stoppingToken))
            .ToArray();
        await Task.WhenAll(workers);
    }

    private async Task WorkerLoopAsync(int workerIndex, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages in worker {WorkerIndex}", workerIndex);
            }

            await Task.Delay(_options.PollingIntervalMilliseconds, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var eventMapper = scope.ServiceProvider.GetRequiredService<IIntegrationEventMapper>();

        await EnsurePendingIndexAsync(context, ct);

        var candidates = await context.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxStatus.Pending &&
                       (m.LockExpiresAt == null || m.LockExpiresAt < DateTime.UtcNow) &&
                       (m.NextAttemptAt == null || m.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(m => m.OccurredOn)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        var messages = new List<OutboxMessage>();
        foreach (var candidate in candidates)
        {
            var claimedUntil = DateTime.UtcNow.AddSeconds(_options.ClaimLeaseSeconds);
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
                ClaimedCounter.Add(1);
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

                var domainEvent = JsonConvert.DeserializeObject(
                    message.Content, eventType,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All,
                    }) as His.Hope.SharedKernel.Domain.Common.IDomainEvent;

                if (domainEvent is null)
                {
                    throw new InvalidOperationException("Outbox payload is not a domain event.");
                }

                var @event = eventMapper.Map(domainEvent)
                    ?? throw new InvalidOperationException($"No integration event mapping registered for '{eventType.FullName}'.");

                var publishMethod = typeof(IEventBus).GetMethod("PublishAsync")!
                    .MakeGenericMethod(@event.GetType());

                var started = Stopwatch.GetTimestamp();
                await (Task)publishMethod.Invoke(eventBus, [@event, ct])!;
                PublishDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);

                message.Status = OutboxStatus.Completed;
                message.ProcessedOn = DateTime.UtcNow;

                _logger.LogDebug("Outbox message {Id} processed: {Type}", message.Id, message.Type);
                CompletedCounter.Add(1);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                FailedCounter.Add(1);
                message.LastRetryOn = DateTime.UtcNow;
                message.Error = ex.ToString();

                if (message.RetryCount >= _options.MaxRetries)
                {
                    message.Status = OutboxStatus.DeadLetter;
                    message.DeadLetteredOn = DateTime.UtcNow;
                    _logger.LogError(ex, "Outbox message {Id} failed after {Retries} retries",
                        message.Id, _options.MaxRetries);
                }
                else
                {
                    message.Status = OutboxStatus.Pending;
                    message.LockExpiresAt = null;
                    message.ClaimedBy = null;
                    message.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, message.RetryCount));
                    _logger.LogWarning(ex, "Outbox message {Id} retry {Retry}/{MaxRetries}",
                        message.Id, message.RetryCount, _options.MaxRetries);
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private async Task EnsurePendingIndexAsync(TDbContext context, CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _pendingIndexInitialized, 1) == 1)
            return;

        // Production runtime roles are intentionally not table owners. The
        // deployer/migration job owns indexes; workers must never attempt DDL.
        if (!_configuration.GetValue("Persistence:RunMigrationsOnStartup", false))
            return;

        if (!context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            return;

        var entityType = context.Model.FindEntityType(typeof(OutboxMessage));
        var tableName = entityType?.GetTableName();
        if (entityType is null || string.IsNullOrWhiteSpace(tableName))
            return;

        var table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        var status = entityType.FindProperty(nameof(OutboxMessage.Status))?.GetColumnName(table);
        var nextAttempt = entityType.FindProperty(nameof(OutboxMessage.NextAttemptAt))?.GetColumnName(table);
        var lockExpires = entityType.FindProperty(nameof(OutboxMessage.LockExpiresAt))?.GetColumnName(table);
        var occurred = entityType.FindProperty(nameof(OutboxMessage.OccurredOn))?.GetColumnName(table);
        if (new[] { status, nextAttempt, lockExpires, occurred }.Any(string.IsNullOrWhiteSpace))
            return;

        var schema = entityType.GetSchema();
        var qualifiedTable = string.IsNullOrWhiteSpace(schema)
            ? QuoteIdentifier(tableName)
            : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";
        var indexName = $"ix_{tableName}_pending_dispatch";
        var sql = $"CREATE INDEX IF NOT EXISTS {QuoteIdentifier(indexName)} ON {qualifiedTable} " +
                  $"({QuoteIdentifier(status!)}, {QuoteIdentifier(nextAttempt!)}, {QuoteIdentifier(lockExpires!)}, {QuoteIdentifier(occurred!)}) " +
                  $"WHERE {QuoteIdentifier(status!)} = 'Pending'";

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to ensure outbox pending index for {Table}; migrations must create it in production", tableName);
        }
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

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
