using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Channels;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class DatabaseAuditBackgroundService : BackgroundService
{
    private static readonly Meter Meter = new("His.Hope.Identity.Audit");
    private static readonly Counter<long> WritesTotal = Meter.CreateCounter<long>("audit_writes_total", description: "Total audit events written to DB");
    private static readonly Counter<long> LossTotal = Meter.CreateCounter<long>("audit_loss_total", description: "Audit events lost after retries exhausted");
    private long _dlqSize;

    private const int BatchSize = 10;

    private readonly Channel<PhiAuditEntry> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<DatabaseAuditBackgroundService> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public DatabaseAuditBackgroundService(
        Channel<PhiAuditEntry> channel,
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer? redis,
        ILogger<DatabaseAuditBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;

        Meter.CreateObservableGauge("audit_dlq_size", () => _dlqSize, description: "Dead-letter queue size");

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Audit DB write attempt {Attempt}/{MaxRetries} failed: {Message}",
                        args.AttemptNumber + 1, 3,
                        args.Outcome.Exception?.Message);
                    return default;
                }
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<PhiAuditEntry>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();

                while (batch.Count < BatchSize)
                {
                    if (batch.Count == 0)
                    {
                        var entry = await _channel.Reader.ReadAsync(stoppingToken);
                        batch.Add(entry);
                    }
                    else
                    {
                        if (_channel.Reader.TryRead(out var entry))
                            batch.Add(entry);
                        else
                            break;
                    }
                }

                await ProcessBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in audit background processor");
            }
        }

        await DrainRemainingAsync();
    }

    private async Task ProcessBatchAsync(List<PhiAuditEntry> batch, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        foreach (var entry in batch)
        {
            var success = false;
            try
            {
                    await _retryPipeline.ExecuteAsync(
                        async cancel =>
                        {
                            var auditLog = new Domain.Entities.AuditLog
                            {
                                Id = Guid.NewGuid(),
                                UserId = entry.UserId,
                                UserName = null,
                                Action = entry.Action,
                                ResourceType = entry.ResourceType,
                                ResourceId = entry.ResourceId,
                                Details = $"{entry.HttpMethod} {entry.Path}",
                                IpAddress = entry.ClientIp,
                                UserAgent = entry.UserAgent,
                                Timestamp = entry.Timestamp
                            };

                            db.AuditLogs.Add(auditLog);
                            await db.SaveChangesAsync(cancel);
                        },
                        ct);

                WritesTotal.Add(1);
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Audit event permanently failed for {ResourceType}/{ResourceId} after retries exhausted. Entry: {@Entry}",
                    entry.ResourceType, entry.ResourceId, entry);
            }

            if (!success)
            {
                LossTotal.Add(1);
                await TryPushToDeadLetterAsync(entry);
            }
        }

        _dlqSize = await TryGetDlqSizeAsync();
    }

    private async Task TryPushToDeadLetterAsync(PhiAuditEntry entry)
    {
        if (_redis is null)
            return;

        try
        {
            var db = _redis.GetDatabase();
            var key = $"his_hope:audit_dlq:{DateTime.UtcNow:yyyy-MM-dd}";
            var json = JsonSerializer.Serialize(entry);
            await db.ListRightPushAsync(key, json);
            await db.KeyExpireAsync(key, TimeSpan.FromDays(7));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push audit entry to Redis DLQ for {ResourceType}/{ResourceId}",
                entry.ResourceType, entry.ResourceId);
        }
    }

    private async Task<long> TryGetDlqSizeAsync()
    {
        if (_redis is null)
            return 0;

        try
        {
            var db = _redis.GetDatabase();
            var key = $"his_hope:audit_dlq:{DateTime.UtcNow:yyyy-MM-dd}";
            return await db.ListLengthAsync(key);
        }
        catch
        {
            return 0;
        }
    }

    private async Task DrainRemainingAsync()
    {
        _channel.Writer.TryComplete();

        var remaining = new List<PhiAuditEntry>();
        while (_channel.Reader.TryRead(out var entry))
            remaining.Add(entry);

        if (remaining.Count > 0)
        {
            _logger.LogInformation("Draining {Count} remaining audit entries before shutdown", remaining.Count);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

                foreach (var entry in remaining)
                {
                    db.AuditLogs.Add(new Domain.Entities.AuditLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = entry.UserId,
                        Action = entry.Action,
                        ResourceType = entry.ResourceType,
                        ResourceId = entry.ResourceId,
                        Details = $"{entry.HttpMethod} {entry.Path}",
                        IpAddress = entry.ClientIp,
                        UserAgent = entry.UserAgent,
                        Timestamp = entry.Timestamp
                    });
                }

                await db.SaveChangesAsync(CancellationToken.None);
                WritesTotal.Add(remaining.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to drain {Count} remaining audit entries during shutdown", remaining.Count);
            }
        }
    }
}
