namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// Persistence contract for saga instance state.
/// Implementations store and retrieve saga execution state from a backing
/// store (e.g., EF Core + CockroachDB).
/// </summary>
public interface ISagaStateStore
{
    /// <summary>
    /// Persists a new saga instance or updates an existing one.
    /// </summary>
    Task SaveAsync(SagaInstance instance, CancellationToken ct = default);

    /// <summary>
    /// Loads a saga instance by its unique identifier.
    /// Returns null if no saga with the specified ID exists.
    /// </summary>
    Task<SagaInstance?> LoadAsync(Guid sagaId, CancellationToken ct = default);

    /// <summary>
    /// Updates the status, step index, and heartbeat of a saga instance in a single operation.
    /// Used by <see cref="PersistentSagaOrchestrator{TData}"/> during step transitions.
    /// </summary>
    Task UpdateStatusAsync(Guid sagaId, string status, int stepIndex, DateTime heartbeat, CancellationToken ct = default);

    /// <summary>
    /// Finds all saga instances whose last heartbeat is older than the specified threshold.
    /// These are candidates for recovery by <see cref="SagaRecoveryService"/>.
    /// </summary>
    Task<List<SagaInstance>> GetStaleAsync(TimeSpan staleThreshold, CancellationToken ct = default);
}
