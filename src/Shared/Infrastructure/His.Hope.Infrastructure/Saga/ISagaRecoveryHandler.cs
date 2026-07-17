namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// Non-generic interface for saga recovery.
/// Each saga type registers a handler so the <see cref="SagaRecoveryService"/>
/// can resume or compensate sagas without knowing their data type at compile time.
/// </summary>
public interface ISagaRecoveryHandler
{
    /// <summary>
    /// The saga type identifier stored in <see cref="SagaInstance.SagaType"/>.
    /// Must match the value written by <see cref="PersistentSagaOrchestrator{TData}"/>.
    /// </summary>
    string SagaType { get; }

    /// <summary>
    /// Resume or compensate the given saga instance.
    /// Called after the distributed lock has been acquired.
    /// </summary>
    Task ResumeAsync(SagaInstance instance, CancellationToken ct = default);
}
