using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// EF Core implementation of <see cref="ISagaStateStore"/> backed by <see cref="SagaDbContext"/>.
/// Provides ACID-compliant persistence of saga instances to CockroachDB.
/// </summary>
public sealed class EfSagaStateStore : ISagaStateStore, IAsyncDisposable
{
    private readonly IDbContextFactory<SagaDbContext> _contextFactory;
    private readonly ILogger<EfSagaStateStore> _logger;

    public EfSagaStateStore(
        IDbContextFactory<SagaDbContext> contextFactory,
        ILogger<EfSagaStateStore> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task SaveAsync(SagaInstance instance, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var current = await context.SagaInstances
            .SingleOrDefaultAsync(x => x.SagaId == instance.SagaId, ct);
        if (current is null)
        {
            context.SagaInstances.Add(instance);
        }
        else
        {
            current.SagaType = instance.SagaType;
            current.TenantKey = instance.TenantKey;
            current.CorrelationId = instance.CorrelationId;
            current.CausationId = instance.CausationId;
            current.IdempotencyKey = instance.IdempotencyKey;
            current.Status = instance.Status;
            current.StepIndex = instance.StepIndex;
            current.RetryCount = instance.RetryCount;
            // Version is maintained by the transition update below. Do not
            // overwrite it with a stale detached instance during final save.
            current.Data = instance.Data;
            current.ErrorMessage = instance.ErrorMessage;
            current.StartedAt = instance.StartedAt;
            current.CompletedAt = instance.CompletedAt;
            current.LastHeartbeat = instance.LastHeartbeat;
            current.UpdatedAt = instance.UpdatedAt;
            current.Version++;
        }
        await context.SaveChangesAsync(ct);
        _logger.LogDebug("Saved saga {SagaId} ({SagaType}) with status {Status}",
            instance.SagaId, instance.SagaType, instance.Status);
    }

    public async Task<SagaInstance?> LoadAsync(Guid sagaId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.SagaInstances
            .AsNoTracking()
            .TagWith("Saga.State.Load")
            .FirstOrDefaultAsync(s => s.SagaId == sagaId, ct);
    }

    public async Task UpdateStatusAsync(
        Guid sagaId,
        string status,
        int stepIndex,
        DateTime heartbeat,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.SagaInstances.Where(s => s.SagaId == sagaId);
        var rows = stepIndex >= 0
            ? await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, status)
                .SetProperty(s => s.StepIndex, stepIndex)
                .SetProperty(s => s.LastHeartbeat, heartbeat)
                .SetProperty(s => s.UpdatedAt, heartbeat)
                .SetProperty(s => s.Version, s => s.Version + 1), ct)
            : await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, status)
                .SetProperty(s => s.LastHeartbeat, heartbeat)
                .SetProperty(s => s.UpdatedAt, heartbeat)
                .SetProperty(s => s.Version, s => s.Version + 1), ct);

        if (rows == 0)
        {
            _logger.LogWarning(
                "UpdateStatusAsync: No saga found with SagaId {SagaId}", sagaId);
        }
    }

    public async Task<List<SagaInstance>> GetStaleAsync(
        TimeSpan staleThreshold,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - staleThreshold;

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.SagaInstances
            .AsNoTracking()
            .TagWith("Saga.Recovery.GetStale")
            .Where(s => s.LastHeartbeat < cutoff)
            .Where(s => s.Status == SagaStatus.Running || s.Status == SagaStatus.Compensating)
            .OrderBy(s => s.LastHeartbeat)
            .ToListAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        // IDbContextFactory handles its own cleanup;
        // no resources to dispose at this level.
        await ValueTask.CompletedTask;
    }
}
