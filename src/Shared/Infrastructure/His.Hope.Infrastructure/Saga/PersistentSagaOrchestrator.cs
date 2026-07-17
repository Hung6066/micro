using System.Diagnostics;
using His.Hope.Infrastructure.Locking;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// Persistent saga orchestrator with DB-backed state, heartbeat monitoring,
/// per-step timeout enforcement, distributed locking, and automatic compensation.
///
/// Wraps the existing <see cref="ISagaStep{TData}"/> pattern with durable
/// persistence so that long-running sagas survive process restarts.
///
/// Lifecycle:
///   Pending → Running → (step loop) → Completed
///                                  ↘ Failed → Compensating → Compensated
///
/// Timeout: each step must complete within <see cref="PerStepTimeout"/> (default 30s).
///          On timeout the orchestrator cancels the step and starts compensation.
///
/// Heartbeat: written every 5s during step execution so <see cref="SagaRecoveryService"/>
///            can detect crashes (stale heartbeat &gt; 60s).
/// </summary>
public sealed class PersistentSagaOrchestrator<TData>
{
    private readonly List<ISagaStep<TData>> _steps = [];
    private readonly ISagaStateStore _stateStore;
    private readonly ILockManager _lockManager;
    private readonly ILogger<PersistentSagaOrchestrator<TData>> _logger;

    /// <summary>
    /// Maximum time allowed per step execution (including compensation).
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan PerStepTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Interval between heartbeat writes during saga execution.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// TTL for the distributed lock acquired during saga execution.
    /// Must be longer than the maximum expected saga duration.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan LockTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Creates a new persistent saga orchestrator.
    /// </summary>
    public PersistentSagaOrchestrator(
        ISagaStateStore stateStore,
        ILockManager lockManager,
        ILogger<PersistentSagaOrchestrator<TData>> logger)
    {
        _stateStore = stateStore;
        _lockManager = lockManager;
        _logger = logger;
    }

