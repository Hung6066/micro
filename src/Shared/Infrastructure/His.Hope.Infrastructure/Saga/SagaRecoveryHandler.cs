namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// Generic adapter that wraps a <see cref="PersistentSagaOrchestrator{TData}"/>
/// and exposes it as a non-generic <see cref="ISagaRecoveryHandler"/> for the
/// <see cref="SagaRecoveryService"/>.
/// </summary>
public sealed class SagaRecoveryHandler<TData> : ISagaRecoveryHandler
{
    private readonly PersistentSagaOrchestrator<TData> _orchestrator;

    public SagaRecoveryHandler(PersistentSagaOrchestrator<TData> orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// The fully-qualified type name of TData, matching the saga_type written
    /// by <see cref="PersistentSagaOrchestrator{TData}"/>.
    /// </summary>
    public string SagaType => typeof(TData).FullName ?? typeof(TData).Name;

    /// <summary>
    /// Delegates recovery to <see cref="PersistentSagaOrchestrator{TData}.ResumeAsync"/>.
    /// </summary>
    public Task ResumeAsync(SagaInstance instance, CancellationToken ct = default) =>
        _orchestrator.ResumeAsync(instance, ct);
}
