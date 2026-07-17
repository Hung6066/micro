using His.Hope.Infrastructure.Locking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// Background service that periodically scans for stale saga instances
/// (heartbeat older than the stale threshold) and attempts to recover them.
///
/// Recovery strategy:
///   - Running sagas:  resume execution from the next uncompleted step.
///   - Compensating sagas: resume compensation from where it left off.
///
/// The service acquires a distributed lock before recovering each saga to
/// prevent duplicate recovery across multiple instances.
///
/// Configuration:
///   - CheckInterval: how often to scan (default 30s).
///   - StaleThreshold: heartbeat age that triggers recovery (default 60s).
/// </summary>
public sealed class SagaRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SagaRecoveryService> _logger;

    /// <summary>How often to scan for stale sagas. Default: 30 seconds.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Heartbeat age threshold for considering a saga stale.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>TTL for recovery lock. Default: 5 minutes.</summary>
    public TimeSpan RecoveryLockTtl { get; set; } = TimeSpan.FromMinutes(5);

    public SagaRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<SagaRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SagaRecoveryService started. CheckInterval={CheckInterval}, StaleThreshold={StaleThreshold}",
            CheckInterval, StaleThreshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
                await RecoverStaleSagasAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error in SagaRecoveryService. Continuing...");
            }
        }

        _logger.LogInformation("SagaRecoveryService stopped.");
    }

    private async Task RecoverStaleSagasAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var stateStore = scope.ServiceProvider.GetRequiredService<ISagaStateStore>();
        var lockManager = scope.ServiceProvider.GetRequiredService<ILockManager>();
        var handlers = scope.ServiceProvider.GetServices<ISagaRecoveryHandler>();
        var handlerMap = handlers.ToDictionary(h => h.SagaType);

        if (handlerMap.Count == 0)
        {
            _logger.LogDebug("No saga recovery handlers registered. Skipping scan.");
            return;
        }

        var staleSagas = await stateStore.GetStaleAsync(StaleThreshold, ct);

        if (staleSagas.Count == 0)
            return;

        _logger.LogWarning(
            "Found {Count} stale saga(s) to recover.", staleSagas.Count);

        foreach (var saga in staleSagas)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await RecoverSagaAsync(saga, stateStore, lockManager, handlerMap, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to recover saga {SagaId} ({SagaType})",
                    saga.SagaId, saga.SagaType);
            }
        }
    }

    private async Task RecoverSagaAsync(
        SagaInstance saga,
        ISagaStateStore stateStore,
        ILockManager lockManager,
        Dictionary<string, ISagaRecoveryHandler> handlerMap,
        CancellationToken ct)
    {
        // ── Find handler for this saga type ──────────────────────────────────
        if (!handlerMap.TryGetValue(saga.SagaType, out var handler))
        {
            _logger.LogWarning(
                "No recovery handler registered for saga type '{SagaType}' " +
                "(saga {SagaId}). Skipping.",
                saga.SagaType, saga.SagaId);
            return;
        }

        // ── Acquire distributed lock ─────────────────────────────────────────
        await using var lockHandle = await lockManager.AcquireAsync(
            $"saga:{saga.SagaId}", RecoveryLockTtl, ct);

        if (lockHandle is null)
        {
            _logger.LogDebug(
                "Could not acquire lock for saga {SagaId}. " +
                "Another instance may be handling it.", saga.SagaId);
            return;
        }

        // ── Re-load saga to avoid TOCTOU race ────────────────────────────────
        var current = await stateStore.LoadAsync(saga.SagaId, ct);
        if (current is null)
        {
            _logger.LogWarning(
                "Saga {SagaId} disappeared before recovery could start.", saga.SagaId);
            return;
        }

        // ── Double-check staleness ───────────────────────────────────────────
        if (current.LastHeartbeat > DateTime.UtcNow - StaleThreshold)
        {
            _logger.LogDebug(
                "Saga {SagaId} is no longer stale (heartbeat updated). Skipping.",
                saga.SagaId);
            return;
        }

        // ── Attempt recovery ─────────────────────────────────────────────────
        _logger.LogWarning(
            "Recovering saga {SagaId} ({SagaType}) from status '{Status}' " +
            "at step {StepIndex}.",
            current.SagaId, current.SagaType, current.Status, current.StepIndex);

        try
        {
            await handler.ResumeAsync(current, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Recovery of saga {SagaId} failed. Manual intervention may be required.",
                current.SagaId);
        }
    }
}