    /// <summary>
    /// Adds a step to the saga pipeline. Steps execute in registration order.
    /// </summary>
    public PersistentSagaOrchestrator<TData> AddStep(ISagaStep<TData> step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Executes the saga with a generated saga ID (no idempotency check).
    /// </summary>
    public Task<Guid> ExecuteAsync(
        TData data,
        CancellationToken ct = default) =>
        ExecuteAsync(Guid.NewGuid(), data, ct);

    /// <summary>
    /// Executes the saga with the specified <paramref name="sagaId"/>.
    /// If a saga with this ID already exists (and is not Failed/Compensated),
    /// the call is idempotent — returns the existing saga ID without re-executing.
    ///
    /// Acquires a distributed lock on <c>saga:{sagaId}</c> to prevent
    /// concurrent execution of the same saga.
    /// </summary>
    public async Task<Guid> ExecuteAsync(
        Guid sagaId,
        TData data,
        CancellationToken ct = default)
    {
        // ── Idempotency check ────────────────────────────────────────────────
        var existing = await _stateStore.LoadAsync(sagaId, ct);
        if (existing is not null)
        {
            if (existing.Status is "Completed" or "Compensated")
            {
                _logger.LogInformation(
                    "Saga {SagaId} already in terminal state '{Status}'. Skipping.",
                    sagaId, existing.Status);
                return sagaId;
            }

            // Stale Running/Compensating saga — the caller should not be
            // attempting to re-run without recovery. We still proceed below
            // (the lock will arbitrate).
            _logger.LogWarning(
                "Saga {SagaId} found with non-terminal status '{Status}'. " +
                "Acquiring lock to attempt execution.",
                sagaId, existing.Status);
        }

        // ── Distributed lock ─────────────────────────────────────────────────
        await using var lockHandle = await _lockManager.AcquireAsync(
            $"saga:{sagaId}", LockTtl, ct);

        if (lockHandle is null)
        {
            throw new SagaLockAcquisitionException(
                $"Failed to acquire distributed lock for saga {sagaId}. " +
                "Another instance may be executing this saga.");
        }

        // ── Initialise saga instance ─────────────────────────────────────────
        var instance = existing ?? new SagaInstance
        {
            SagaId = sagaId,
            SagaType = typeof(TData).FullName ?? typeof(TData).Name,
            Status = "Pending",
            StepIndex = -1,
            StartedAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };
        instance.SetData(data);
        instance.Status = "Running";
        instance.ErrorMessage = null;

        if (existing is null)
        {
            await _stateStore.SaveAsync(instance, ct);
        }
        else
        {
            await _stateStore.UpdateStatusAsync(
                sagaId, "Running", instance.StepIndex, DateTime.UtcNow, ct);
        }

        // ── Execute steps ────────────────────────────────────────────────────
        var executedStepIndices = new Stack<int>(capacity: _steps.Count);

        try
        {
            for (int i = Math.Max(0, instance.StepIndex + 1); i < _steps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "Executing saga step {Step}/{Total} for saga {SagaId}: {StepType}",
                    i + 1, _steps.Count, sagaId, _steps[i].GetType().Name);

                await ExecuteStepWithTimeoutAsync(sagaId, i, data, ct);

                executedStepIndices.Push(i);

                // Persist progress after each successful step
                await _stateStore.UpdateStatusAsync(
                    sagaId, "Running", i, DateTime.UtcNow, ct);
            }

            // ── Mark completed ───────────────────────────────────────────────
            instance.Status = "Completed";
            instance.CompletedAt = DateTime.UtcNow;
            await SaveFinalStateAsync(instance, ct);

            _logger.LogInformation("Saga {SagaId} completed successfully.", sagaId);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Per-step timeout occurred
            await FailAndCompensateAsync(instance, executedStepIndices, data,
                new SagaStepTimeoutException(
                    $"Step {instance.StepIndex + 1}/{_steps.Count} timed out after " +
                    $"{PerStepTimeout.TotalSeconds}s"), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailAndCompensateAsync(instance, executedStepIndices, data, ex, ct);
        }

        return sagaId;
    }

    /// <summary>
    /// Attempts to resume or compensate a saga found by the recovery service.
    /// This is called by <see cref="SagaRecoveryService"/> with the lock already held.
    /// </summary>
    internal async Task ResumeAsync(
        SagaInstance instance,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Resuming saga {SagaId} from status '{Status}' step {StepIndex}",
            instance.SagaId, instance.Status, instance.StepIndex);

        if (instance.Status == "Running")
        {
            // Resume execution from the next step
            var data = instance.GetData<TData>();
            var executedStepIndices = new Stack<int>(capacity: _steps.Count);

            try
            {
                for (int i = instance.StepIndex + 1; i < _steps.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    await ExecuteStepWithTimeoutAsync(instance.SagaId, i, data, ct);
                    executedStepIndices.Push(i);

                    await _stateStore.UpdateStatusAsync(
                        instance.SagaId, "Running", i, DateTime.UtcNow, ct);
                }

                instance.Status = "Completed";
                instance.CompletedAt = DateTime.UtcNow;
                instance.ErrorMessage = null;
                await SaveFinalStateAsync(instance, ct);

                _logger.LogInformation(
                    "Saga {SagaId} completed after recovery.", instance.SagaId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await FailAndCompensateAsync(instance, executedStepIndices, data, ex, ct);
            }
        }
        else if (instance.Status == "Compensating")
        {
            // Resume compensation from where it left off
            var data = instance.GetData<TData>();
            await ExecuteCompensationAsync(instance, data, ct);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task ExecuteStepWithTimeoutAsync(
        Guid sagaId,
        int stepIndex,
        TData data,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(PerStepTimeout);

        var step = _steps[stepIndex];

        // Run heartbeat in background alongside the step
        var heartbeatTask = RunHeartbeatAsync(sagaId, timeoutCts.Token);

        try
        {
            await step.ExecuteAsync(data, timeoutCts.Token);
        }
        finally
        {
            // Signal heartbeat to stop
            timeoutCts.Cancel();
            try { await heartbeatTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task RunHeartbeatAsync(Guid sagaId, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(HeartbeatInterval, ct);
                await _stateStore.UpdateStatusAsync(
                    sagaId, "Running", -1, DateTime.UtcNow, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on step completion
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Heartbeat update failed for saga {SagaId}. Continuing...", sagaId);
        }
    }

    private async Task FailAndCompensateAsync(
        SagaInstance instance,
        Stack<int> executedStepIndices,
        TData data,
        Exception exception,
        CancellationToken ct)
    {
        _logger.LogError(exception,
            "Saga {SagaId} failed at step {StepIndex}. Starting compensation.",
            instance.SagaId, instance.StepIndex + 2); // +2 because we haven't persisted the failed step yet

        instance.Status = "Failed";
        instance.ErrorMessage = exception.ToString();
        await SaveFinalStateAsync(instance, ct);

        instance.Status = "Compensating";
        instance.StepIndex = instance.StepIndex; // keep current
        await _stateStore.UpdateStatusAsync(
            instance.SagaId, "Compensating", instance.StepIndex, DateTime.UtcNow, ct);

        await ExecuteCompensationAsync(instance, data, ct);
    }

    private async Task ExecuteCompensationAsync(
        SagaInstance instance,
        TData data,
        CancellationToken ct)
    {
        var succeeded = true;

        for (int i = instance.StepIndex; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation(
                    "Compensating step {Step} for saga {SagaId}: {StepType}",
                    i + 1, instance.SagaId, _steps[i].GetType().Name);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(PerStepTimeout);

                await _steps[i].CompensateAsync(data, timeoutCts.Token);

                instance.StepIndex = i - 1;
                await _stateStore.UpdateStatusAsync(
                    instance.SagaId, "Compensating", instance.StepIndex, DateTime.UtcNow, ct);
            }
            catch (Exception ex)
            {
                succeeded = false;
                _logger.LogCritical(ex,
                    "Compensation failed for step {Step} of saga {SagaId}. " +
                    "Manual intervention required.",
                    i + 1, instance.SagaId);
            }
        }

        instance.Status = succeeded ? "Compensated" : "Failed";
        instance.CompletedAt = DateTime.UtcNow;
        if (!succeeded)
        {
            instance.ErrorMessage = "Compensation completed with errors. Manual intervention required.";
        }
        await SaveFinalStateAsync(instance, ct);
    }

    private async Task SaveFinalStateAsync(SagaInstance instance, CancellationToken ct)
    {
        // Use SaveAsync to upsert the final state including Data updates
        // We need a load-then-save pattern since we're using a factory-based context
        var existing = await _stateStore.LoadAsync(instance.SagaId, ct);
        if (existing is not null)
        {
            existing.Status = instance.Status;
            existing.CompletedAt = instance.CompletedAt;
            existing.ErrorMessage = instance.ErrorMessage;
            existing.StepIndex = instance.StepIndex;
            existing.LastHeartbeat = DateTime.UtcNow;
            await _stateStore.SaveAsync(existing, ct);
        }
    }
}

/// <summary>
/// Thrown when a distributed lock cannot be acquired for a saga instance.
/// </summary>
public sealed class SagaLockAcquisitionException : Exception
{
    public SagaLockAcquisitionException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a saga step exceeds its per-step timeout.
/// </summary>
public sealed class SagaStepTimeoutException : Exception
{
    public SagaStepTimeoutException(string message) : base(message) { }
    public SagaStepTimeoutException(string message, Exception inner) : base(message, inner) { }
}
