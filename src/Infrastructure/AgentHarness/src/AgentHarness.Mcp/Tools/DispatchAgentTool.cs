using System.Text.Json;
using MediatR;
using His.Hope.AgentHarness.Application.Commands.DispatchAgent;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class DispatchAgentTool
{
    private readonly IMediator _mediator;

    public DispatchAgentTool(IMediator mediator) => _mediator = mediator;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pipelineRunIdStr = parameters.GetValueOrDefault("pipeline_run_id")?.ToString()
            ?? throw new ArgumentException("'pipeline_run_id' is required.");
        var agentName = parameters.GetValueOrDefault("agent_name")?.ToString()
            ?? throw new ArgumentException("'agent_name' is required.");
        var taskDescription = parameters.GetValueOrDefault("task_description")?.ToString()
            ?? throw new ArgumentException("'task_description' is required.");

        if (!Guid.TryParse(pipelineRunIdStr, out var pipelineRunId))
            throw new ArgumentException("'pipeline_run_id' must be a valid GUID.");

        var maxRetries = 3;
        if (parameters.TryGetValue("max_retries", out var maxRetriesObj) && maxRetriesObj is JsonElement maxRetriesEl)
            maxRetries = maxRetriesEl.GetInt32();

        var timeoutSeconds = 600;
        if (parameters.TryGetValue("timeout", out var timeoutObj) && timeoutObj is JsonElement timeoutEl)
            timeoutSeconds = timeoutEl.GetInt32();

        var command = new DispatchAgentCommand(pipelineRunId, agentName, taskDescription, null, maxRetries, timeoutSeconds);
        var agentRun = await _mediator.Send(command);

        return JsonSerializer.Serialize(new
        {
            agent_run_id = agentRun.Id.ToString(),
            status = agentRun.Status.ToString()
        });
    }
}
