using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Application.Commands.DispatchAgent;

public class DispatchAgentHandler : IRequestHandler<DispatchAgentCommand, AgentRun>
{
    private readonly AgentPoolManager _poolManager;
    private readonly IStateStore _store;
    private readonly IEventBus _eventBus;
    private readonly PromptTemplateService _prompts;

    public DispatchAgentHandler(
        AgentPoolManager poolManager,
        IStateStore store,
        IEventBus eventBus,
        PromptTemplateService prompts)
    {
        _poolManager = poolManager;
        _store = store;
        _eventBus = eventBus;
        _prompts = prompts;
    }

    public async Task<AgentRun> Handle(DispatchAgentCommand request, CancellationToken ct)
    {
        var taskDescription = BuildPrompt(request.AgentName, request.TaskDescription, request.ContextFrom);

        var agentRun = AgentRun.Create(
            request.PipelineRunId,
            request.AgentName,
            taskDescription,
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

    private string BuildPrompt(string agentName, string taskDescription, string? contextFrom)
    {
        var tools = agentName switch
        {
            "dotnet" => "db-* MCP, rabbitmq, docker, agent-harness",
            "angular" => "filesystem, playwright, agent-harness",
            "devops" => "kubernetes, docker, prometheus, rabbitmq, redis",
            "security" => "kubernetes, redis, github security scanning",
            _ => "agent-harness and role-specific project tools"
        };

        var prompt = _prompts.GetPrompt("default-agent", new Dictionary<string, string>
        {
            ["task"] = taskDescription,
            ["tools"] = tools
        });

        if (!string.IsNullOrWhiteSpace(contextFrom))
        {
            prompt += $"\n\nContext source: {contextFrom}";
        }

        return prompt;
    }
}
