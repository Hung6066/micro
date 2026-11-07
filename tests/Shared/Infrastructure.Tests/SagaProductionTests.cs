using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Saga;
using Microsoft.Extensions.Logging.Abstractions;

namespace His.Hope.Infrastructure.Tests;

public sealed class SagaProductionTests
{
    [Fact]
    public async Task Execute_PersistsMetadata_AndCompensatesEveryCompletedStepWhenLaterStepFails()
    {
        var store = new InMemorySagaStateStore();
        var first = new RecordingStep();
        var second = new RecordingStep(throwOnExecute: true);
        var orchestrator = new PersistentSagaOrchestrator<SagaData>(
            store,
            new InMemoryLockManager(),
            NullLogger<PersistentSagaOrchestrator<SagaData>>.Instance)
            .AddStep(first)
            .AddStep(second);

        var sagaId = Guid.NewGuid();
        await orchestrator.ExecuteAsync(
            sagaId,
            new SagaData("order-1"),
            new SagaExecutionMetadata("tenant-1", "corr-1", "event-1", "order-1"));

        var state = await store.LoadAsync(sagaId);
        Assert.NotNull(state);
        Assert.Equal(SagaStatus.Compensated, state!.Status);
        Assert.Equal("tenant-1", state.TenantKey);
        Assert.Equal("corr-1", state.CorrelationId);
        Assert.Equal("event-1", state.CausationId);
        Assert.Equal("order-1", state.IdempotencyKey);
        Assert.Equal(-1, state.StepIndex);
        Assert.Equal(1, first.CompensationCount);
        Assert.Equal(0, second.CompensationCount);
    }

    [Fact]
    public async Task Execute_IsIdempotentForTerminalSaga_AndDoesNotReexecuteSteps()
    {
        var store = new InMemorySagaStateStore();
        var step = new RecordingStep();
        var orchestrator = new PersistentSagaOrchestrator<SagaData>(
            store,
            new InMemoryLockManager(),
            NullLogger<PersistentSagaOrchestrator<SagaData>>.Instance)
            .AddStep(step);
        var sagaId = Guid.NewGuid();
        var metadata = new SagaExecutionMetadata(IdempotencyKey: "order-2");

        await orchestrator.ExecuteAsync(sagaId, new SagaData("order-2"), metadata);
        await orchestrator.ExecuteAsync(sagaId, new SagaData("order-2"), metadata);

        Assert.Equal(1, step.ExecutionCount);
        Assert.Equal(SagaStatus.Completed, (await store.LoadAsync(sagaId))!.Status);
    }

    private sealed record SagaData(string OrderId);

    private sealed class RecordingStep(bool throwOnExecute = false) : ISagaStep<SagaData>
    {
        public int ExecutionCount { get; private set; }
        public int CompensationCount { get; private set; }

        public Task ExecuteAsync(SagaData data, CancellationToken ct = default)
        {
            ExecutionCount++;
            if (throwOnExecute) throw new InvalidOperationException("step failed");
            return Task.CompletedTask;
        }

        public Task CompensateAsync(SagaData data, CancellationToken ct = default)
        {
            CompensationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySagaStateStore : ISagaStateStore
    {
        private readonly Dictionary<Guid, SagaInstance> _states = [];

        public Task SaveAsync(SagaInstance instance, CancellationToken ct = default)
        {
            _states[instance.SagaId] = instance;
            return Task.CompletedTask;
        }

        public Task<SagaInstance?> LoadAsync(Guid sagaId, CancellationToken ct = default) =>
            Task.FromResult(_states.TryGetValue(sagaId, out var state) ? state : null);

        public Task UpdateStatusAsync(Guid sagaId, string status, int stepIndex, DateTime heartbeat, CancellationToken ct = default)
        {
            var state = _states[sagaId];
            state.Status = status;
            state.StepIndex = stepIndex >= 0 ? stepIndex : state.StepIndex;
            state.LastHeartbeat = heartbeat;
            state.UpdatedAt = heartbeat;
            state.Version++;
            return Task.CompletedTask;
        }

        public Task<List<SagaInstance>> GetStaleAsync(TimeSpan staleThreshold, CancellationToken ct = default) =>
            Task.FromResult(_states.Values.Where(x => x.LastHeartbeat < DateTime.UtcNow - staleThreshold).ToList());
    }

    private sealed class InMemoryLockManager : ILockManager
    {
        public Task<IDistributedLock?> AcquireAsync(string key, TimeSpan? ttl = null, CancellationToken ct = default) =>
            Task.FromResult<IDistributedLock?>(new LockHandle(key));

        private sealed class LockHandle(string key) : IDistributedLock
        {
            public string Key { get; } = key;
            public long FencingToken => 1;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            public Task ReleaseAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task<bool> ExtendAsync(TimeSpan ttl, CancellationToken ct = default) => Task.FromResult(true);
        }
    }
}
