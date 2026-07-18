using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Application.Commands.DispatchAgent;

public class DispatchAgentHandler : IRequestHandler<DispatchAgentCommand, AgentRun>
{
    private readonly AgentPoolManager _poolManager;
    private readonly IStateStore _store;
    private readonly IEventBus _eventBus;

    public DispatchAgentHandler(AgentPoolManager poolManager, IStateStore store, IEventBus eventBus)
    {
        _poolManager = poolManager;
        _store = store;
        _eventBus = eventBus;
    }

    public async Task<AgentRun> Handle(DispatchAgentCommand request, CancellationToken ct)
    {
        var agentRun = AgentRun.Create(
            request.PipelineRunId,
            request.AgentName,
            request.TaskDescription,
            request.MaxRetries,
            request.TimeoutSeconds);

        // 1. Dispatch qua AgentPoolManager — circuit breaker + concurrency control
        var result = await _poolManager.DispatchWithPoolAsync(agentRun, ct);

        // 2. Tạo QualityGate từ kết quả agent
        var passed = result.Status == AgentRunStatus.Completed;
        var gate = QualityGate.Create(
            pipelineRunId: request.PipelineRunId,
            gateId: $"agent-{request.AgentName}",
            gateType: "agent-task",
            passed: passed,
            details: passed
                ? $"Agent {request.AgentName} completed: {request.TaskDescription}"
                : $"Agent {request.AgentName} failed ({result.Status}): {request.TaskDescription}");
        if (!passed)
            gate.MarkFailed(result.Status.ToString());

        await _store.SaveQualityGateAsync(gate, ct);

        // 3. Tạo Artifact từ agent output
        var storagePath = !string.IsNullOrEmpty(result.OutputArtifactRef)
            ? result.OutputArtifactRef
            : $"agents/{request.AgentName}/run-{result.Id:N}";
        var artifact = Artifact.Create(
            pipelineRunId: request.PipelineRunId,
            name: $"agent-{request.AgentName}",
            contentType: "text/plain",
            storagePath: storagePath,
            sizeBytes: 0);
        await _store.SaveArtifactAsync(artifact, ct);

        // 4. Publish event cho artifacts sẵn sàng
        await _eventBus.PublishAsync(
            new Core.Events.ArtifactReady(
                artifact.Id,
                request.PipelineRunId,
                request.AgentName,
                storagePath), ct);

        return result;
    }
}
